using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ZStats
{
    public class CaseInsensitiveDictionary<T1, T2> : Dictionary<string, T2>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        { }
    }

    public class MCFile
    {
        public int Key { get; set; }
        public string Name { get; set; }

        [JsonPropertyName("Date Imported")]
        public long Imported { get; set; }

        [JsonPropertyName("Number Plays")]
        public int NumberPlays { get; set; }

        public string History { get; set; }

        public string PreHistory { get; set; }

        [JsonExtensionData]
        public CaseInsensitiveDictionary<string, object> ExtensionData { get; set; }


        public string getProperty(string name) => ExtensionData.TryGetValue(name, out object value) ? value?.ToString() ?? "" : "";

        public List<DateTime> played = new List<DateTime>();
        public List<DateTime> prePlayed = new List<DateTime>();
        public Dictionary<string, int> ComputedStats;
        public Dictionary<string, List<int>> yearlyStats;
        public List<int> monthlyStats;
        public List<int> weekdayStats;
        public DateTime LastPlayed = DateTime.MinValue;
        public int startYear = DateTime.Now.Year;

        static readonly Random rand = new Random();

        public int StatsValue(string token) { if (ComputedStats.TryGetValue(token, out int count)) return count; return 0; }
        public List<int> YearStatsValue(string token) { if (yearlyStats.TryGetValue(token, out var counts)) return counts; return new List<int>(); }

        public int HistoryCount { get; private set; }           // [play history] length
        public int PreHistoryCount { get; private set; }        // [number plays] minus HistoryCount
        public DateTime HistoryStart { get; private set; }      // does not include pre-history
        

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

        public void Process(string dateFormat, int offsetMinutes)
        {
#if DEBUG
            //GenerateRandomDates();
#endif
            if (!ParseHistory(dateFormat, offsetMinutes)) return;
            HistoryCount = played.Count;
            HistoryStart = played.Count == 0 ? Program.config.Now : played.Min();
            PreHistoryCount = Math.Max(0, NumberPlays - played.Count);

            if (played.Count > 0) 
                startYear = played.Min(p => p.Year);
        }

        // generate missing timestamps if [Number Plays] is larger than [Play History] count
        public void GeneratePreHistory(DateTime historyStart)
        {
            if (PreHistoryCount == 0) return;

            DateTime start = Util.Epoch2Datetime(Imported);
            if (start.Year < 2000) start = new DateTime(2000, 1, 1);          // prevent 1970 default timestamp
            if (start >= historyStart) start = historyStart.AddYears(-1);     // push pre-history to older dates in case of re-import

            double interval = ((historyStart - start).TotalSeconds) / PreHistoryCount;
            for (int i = 0; i < PreHistoryCount; i++)
            {
                prePlayed.Add(start);
                start = start.AddSeconds(interval);
            }

            played.AddRange(prePlayed);
            if (played.Count > 0)
                startYear = played.Min(p => p.Year);
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
