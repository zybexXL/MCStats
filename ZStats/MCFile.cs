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

        [DataMember]
        public string History { get; set; }

        public string getProperty(string name) => jsonObject.GetValue(name)?.ToString();

        public JObject jsonObject;

        public List<DateTime> played;
        public Dictionary<string, int> ComputedStats;
        public Dictionary<string, List<int>> yearlyStats;
        public List<int> monthlyStats;
        public List<int> weekdayStats;
        public DateTime LastPlayed = DateTime.MinValue;
        public int startYear = 9999;    // no data

        static readonly Random rand = new Random();

        public int StatsValue(string token) { if (ComputedStats.TryGetValue(token, out int count)) return count; return 0; }
        public List<int> YearStatsValue(string token) { if (yearlyStats.TryGetValue(token, out var counts)) return counts; return new List<int>(); }

        void GenerateRandomDates()      // debug method to generate random History
        {
            int entries = rand.Next(50);
            List<string> dates = new List<string>();
            for (int i=5;i<entries;i++)
                 dates.Add((rand.NextDouble()*720 + 43831).ToString());
            History = string.Join(";", dates);
        }

        public bool Process(string dateFormat, int offsetMinutes)
        {
#if DEBUG
            //GenerateRandomDates();
#endif
            if (!ParseHistory(dateFormat, offsetMinutes)) return false;
            if (played.Count > 0) startYear = played.Min(p => p.Year);
            return true;
        }

        bool ParseHistory(string dateFormat, int offsetMinutes)
        {
            played = new List<DateTime>();
            if (string.IsNullOrEmpty(History)) return true;

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
