using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZStats
{
    public enum TokenType { Range, Year, Month, Weekday, PerYear, PerMonth, PerWeekday, Recent, Unpopular, Unplayed, PreHistory }
    public enum Weekdays { Sun = 0, Mon, Tue, Wed, Thu, Fri, Sat }      // C# enum uses Sun=0 on DayOfWeek enum
    public enum Months { Jan = 1, Feb, Mar, Apr, May, Jun, Jul, Aug, Sep, Oct, Nov, Dec }
    
    public class Token
    {
        public TokenType type;
        public string text;
        public DateTime start = DateTime.MinValue;
        public DateTime end = DateTime.MaxValue;
        public List<int> values;
        public bool invalid = false;


        public static bool isValid(string text)
        {
            var token = Parse(text, DateTime.Now, 0);
            return token != null && !token.invalid;
        }

        public static bool isAllValid(string template)
        {
            var tokens = ParseAll(template, DateTime.Now, 0);
            return tokens != null && tokens.Count > 0 && tokens.All(t => !t.invalid);
        }

        public static List<Token> ParseAll(string template, DateTime Now, int weekStart)
        {
            List<Token> tokens = new List<Token>();
            var matches = Regex.Matches(template, @"\[.+?\]");
            foreach (Match match in matches)
                tokens.Add(Parse(match.ToString(), Now, weekStart));

            return tokens;
        }

        public static DateTime AddOffset(DateTime date, string delta, int defaultDelta, string defaultUnit)
        {
            var m = Regex.Match(delta, @",?([-+]?\d+)([hwdmy])?", RegexOptions.IgnoreCase);

            int offset = !m.Success ? defaultDelta : int.Parse(m.Groups[1].Value);
            string unit = !m.Success || string.IsNullOrEmpty(m.Groups[2].Value) ? defaultUnit : m.Groups[2].Value;

            if (offset == 0) return date;
            switch (unit.ToLower())
            {
                case "h": return date.AddHours(offset);
                case "w": return date.AddDays(offset * 7);
                case "m": return date.AddMonths(offset);
                case "y": return date.AddYears(offset);
                default: 
                case "d": return date.AddDays(offset);
            }
        }

        public static Token Parse(string txt, DateTime Now, int weekStart)
        {
            Token token = new Token() { text = txt?.Trim('[', ']').ToLower() };
            txt = token.text?.Replace(" ", "");
            token.invalid = string.IsNullOrEmpty(txt);
            if (token.invalid) return token;

            // relative date range
            var m = Regex.Match(txt, @"^(now|today|week|month|year)([-+]\d+[hdwmy]?)?(,\d+[hdwmy]?)?$");
            if (m.Success)
            {
                token.type = TokenType.Range;
                switch (m.Groups[1].Value)
                {
                    case "now":
                        token.start = AddOffset(Now, m.Groups[2].Value, 0, "h"); 
                        token.end = AddOffset(token.start, m.Groups[3].Value, 1, "h");
                        break;
                    case "today":
                        token.start = AddOffset(Now.Date, m.Groups[2].Value, 0, "d");
                        token.end = AddOffset(token.start, m.Groups[3].Value, 1, "d");
                        break;
                    case "week":
                        int offset = ((int)Now.DayOfWeek - weekStart + 7) % 7;
                        token.start = AddOffset(Now.Date.AddDays(-offset), m.Groups[2].Value, 0, "w");
                        token.end = AddOffset(token.start, m.Groups[3].Value, 1, "w");
                        break;
                    case "month":
                        token.start = AddOffset(new DateTime(Now.Year, Now.Month, 1), m.Groups[2].Value, 0, "m");
                        token.end = AddOffset(token.start, m.Groups[3].Value, 1, "m");
                        break;
                    case "year":
                        token.start = AddOffset(new DateTime(Now.Year, 1, 1), m.Groups[2].Value, 0, "y");
                        token.end = AddOffset(token.start, m.Groups[3].Value, 1, "y");
                        break;
                }
                return token;
            }

            // numeric year/month/weekday (1 or more)
            m = Regex.Match(txt, @"^(weekday|month|year)=(\d+(,\d+)*)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                token.type = (TokenType) Enum.Parse(typeof(TokenType), m.Groups[1].Value, true);
                token.values = m.Groups[2].Value.Split(',').Select(n => int.Parse(n)).ToList();
                switch (token.type)
                {
                    case TokenType.Year: token.invalid = token.values.Any(v => v < 2000 || v > 2100); break;
                    case TokenType.Month: token.invalid = token.values.Any(v => v < 1 || v > 12); break;
                    case TokenType.Weekday: 
                        token.invalid = token.values.Any(v => v < 1 || v > 7); 
                        token.values = token.values.Select(v => (v - 1 + Program.config.weekStart) % 7).ToList();
                        break;
                }
                return token;
            }

            // weekday(s) by name
            string days = string.Join("|", Enum.GetNames(typeof(Weekdays))); 
            m = Regex.Match(txt, $@"^weekday=(({days})(?:day)?(,({days})(?:day)?)*)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                token.type = TokenType.Weekday;
                token.values = m.Groups[1].Value.Split(',').Select(v => (int)Enum.Parse(typeof(Weekdays), v, true)).ToList();
                return token;
            }

            // month(s) by name
            string months = string.Join("|", Enum.GetNames(typeof(Months)));
            m = Regex.Match(txt, $@"^month=(({months})(,({months}))*)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                token.type = TokenType.Month;
                token.values = m.Groups[1].Value.Split(',').Select(v => (int)Enum.Parse(typeof(Months), v, true)).ToList();
                return token;
            }

            // absolute date range
            m = Regex.Match(txt, @"^date=(\d{4}-\d{2}-\d{2}(\d{2}:\d{2})?)(,\d+[hdwmy]?)?$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                token.type = TokenType.Range;
                string format = string.IsNullOrEmpty(m.Groups[2].Value) ? "yyyy-MM-dd" : "yyyy-MM-ddHH:mm";
                token.start = DateTime.ParseExact(m.Groups[1].Value, format, CultureInfo.CurrentCulture);
                token.end = AddOffset(token.start, m.Groups[3].Value, 1, "d");
                return token;
            }

            // fixed tokens
            m = Regex.Match(txt, @"^(total|prehistory|weekends?|permonth|perweekday|peryear|recent|unpopular|unplayed)([,=](\d+))?$");
            if (m.Success)
            {
                switch (m.Groups[1].Value)
                {
                    case "total":
                        token.type = TokenType.Range;
                        break;
                    case "weekend":
                    case "weekends":
                        token.type = TokenType.Weekday;
                        token.values = new List<int> { (int)Weekdays.Sun, (int)Weekdays.Sat };
                        break;
                    case "recent":      
                    case "unpopular":
                    case "unplayed":
                    case "peryear":
                    case "permonth":
                    case "perweekday":
                    case "prehistory":
                        token.type = (TokenType)Enum.Parse(typeof(TokenType), m.Groups[1].Value, true);
                        if (!string.IsNullOrEmpty(m.Groups[3].Value))
                        {
                            token.values = new List<int> { int.Parse(m.Groups[3].Value) };
                            token.invalid = token.type != TokenType.PerYear;        // value only allowed to specify the starting year
                        }
                        break;
                }
                return token;
            }

            token.invalid = true;
            return token;
        }
    }
}
