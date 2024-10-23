using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZStats
{

    public class Statistics
    {
        Config config;
        public List<Token> uniqueTokens = new List<Token>();
        public int totalPlays = 0;
        public int startYear;       // first year seen in PlayHistory

        public Statistics(Config config)
        {
            this.config = config;
        }

        public bool Compute(List<MCFile> files)
        {
            Console.WriteLine($"  Computing statistics");

            // parse History values
            foreach (var file in files)
                file.Process(config.historyFormat, config.midnightOffset);

            // infer pre-History timestamps
            if (Program.config.inferPreHistory)
            {
                DateTime historyStart = files.Min(f => f.HistoryStart);
                foreach (var file in files)
                    file.GeneratePreHistory(historyStart);
            }

            // find earliest History date/year, expand [perYear] playlists
            totalPlays = files.Sum(f => f.played.Count);
            startYear = files.Min(f => f.startYear);
            if (startYear < 2000) startYear = 2000;

            if (config.updatePlaylists)
                config.expandPlaylistYears(startYear);

            // calculate file stats
            foreach (var file in files)
                if (!ComputeStats(file))
                    return false;

            // populate playlists with top files
            if (config.updatePlaylists)
            { 
                Console.WriteLine($"  Preparing playlists");
                List<MCFile> sortedFiles = files.OrderByDescending(f => f.LastPlayed).ToList();
                foreach (var playlist in config.playlists)
                    PopulatePlaylist(playlist, sortedFiles);
            }

            return true;
        }

        private bool ComputeStats(MCFile file)
        {
            file.ComputedStats = new Dictionary<string, int>();
            file.yearlyStats = new Dictionary<string, List<int>>();
            file.monthlyStats = new List<int>();
            file.weekdayStats = new List<int>(); 
            file.LastPlayed = file.played.Count == 0 ? DateTime.MinValue : file.played.Min(p => p);

            foreach (var tokenObj in config.uniqueTokens)
            {
                string token = tokenObj.text;
                switch (tokenObj.type)
                {
                    case TokenType.Range:
                        file.ComputedStats[token] = file.played.Count(p => p >= tokenObj.start && p < tokenObj.end);
                        break;
                    case TokenType.Weekday:
                        file.ComputedStats[token] = file.played.Count(p => tokenObj.values.Contains((int)p.DayOfWeek));
                        break;
                    case TokenType.Month:
                        file.ComputedStats[token] = file.played.Count(p => tokenObj.values.Contains((int)p.Month));
                        break;
                    case TokenType.Year:
                        file.ComputedStats[token] = file.played.Count(p => tokenObj.values.Contains((int)p.Year));
                        break;
                    case TokenType.Unplayed:
                        file.ComputedStats[token] = file.LastPlayed == DateTime.MinValue ? 1 : 0;
                        break;
                    case TokenType.PreHistory:
                        file.ComputedStats[token] = file.PreHistoryCount;
                        break;
                    case TokenType.Recent:
                        file.ComputedStats[token] = file.LastPlayed < config.Now.AddMonths(-1) ? 0 : 1;
                        break;
                    case TokenType.Unpopular:
                        file.ComputedStats[token] = file.LastPlayed < config.Now.AddYears(-1) ? 1 : 0;
                        break;
                    case TokenType.PerWeekday:
                        for (int i = 0; i <= 6; i++)
                            file.weekdayStats.Add(file.played.Count(p => (int)p.DayOfWeek == i));    // TODO: user-defined weekstart
                        break;
                    case TokenType.PerMonth:
                        for (int i = 1; i <= 12; i++)
                            file.monthlyStats.Add(file.played.Count(p => p.Month == i));
                        break;
                    case TokenType.PerYear:
                        int year1 = tokenObj.values != null && tokenObj.values.Count > 0 ? tokenObj.values[0] : startYear;
                        List<int> counts = new List<int>();
                        for (int i = year1; i <= config.Now.Year; i++)
                            counts.Add(file.played.Count(p => p.Year == i));
                        file.yearlyStats[token] = counts;
                        break;

                    default:
                        tokenObj.invalid = true;
                        break;
                }
                if (tokenObj.invalid)
                {
                    Console.WriteLine($"  ERROR - invalid sort token in config file: [{token}]");
                    return false;
                }
            }       
            return true;
        }

        private bool PopulatePlaylist(MCPlaylist playlist, List<MCFile> files)
        {
            switch (playlist.sortToken.type)
            {
                case TokenType.Unplayed:
                    playlist.files = files.Where(f => f.LastPlayed == DateTime.MinValue).OrderByDescending(f => f.Imported).ToList();
                    break;
                case TokenType.Recent:
                    playlist.files = files.Where(f => f.LastPlayed != DateTime.MinValue).OrderByDescending(f => f.LastPlayed).ToList();
                    break;
                case TokenType.Unpopular:
                    playlist.files = files.Where(f => f.LastPlayed != DateTime.MinValue).OrderBy(f => f.LastPlayed).ToList();
                    break;
                case TokenType.PreHistory:
                    playlist.files = files.Where(f => f.PreHistoryCount > 0).OrderByDescending(f => f.PreHistoryCount).ToList();
                    break;
                default:
                    playlist.files = files.Where(f => f.StatsValue(playlist.sortToken.text) > 0).OrderByDescending(f => f.StatsValue(playlist.sortToken.text)).ToList();
                    break;
            }

            if (playlist.max > 0 && playlist.files.Count > playlist.max)
                playlist.files = playlist.files.GetRange(0, playlist.max);

            return true;
        }
    }
}
