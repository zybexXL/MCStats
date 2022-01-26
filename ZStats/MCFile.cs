using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZStats
{
    [DataContract]
    public class MCFile
    {
        [DataMember] public int Key { get; set; }
        [DataMember] public string Name { get; set; }

        [DataMember(Name = "Date Imported")]
        public long Imported { get; set; }

        [DataMember(Name = "Number Plays")]
        public int NumberPlays { get; set; }

        [DataMember]
        public string History { get; set; }

        [DataMember]
        public string PreHistory { get; set; }

        public string getProperty(string name) => jsonObject.GetValue(name)?.ToString();

        public JObject jsonObject;
        public List<DateTime> played = new List<DateTime>();
        public List<DateTime> prePlayed = new List<DateTime>();
        public Dictionary<string, int> ComputedStats;
        public Dictionary<string, List<int>> yearlyStats;
        public List<int> monthlyStats;
        public List<int> weekdayStats;
        public DateTime LastPlayed = DateTime.MinValue;
        public int startYear = 9999;    // no data

        static readonly Random rand = new Random();

        public int StatsValue(string token) { if (ComputedStats.TryGetValue(token, out int count)) return count; return 0; }
        public List<int> YearStatsValue(string token) { if (yearlyStats.TryGetValue(token, out var counts)) return counts; return new List<int>(); }

        public int HistoryCount { get; private set; }           // [play history] length
        public int PreHistoryCount { get; private set; }        // [number plays] minus HistoryCount
        public DateTime HistoryStart { get; private set; }      // does not include pre-history
        public DateTime PreHistoryStart { get; private set; }   // includes pre-history; usually = ImportDate


        // debug method to generate random History spread out on last 2 years
        void GenerateRandomDates()
        {
            int entries = rand.Next(50);
            List<string> dates = new List<string>();
            double now = Util.Datetime2Excel(DateTime.Now) - 0.1;   // MC date
            for (int i=5;i<entries;i++)
                 dates.Add((now - rand.NextDouble()*730).ToString());
            History = string.Join(";", dates);
        }

        public bool Process(string dateFormat, int offsetMinutes)
        {
#if DEBUG
            GenerateRandomDates();
#endif
            if (!ParseHistory(dateFormat, offsetMinutes)) return false;
            HistoryCount = played.Count;
            HistoryStart = played.Count == 0 ? DateTime.Now : played.Min();
            PreHistoryStart = Util.Epoch2Datetime(Imported);
            PreHistoryCount = Math.Max(0, NumberPlays - played.Count);

            if (Program.config.inferPreHistory && PreHistoryCount > 0) 
                GeneratePreHistory();

            if (played.Count > 0) startYear = played.Min(p => p.Year);
            return true;
        }

        // generate missing timestamps if [Number Plays] is larger than [Play History] count
        void GeneratePreHistory()
        {
            DateTime date = PreHistoryStart;
            if (date >= HistoryStart) date = HistoryStart.AddYears(-1);     // hack to push pre-history to older dates

            double interval = ((HistoryStart - date).TotalSeconds) / PreHistoryCount;
            for (int i = 0; i < PreHistoryCount; i++)
            {
                prePlayed.Add(date);
                date = date.AddSeconds(interval);
            }
            played.AddRange(prePlayed);
        }

        bool ParseHistory(string dateFormat, int offsetMinutes)
        {
            if (string.IsNullOrEmpty(History)) return true;

            History = Regex.Replace(History, @"[\.,]", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            var entries = History.Split(new string[] { Program.config.listSeparator }, StringSplitOptions.RemoveEmptyEntries);
            bool numeric = Double.TryParse(entries[0], out _);

            string currEntry = null;
            try
            {
                foreach (var entry in entries)
                {
                    currEntry = entry;
                    DateTime date;
                    if (numeric) date = Util.Excel2Datetime(Double.Parse(entry));
                    else if (string.IsNullOrEmpty(dateFormat))
                        date = DateTime.Parse(entry, CultureInfo.CurrentCulture, DateTimeStyles.None);
                    else
                        date = DateTime.ParseExact(entry, dateFormat, CultureInfo.CurrentCulture, DateTimeStyles.None);

                    if (offsetMinutes != 0)
                        date.AddMinutes(-offsetMinutes);

                    played.Add(date);
                }
                return true;
            }
            catch
            {
                Console.WriteLine($"  ERROR: Failed to parse History entry '{currEntry}' in file {Key} ({Name})");
                return false;
            }
        }
    }
}
