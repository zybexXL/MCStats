using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZStats
{

    public class Statistics
    {
        DateTime Now, NowOffset;
        DateTime Yesterday, Ereyesterday;
        DateTime WeekStart, PrevWeekStart;
        int prevMonth, prevMonthYear;
        
        Config config;
        MCFile[] files;
        List<SortToken> tokens;

        public int totalPlays = 0;

        public Statistics(MCFile[] files, Config config)
        {
            Now = DateTime.Now;
            NowOffset = Now.AddMinutes(-config.midnightOffset);     // correction for offset added to History timestamps
            
            this.files = files;
            this.config = config;
            
            Yesterday = Now.Date.AddDays(-1);
            Ereyesterday = Now.Date.AddDays(-2); 
            WeekStart = Now.Date.AddDays(-(int)Now.DayOfWeek);
            PrevWeekStart = WeekStart.AddDays(-7);
            prevMonth = Now.Date.AddMonths(-1).Month;
            prevMonthYear = Now.Date.AddMonths(-1).Year;
            
            List<string> tokenList = Regex.Matches(config.statsTemplate, @"\[(\w+?|\w+\d+[hdm]|[ymd]\d+)\]").Cast<Match>().Select(m=>m.Groups[1].Value.ToLower()).ToList();
            tokenList.AddRange(config.playlists.Select(p => p.sort.ToLower()).ToList());
            tokenList = tokenList.Distinct().ToList();

            tokens = tokenList.Select(t => SortToken.Parse(t, NowOffset)).ToList();
        }

        public bool Compute()
        {
            Console.WriteLine($"  Computing statistics");
            foreach (var file in files)
            {
                file.Process(config.historyFormat, config.midnightOffset);
                if (!ComputeStats(file, config.statsTemplate))
                    return false;
                totalPlays += file.played.Count;
            }

            Console.WriteLine($"  Preparing playlists");
            List<MCFile> sortedFiles = files.OrderByDescending(f => f.LastPlayed).ToList();
            if (config.updatePlaylists)
                foreach (var playlist in config.playlists)
                    GeneratePlaylist(playlist, sortedFiles);

            return true;
        }

        private bool ComputeStats(MCFile file, string template)
        {
            file.ComputedStats = new Dictionary<string, int>();
            file.yearlyStats = new List<int>();
            file.monthlyStats = new List<int>();
            file.LastPlayed = file.played.Count == 0 ? DateTime.MinValue : file.played.Min(p => p);

            foreach (var tokenObj in tokens)
            {
                string token = tokenObj.token;
                switch (token)
                {
                    case "total":
                        file.ComputedStats[token] = file.played.Count;
                        break;
                    case "year":
                        file.ComputedStats[token] = file.played.Count(p => p.Year == Now.Year);
                        break;
                    case "month":
                        file.ComputedStats[token] = file.played.Count(p => p.Year == Now.Year && p.Month == Now.Month);
                        break;
                    case "week":
                        file.ComputedStats[token] = file.played.Count(p => p >= WeekStart && p < Now);
                        break;

                    case "today":
                        file.ComputedStats[token] = file.played.Count(p => p.Date == Now.Date);
                        break;
                    case "yesterday":
                        file.ComputedStats[token] = file.played.Count(p => p.Date == Yesterday);
                        break;
                    case "twodaysago":
                        file.ComputedStats[token] = file.played.Count(p => p.Date == Ereyesterday);
                        break;

                    case "weekends":
                        file.ComputedStats[token] = file.played.Count(p => p.DayOfWeek == DayOfWeek.Saturday || p.DayOfWeek == DayOfWeek.Sunday);
                        break;

                    case "prevyear":
                        file.ComputedStats[token] = file.played.Count(p => p.Year == Now.Year - 1);
                        break;
                    case "prevmonth":
                        file.ComputedStats[token] = file.played.Count(p => p.Year == prevMonthYear && p.Month == prevMonth);
                        break;
                    case "prevweek":
                        file.ComputedStats[token] = file.played.Count(p => p >= PrevWeekStart && p < WeekStart);
                        break;

                    case "unplayed":
                        file.ComputedStats[token] = file.LastPlayed == DateTime.MinValue ? 1 : 0;
                        break;
                    case "recent":
                        file.ComputedStats[token] = file.LastPlayed == DateTime.MinValue ? 0 : 1;
                        break;
                    case "lastplay":
                        file.ComputedStats[token] = 0;
                        break;

                    case "yearly":
                        for (int i = config.yearlyBase; i <= Now.Year; i++)
                            file.yearlyStats.Add(file.played.Count(p => p.Year == i));
                        break;

                    case "monthly":
                        for (int i = 1; i < 13; i++)
                            file.monthlyStats.Add(file.played.Count(p => p.Month == i));
                        break;

                    case "range":
                        token = tokenObj.iniToken;
                        int period = tokenObj.period;
                        switch (tokenObj.range)
                        {
                            case "y":
                                file.ComputedStats[token] = file.played.Count(p => p.Year == tokenObj.period);
                                break;
                            case "m":
                                file.ComputedStats[token] = file.played.Count(p => p.Month == tokenObj.period); 
                                break;
                            case "prev":
                            case "last":
                                file.ComputedStats[token] = file.played.Count(p => p >= tokenObj.start && p < tokenObj.end);
                                break;
                        }
                        break;
                    default:
                        tokenObj.invalid = true;
                        break;
                }
                if (tokenObj.invalid)
                {
                    Console.WriteLine($"  ERROR - invalid sort token in config file: {token}");
                    return false;
                }

                string value = file.StatsValue(token).ToString();
                if (token == "yearly") value = string.Join(config.statsSeparator, file.yearlyStats);
                if (token == "monthly") value = string.Join(config.statsSeparator, file.monthlyStats);
                template = Regex.Replace(template, $@"\[{token}\]", value, RegexOptions.IgnoreCase);
                file.newStats = template;
            }
            
            return true;
        }

        private bool GeneratePlaylist(MCPlaylist playlist, List<MCFile> files)
        {
            var token = SortToken.Parse(playlist.sort, NowOffset);
            switch (token.token)
            {
                case "unplayed":
                    playlist.files = files.Where(f => f.LastPlayed == DateTime.MinValue).OrderByDescending(f => f.Imported).ToList();
                    break;
                case "recent":
                    playlist.files = files.Where(f => f.LastPlayed != DateTime.MinValue).OrderByDescending(f => f.LastPlayed).ToList();
                    break;
                case "lastplay":
                    playlist.files = files.Where(f => f.LastPlayed != DateTime.MinValue).OrderBy(f => f.LastPlayed).ToList();
                    break;
                default:
                    playlist.files = files.Where(f => f.StatsValue(token.iniToken) > 0).OrderByDescending(f => f.StatsValue(token.iniToken)).ToList();
                    break;
            }

            if (playlist.max > 0 && playlist.files.Count > playlist.max)
                playlist.files = playlist.files.GetRange(0, playlist.max);

            return true;
        }
    }
}
