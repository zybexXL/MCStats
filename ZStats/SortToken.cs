using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZStats
{
    public class SortToken
    {
        public string token;
        public string iniToken;
        public string range;
        public int period;
        public string unit;
        public DateTime start;
        public DateTime end;
        public bool invalid = false;

        public static SortToken Parse(string token, DateTime NowOffset)
        {
            SortToken tokenObj = new SortToken() { token = token, iniToken = token };

            var m = Regex.Match(token, @"^([ym]|last|prev)(\d+)([hmd])?$");
            if (!m.Success) return tokenObj;
            if (m.Groups[1].Value.Length > 1 && string.IsNullOrEmpty(m.Groups[3].Value))
                tokenObj.invalid = true;
            else
            {
                tokenObj.token = "range";
                tokenObj.range = m.Groups[1].Value;
                tokenObj.period = int.Parse(m.Groups[2].Value);
                tokenObj.unit = m.Groups[3].Value;

                if (tokenObj.range == "last")
                {
                    tokenObj.end = NowOffset;
                    if (tokenObj.unit == "h") tokenObj.start = NowOffset.AddHours(-tokenObj.period);
                    else if (tokenObj.unit == "d") tokenObj.start = NowOffset.AddDays(-tokenObj.period);
                    else if (tokenObj.unit == "m") tokenObj.start = NowOffset.AddMonths(-tokenObj.period);
                }
                else if (tokenObj.range == "prev")
                {
                    switch (tokenObj.unit)
                    {
                        case "h":
                            tokenObj.start = NowOffset.AddHours(-tokenObj.period * 2);
                            tokenObj.end = NowOffset.AddHours(-tokenObj.period);
                            break;
                        case "d":
                            tokenObj.start = NowOffset.AddDays(-tokenObj.period * 2);
                            tokenObj.end = NowOffset.AddDays(-tokenObj.period);
                            break;
                        case "m":
                            tokenObj.start = NowOffset.AddMonths(-tokenObj.period * 2);
                            tokenObj.end = NowOffset.AddMonths(-tokenObj.period);
                            break;
                        default:
                            tokenObj.invalid = true;
                            break;
                    }
                }
            }
            return tokenObj;
        }
    }
}
