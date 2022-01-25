using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZStats
{
    class Program
    {
        static MCWS mc;
        public static Config config;
        static List<MCFile> files = new List<MCFile>();

        static readonly Version RequiredVersion = new Version(28, 0, 93);
        static readonly Version ZStatsVersion = new Version(0, 9, 5);

        static void Main(string[] args)
        {
            Console.WriteLine($"ZStats v{ZStatsVersion} for JRiver MediaCenter, by Zybex\n");
            DateTime start = DateTime.Now;
            if (args.Length > 0 && Regex.IsMatch(args[0], @"^[-/]"))
            {
                Console.WriteLine("Usage: ZStats [zstats.ini]");
                Console.WriteLine("If no config file is given and no 'zstats.ini' exists in the current folder, a new sample one is created.");
                return;
            }
            try
            {
                string configfile = args.Length > 0 ? args[0] : "zstats.ini";
                config = Config.Load(configfile, true);
                if (config == null && args.Length == 0) 
                    config = Config.Load(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), configfile), true);
                if (config == null || !config.valid) return;

                if (Connect())
                    runStats();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred during execution! Please report this issue to the developer:\n{ex}");
            }
            Console.WriteLine($"\nFinished in {DateTime.Now-start:hh':'mm':'ss}");
            Thread.Sleep(3000);
        }

        static bool Connect()
        {
            mc = new MCWS(config.MCserver, config.MCuser, config.MCpass, config.verbose);
            Console.WriteLine($"Connecting to {mc.hostURL}");

            if (!mc.GetVersion(out Version ver, out string app, out string platform, out string friendly))
            {
                if (mc.status == 0)
                    Console.WriteLine($"Could not connect to MCWS at {mc.hostURL}");
                else
                    Console.WriteLine($"Connection to {mc.hostURL} failed with status code {mc.status}");
                return false;
            }

            if (ver < RequiredVersion)
            {
                Console.WriteLine($"This application requires MC {RequiredVersion} or above. Please upgrade.");
                return false;
            }

            Console.WriteLine($"Connected to {app} {ver} on {friendly}");
            if (!mc.Authenticate())
            {
                if (mc.status == 401)
                    Console.WriteLine("Invalid MCWS credentials - please check username and password");
                else
                    Console.WriteLine($"Authentication failed with error {mc.status}");
                return false;
            }

            return true;
        }

        static void runStats()
        {
            if (!ReadMCFiles(false)) return;
            if (files.Count == 0)
            {
                Console.WriteLine("No files found, please check the MCFilter in the config file!");
                return;
            }

            if (config.runExpressions && !string.IsNullOrWhiteSpace(config.runBefore.Trim()))
                if (!RunMCExpression(config.runBefore, "[RunBefore]")) return;

            if (config.updateStats || config.updatePlaylists)
            {
                if (!CheckMCFields()) return;

                if (!ReadMCFiles(true)) return;   

                if (!ProcessFiles()) return;

                if (config.updateStats)
                    if (!UpdateMCStats()) return;

                if (config.updatePlaylists)
                    if (!UpdateMCPlayLists()) return;
            }

            if (config.runExpressions && !string.IsNullOrWhiteSpace(config.runAfter.Trim()))
                if (!RunMCExpression(config.runAfter, "[RunAfter]")) return;
        }

        // get the list of MC Fields and check if we need to create any new field
        // Optionally creates missing fields
        static bool CheckMCFields()
        {
            Console.WriteLine("Checking fields");
            var fields = mc.GetFields();
            if (fields == null)
            {
                Console.WriteLine("  Failed to get list of Fields!");
                return false;
            }

            if (!fields.Contains(config.historyField, StringComparer.InvariantCultureIgnoreCase))
            {
                Console.WriteLine($"  history field [{config.historyField}] does not exist");
                //if (!config.createFields) return false;
                //if (!mc.CreateField(config.historyField))
                    return false;
            }

            if (config.updateStats)
            {
                foreach (var group in config.statGroups)
                {
                    if (!group.enabled) continue;
                    if (!fields.Contains(group.UpdateField, StringComparer.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine($"  field [{group.UpdateField}] does not exist");
                        if (!config.createFields) return false;
                        if (!mc.CreateField(group.UpdateField))
                            return false;
                    }
                    if (group.GroupByField.ToLower() != "key" && !fields.Contains(group.GroupByField, StringComparer.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine($"  grouping field [{group.UpdateField}] does not exist");
                        //if (!config.createFields) return false;
                        //if (!mc.CreateField(group.UpdateField))
                            return false;
                    }
                }
            }

            return true;
        }

        // reads the list of MC files and their relevant fields
        static bool ReadMCFiles(bool withHistory)
        {
            Console.WriteLine($"Reading {(withHistory ? "play history" : "file list")}");
            var fields = new List<string> { "key", "name", "date imported" };
            if (withHistory)
            {
                fields.AddRange(config.groupbyFields);
                fields.AddRange(config.outputFields);
                fields.Add(config.historyField);
                fields = fields.Distinct().ToList();
            }

            try
            {
                string data = mc.SearchFiles(config.MCfilter, fields);
                if (data == null) return false;

                data = Regex.Replace(data, $"\"{config.historyField}\":", "\"History\":", RegexOptions.IgnoreCase);
                var objList = JArray.Parse(data);

                files = new List<MCFile>();
                foreach (JObject obj in objList)
                {
                    MCFile file = obj.ToObject<MCFile>();
                    file.jsonObject = obj;
                    files.Add(file);
                }
            }
            catch 
            {
                Console.WriteLine("  Failed to understand MCWS response!");
                return false;
            }
            Console.WriteLine($"  {files.Count} files read");
            return files.Count > 0;
        }

        // compute statistics for all files
        static bool ProcessFiles()
        {
            
            Console.WriteLine("Munching data");
            Statistics stats = new Statistics(config);
            if (!stats.Compute(files))
                return false;

            if (stats.totalPlays == 0)
                Console.WriteLine($"\nERROR: Looks like there's no play History recorded in the [{config.historyField}] field.\nPlease make sure your MC is configured to record PlayHistory. Check the yabb forum for details.");
            else
                Console.WriteLine($"  {stats.totalPlays} play history records processed");
            
            return stats.totalPlays > 0;
        }

        static bool RunMCExpression(string expression, string name)
        {
            Console.WriteLine($"Executing {name} expression");
            int ok = 0;
            int err = 0;
            int count = 0;
            foreach (var file in files)
            {
                if (count++ % 9 == 0)
                    Console.Write($"  file {count} of {files.Count}\r");

                if (!mc.EvaluateExpression(file.Key, expression, out _))
                    err++;
                else
                    ok++;
            }
            Console.WriteLine($"  {ok} files processed, {err} errors");
            return true;
        }

        static bool UpdateMCPlayLists()
        {
            if (config.playlists.Count == 0)
                Console.WriteLine("  No playlists defined in config file");
            else
            {
                Console.WriteLine($"Updating playlists");
                Console.WriteLine($"  Reading MC Playlists");
                var mclists = mc.GetPlayLists();
                foreach (var list in config.playlists)
                {
                    var mclist = mclists.Where(l => l.name.ToLower() == list.name.ToLower()).SingleOrDefault();
                    if (mclist != null)
                    {
                        // clear existing playlist
                        Console.WriteLine($"  Updating List: {list.name}");
                        mc.ClearPlaylist(mclist.id);
                    }
                    else
                    {
                        // create new playlist
                        Console.WriteLine($"  Creating List: {list.name}");
                        mclist = new MCPlaylist(list.name, 0);
                        if (!mc.CreatePlaylist(list.name, out mclist.id))
                            Console.WriteLine($"    FAILED to create playlist!");
                        else
                            mclists.Add(mclist);
                    }

                    // add files in blocks of 100
                    var files = list.files.Select(f => f.Key).ToList();
                    int batchsize = Config.MC_PLAYLIST_BATCH;
                    for (int i = 0; i < files.Count(); i += batchsize)
                        if (!mc.AddPlaylistFiles(mclist.id, files.Skip(i).Take(batchsize).ToList()))
                        {
                            Console.WriteLine($"    FAILED to add files to playlist!");
                            break;
                        }
                }
            }
            return true;
        }

        static bool UpdateMCStats()
        {
            foreach (var group in config.statGroups)
            {
                if (!group.enabled) continue;

                Console.WriteLine($"Updating statistics field [{group.UpdateField}], grouped by [{group.GroupByField}]");

                Dictionary<string, string> templateGroups = new Dictionary<string, string>();

                int ok = 0;
                int err = 0;
                int same = 0;
                int count = 0;
                foreach (var file in files)
                {
                    if (count++ % 9 == 0)
                        Console.Write($"  updating file {count} of {files.Count}\r");

                    string currStats = file.getProperty(group.UpdateField);
                    string template = group.Template;
                    if (string.IsNullOrEmpty(group.GroupByField) || group.GroupByField.ToLower() == "key")
                        template = fillTemplate(template, file, config);
                    else
                    {
                        string key = file.getProperty(group.GroupByField)?.ToLower();
                        if (!templateGroups.ContainsKey(key))
                        {
                            var fgroup = files.Where(f => f.getProperty(group.GroupByField).Equals(key, StringComparison.InvariantCultureIgnoreCase)).ToList();
                            templateGroups[key] = fillGroupTemplate(template, fgroup, config);
                        }
                        template = templateGroups[key];
                    } 
                    if (group.append) template = $"{currStats}{template}";
                    if (currStats == template)
                        same++;
                    else if (!mc.SetField(file.Key, group.UpdateField, template))
                    {
                        err++;
                        Console.WriteLine($"  FAILED to set field [{group.UpdateField}] on file {file.Key}:{file.Name}");
                    }
                    else ok++;
                }
                Console.WriteLine($"  Updated {ok} files, {same} unchanged, {err} errors");
            }
            return true;
        }

        public static string fillTemplate(string template, MCFile file, Config config)
        {
            string tLower = template.ToLower();
            foreach (var token in config.uniqueTokens)
            {
                if (tLower.Contains(token.text))
                {
                    string value = file.StatsValue(token.text).ToString();
                    if (token.type == TokenType.PerYear) value = string.Join(config.seriesSeparator, file.YearStatsValue(token.text));
                    if (token.type == TokenType.PerMonth) value = string.Join(config.seriesSeparator, file.monthlyStats);
                    if (token.type == TokenType.PerWeekday) value = string.Join(config.seriesSeparator, file.weekdayStats);
                    template = Regex.Replace(template, $@"\[{token.text}\]", value, RegexOptions.IgnoreCase);
                }
            }
            return template;
        }

        public static string fillGroupTemplate(string template, List<MCFile> groupFiles, Config config)
        {
            string tLower = template.ToLower();
            foreach (var token in config.uniqueTokens)
            {
                if (tLower.Contains(token.text))
                {
                    string value = groupFiles.Sum(f => f.StatsValue(token.text)).ToString();
                    if (token.type == TokenType.PerYear) value = string.Join(config.seriesSeparator, Util.SumArrays(groupFiles.Select(f=>f.YearStatsValue(token.text)).ToList()));
                    if (token.type == TokenType.PerMonth) value = string.Join(config.seriesSeparator, Util.SumArrays(groupFiles.Select(f => f.monthlyStats).ToList()));
                    if (token.type == TokenType.PerWeekday) value = string.Join(config.seriesSeparator, Util.SumArrays(groupFiles.Select(f => f.weekdayStats).ToList()));
                    template = Regex.Replace(template, $@"\[{token.text}\]", value, RegexOptions.IgnoreCase);
                }
            }
            return template;
        }
    }
}
