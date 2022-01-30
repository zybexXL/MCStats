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
        public const int DEFAULT_PLAYLIST_SIZE = 100;
        public const int MC_PLAYLIST_BATCH = 100;

        Dictionary<string, string> props = new Dictionary<string, string>();

        public string MCserver { get { return getProp("mcserver", "http://localhost:52199"); } }
        public string MCuser { get { return getProp("mcuser"); } }
        public string MCpass { get { return getProp("mcpass"); } }
        public string MCfilter { get { return getProp("mcfilter"); } }
        public bool updateStats { get { return getProp("updatestats", "1") == "1"; } }
        public bool updatePlaylists { get { return getProp("updateplaylists", "1") == "1"; } }
        public bool runExpressions { get { return getProp("runexpressions", "1") == "1"; } }
        public bool createFields { get { return getProp("createfields", "1") == "1"; } }
        public bool inferPreHistory { get { return getProp("inferprehistory", "1") == "1"; } }
        public string preHistoryField { get { return getProp("prehistoryfield"); } }
        public string historyField { get { return getProp("historyfield", "Play History"); } }
        public string historyFormat { get { return getProp("historyformat"); } }
        public string listSeparator { get { return getProp("historyseparator", ";"); } }
        public string seriesSeparator { get { return getProp("seriesseparator", ","); } }
        public bool verbose { get { return getProp("verbose", "0") == "1"; } }


        public int midnightOffset { get {
                if (_midnightOffset < 0)
                    _midnightOffset = uint.TryParse(getProp("midnightoffset", "0"), out uint minutes) ? (int)minutes : 0; 
                return _midnightOffset; } }
        public int _midnightOffset = -1;

        public int weekStart { get {
                string day = getProp("weekStart", "monday");
                if (Enum.TryParse(day, true, out DayOfWeek weekday)) return (int)weekday;
                if (Enum.TryParse(day, true, out Weekdays day2)) return (int)day2;
                return 0; } }

        public List<MCPlaylist> playlists { get; private set; } = new List<MCPlaylist>();
        public List<ConfigStats> statGroups { get; private set; } = new List<ConfigStats>();
        public List<Token> uniqueTokens { get; private set; } = new List<Token>();
        public List<string> outputFields => statGroups.Where(g=>g.enabled).Select(g => g.UpdateField).Distinct().ToList();
        public List<string> groupbyFields => statGroups.Where(g => g.enabled).Select(g => g.GroupByField).Distinct().ToList();
        
        public string runBefore { get; private set; } = "";
        public string runAfter { get; private set; } = "";
        public DateTime Now { get; private set; } = DateTime.Now;   // reference runtime for [Now] token

        public bool valid = false;
        

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
                    return config;  // valid=false
                }
                else
                {
                    Console.WriteLine($"Config file not found: {path}");
                    return null;
                }
            }

            Console.WriteLine($"Reading config file: {path}");
            string[] ini = File.ReadAllLines(path);

            config.valid = config.Parse(ini);
            return config;
        }

        private bool Parse(string[] ini)
        {
            ConfigStats currStats = null;
            string section = null;

            for (int i = 0; i < ini.Length; i++)
            {
                string line = ini[i];
                if (line.Trim().StartsWith("#"))
                    continue;

                var m = Regex.Match(line, @"^(\s*\[(jriver|stats|playlists|runbefore|runafter)\])(\s*#.*)?$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    section = m.Groups[2].Value.ToLower();
                    if (section == "stats")
                    {
                        currStats = new ConfigStats() { order = statGroups.Count };
                        statGroups.Add(currStats);
                    }
                    continue;
                }

                if (section == "runbefore") runBefore += "\r\n" + line;
                else if (section == "runafter") runAfter += "\r\n" + line;
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    if (section == null)
                    {
                        Console.WriteLine($"  Line {i + 1}: missing section header");
                        return false;
                    }

                    m = Regex.Match(line, @"^(.+?)=(.+?)(#.*)?$");
                    if (!m.Success)
                    {
                        Console.WriteLine($"  Line {i + 1}: syntax error");
                        return false;
                    }

                    string key = m.Groups[1].Value.Trim();
                    string value = m.Groups[2].Value.Trim();

                    if (string.IsNullOrEmpty(value) && key.ToLower() != "mcfilter" && key.ToLower() != "prehistoryfield")
                    {
                        Console.WriteLine($"  Line {i + 1}: missing value for '{key}'");
                        return false;
                    }

                    bool ok = true;
                    if (section == "playlists")
                    {
                        m = Regex.Match(value, @"^(\[.+?\])\s*(,\s*(\d+))?$", RegexOptions.IgnoreCase);
                        Token sort = Token.Parse(m.Groups[1].Value, Now, weekStart);
                        if (!m.Success || sort.invalid)
                        {
                            Console.WriteLine($"  Line {i+1}: invalid playlist definition");
                            return false;
                        }

                        int count = string.IsNullOrWhiteSpace(m.Groups[3].Value) ? DEFAULT_PLAYLIST_SIZE : int.Parse(m.Groups[3].Value);
                        playlists.Add(new MCPlaylist(key, sort, count));
                    }

                    else if (section == "jriver")
                    {
                        ok = Regex.IsMatch(key, "^(mcserver|mcuser|mcpass|mcfilter|updatestats|inferprehistory|prehistoryfield|updateplaylists|runexpressions|createfields|HistoryFormat|historyfield|historyseparator|seriesseparator|weekstart|midnightoffset)$", RegexOptions.IgnoreCase);
                        props[key.ToLower()] = value;
                        //if (key == "weekstart" && config.weekStart < 0)
                        //    ok = false;
                    }

                    else if (section == "stats")
                    {
                        ok = Regex.IsMatch(key, "^(enabled|append|template|updatefield|groupbyfield)$", RegexOptions.IgnoreCase);
                        currStats.setProp(key, value);
                    }

                    if (!ok)
                    {
                        Console.WriteLine($"  Line {i + 1}: invalid option '{key}'");
                        return false;
                    }
                }
            }

            // get all unique tokens in config
            uniqueTokens = new List<Token>();
            if (updateStats)
                uniqueTokens = statGroups.Where(g => g.enabled).SelectMany(g => Token.ParseAll(g.Template, Now, weekStart)).ToList();
            uniqueTokens.AddRange(playlists.Select(g => g.sortToken));
            uniqueTokens = uniqueTokens.GroupBy(s => s.text).Select(s => s.First()).ToList();  // distinct

            var badToken = uniqueTokens.FirstOrDefault(t => t.invalid);
            if (badToken != null)
            {
                Console.WriteLine($"  Invalid template token: [{badToken.text}]");
                return false;
            }

            runBefore = runBefore.Trim();
            runAfter = runAfter.Trim();
            
            return true;
        }

        public static void CreateSampleConfig(string path)
        {
            Console.WriteLine($"Creating default config file: {path}\nPlease edit it to adjust your settings.");
            string sampleconfig = Util.GetEmbeddedResource("SampleConfig.ini");
            File.WriteAllText(path, sampleconfig ?? "");
        }

        // expand [perYear] playlist definition into multiple playlists
        public void expandPlaylistYears(int startYear)
        {
            List<MCPlaylist> lists = new List<MCPlaylist>();
            foreach (var playlist in playlists)
            {
                if (playlist.sortToken.type == TokenType.PerYear)
                {
                    int start = playlist.sortToken.values == null || playlist.sortToken.values.Count == 0 ? startYear : playlist.sortToken.values[0];
                    for (int y = start; y <= DateTime.Now.Year; y++)
                    {
                        string name = playlist.name.ToLower().Contains("[year]") ? Regex.Replace(playlist.name, @"\[year\]", y.ToString(), RegexOptions.IgnoreCase) : $"{playlist.name} ({y})";
                        Token token = new Token()
                        {
                            type = TokenType.Range,
                            start = new DateTime(y, 1, 1),
                            end = new DateTime(y + 1, 1, 1),
                            text = $"year={y}"
                        };
                        lists.Add(new MCPlaylist(name, token, playlist.max));
                        if (!uniqueTokens.Any(t=>t.text == token.text))
                            uniqueTokens.Add(token);
                    }
                }
                else
                    lists.Add(playlist);
            }
            playlists = lists;
        }
    }
}
