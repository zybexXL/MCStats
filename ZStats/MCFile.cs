using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
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

        [DataMember]
        public string Stats { get; set; }

        public string newStats;
        public List<DateTime> played;
        public Dictionary<string, int> ComputedStats;
        public List<int> yearlyStats;
        public List<int> monthlyStats;
        public DateTime LastPlayed = DateTime.MinValue;

        static readonly Random rand = new Random();

        public int StatsValue(string token) { if (ComputedStats.TryGetValue(token, out int count)) return count; return 0; }

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
            return true;
        }

        bool ParseHistory(string dateFormat, int offsetMinutes)
        {
            played = new List<DateTime>();
            if (string.IsNullOrEmpty(History)) return true;

            var entries = History.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            bool numeric = Double.TryParse(entries[0], out _);

            string currEntry = null;
            try
            {
                foreach (var entry in entries)
                {
                    currEntry = entry;
                    DateTime date;
                    if (numeric) date = ExcelDate(Double.Parse(entry));
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

        DateTime ExcelDate(double days1900)
        {
            return new DateTime(1899, 12, 30).AddDays(days1900);
        }
    }

}
