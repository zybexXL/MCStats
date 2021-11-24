using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZStats
{
    public class Config
    {
        const int DEFAULT_LIST_SIZE = 100;

        Dictionary<string, string> props = new Dictionary<string, string>();

        public string MCserver { get { return getProp("mcserver", "http://localhost:52199"); } }
        public string MCuser { get { return getProp("mcuser"); } }
        public string MCpass { get { return getProp("mcpass"); } }
        public string MCfilter { get { return getProp("mcfilter"); } }

        public bool updateStats { get { return getProp("updatestats", "1") == "1"; } }
        public bool updatePlaylists { get { return getProp("updateplaylists", "1") == "1"; } }
        public bool runExpressions { get { return getProp("runexpressions", "1") == "1"; } }
        public bool createFields { get { return getProp("createfields", "1") == "1"; } }

        public string historyField { get { return getProp("historyfield", "Play History"); } }
        public string statsField { get { return getProp("statsfield", "Play Stats"); } }
        public string historyFormat { get { return getProp("historyformat"); } }

        public string statsTemplate { get { return getProp("statstemplate", "[total];[year];[month];[week];[today];[yesterday];[last6m];[Yearly];[Monthly]"); } }
        public string statsSeparator { get { return getProp("statsseparator", ","); } }
        public int yearlyBase { get { if (uint.TryParse(getProp("yearlybase", "2020"), out uint year)) return (int)year; return 2020; } }
        public int midnightOffset { get { if (uint.TryParse(getProp("midnightoffset", "0"), out uint minutes)) return (int)minutes; return 0; } }

        public List<MCPlaylist> playlists = new List<MCPlaylist>();

        public string runBefore { get; private set; } = "";
        public string runAfter { get; private set; } = "";


        string getProp(string key, string defaultValue = "")
        {
            if (!props.TryGetValue(key, out string value) || string.IsNullOrEmpty(value))
                return defaultValue;
            return value;
        }


        public static Config Load(string path, bool createDefault)
        {
            Config config = new Config();
            if (!File.Exists(path))
            {
                if (createDefault)
                {
                    CreateSampleConfig(path);
                    return null;
                }
                else
                {
                    Console.WriteLine($"Config file not found: {path}");
                    return null;
                }
            }

            Console.WriteLine($"Reading config file: {path}");
            string[] ini = File.ReadAllLines(path);

            string section = null;
            for (int i = 0; i < ini.Length; i++)
            {
                string line = ini[i];
                if (line.Trim().StartsWith("#"))
                    continue;

                var m = Regex.Match(line, @"^(\s*\[(stats|playlists|runbefore|runafter)\])(\s*#.*)?$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    section = m.Groups[2].Value.ToLower();
                    continue;
                }

                if (section == "runbefore") config.runBefore += "\r\n" + line;
                else if (section == "runafter") config.runAfter += "\r\n" + line;
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    m = Regex.Match(line, @"^(\s*.+?)=(.+?)(#.*)?$");
                    if (!m.Success)
                    {
                        Console.WriteLine($"Syntax error in line {i + 1} of {path}");
                        return null;
                    }

                    string key = m.Groups[1].Value.Trim();
                    string value = m.Groups[2].Value.Trim();

                    if (section == "playlists")
                    {
                        if (!Regex.IsMatch(value, @"^\[(today|yesterday|twodaysago|week|month|year|prevweek|prevmonth|prevyear|y20\d\d|yearly|m[1-9]|m1[0-2]|weekends|total|last\d+[hdm]|prev\d+[hdm]|recent|unplayed|lastplay)\](,\d+)?$", RegexOptions.IgnoreCase))
                        {
                            Console.WriteLine($"Invalid playlist sort order or count '{value}' in line {i + 1} of {path}");
                            return null;
                        }
                        var values = value.Split(',');
                        string sort = values[0];
                        int count = (values.Length > 1) ? int.Parse(values[1]) : DEFAULT_LIST_SIZE;

                        if (sort.ToLower()=="[yearly]")
                        {
                            for (int y=config.yearlyBase; y<=DateTime.Now.Year;y++)
                            {
                                string name = key.ToLower().Contains("[year]") ? Regex.Replace(key, @"\[year\]", y.ToString(), RegexOptions.IgnoreCase) : $"{key} ({y})";
                                config.playlists.Add(new MCPlaylist(name, $"y{y}", count));
                            }
                        }
                        else
                            config.playlists.Add(new MCPlaylist(key, sort, count));
                    }

                    if (section == "stats")
                    {
                        if (!Regex.IsMatch(key, "^(mcserver|mcuser|mcpass|mcfilter|updatestats|updateplaylists|runexpressions|createfields|historyfield|statstemplate|statsfield|statsseparator|midnightoffset|yearlyBase)$", RegexOptions.IgnoreCase))
                        {
                            Console.WriteLine($"Invalid option '{key}' in line {i + 1} of {path}");
                            return null;
                        }
                        config.props[key.ToLower()] = value;
                    }
                }
            }

            config.runBefore = config.runBefore.Trim();
            config.runAfter = config.runAfter.Trim();
            return config;
        }

        public static void CreateSampleConfig(string path)
        {
            Console.WriteLine($"Creating default config file: {path}\nPlease edit it to adjust your settings.");
            string sampleconfig = Util.GetEmbeddedResource("SampleConfig.ini");
            File.WriteAllText(path, sampleconfig ?? "");
        }

    }
}
