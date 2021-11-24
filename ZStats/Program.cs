using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZStats
{
    class Program
    {

#if DEBUG
        static bool debug = true;
#else
        static bool debug = false;
#endif

        static MCWS mc;
        static Config config;
        static MCFile[] files;

        static void Main(string[] args)
        {
            Console.WriteLine("ZStats v0.90 for JRiver MediaCenter, by Zybex\n");
            DateTime start = DateTime.Now;
            try
            {
                string configfile = args.Length > 0 ? args[0] : "zstats.ini";
                config = Config.Load(configfile, true);
                if (config == null) return;

                runStats();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred during execution! Please repotr this issue to the developer:\n{ex}");
            }
            Console.WriteLine($"\nFinished in {DateTime.Now-start:hh':'mm':'ss}");
            Thread.Sleep(3000);
        }

        static void runStats()
        {
            mc = new MCWS(config.MCserver, config.MCuser, config.MCpass, debug);
            if (!mc.Authenticate())
                return;

            if (!ReadFiles(false)) return;
            if (files.Length == 0)
            {
                Console.WriteLine("No files found, please check the MCFilter in the config file!");
                return;
            }

            if (config.runExpressions && !string.IsNullOrWhiteSpace(config.runBefore.Trim()))
                if (!RunExpression(config.runBefore, "[RunBefore]")) return;

            if (config.updateStats || config.updatePlaylists)
            {
                if (!CheckFields()) return;

                if (!ReadFiles(true)) return;   

                if (!ProcessFiles()) return;

                if (config.updateStats)
                    if (!UpdateStats()) return;

                if (config.updatePlaylists)
                    if (!UpdatePlayLists()) return;
            }

            if (config.runExpressions && !string.IsNullOrWhiteSpace(config.runAfter.Trim()))
                if (!RunExpression(config.runAfter, "[RunAfter]")) return;
        }

        static bool CheckFields()
        {
            Console.WriteLine("Checking fields");
            var fields = mc.GetFields();
            if (fields == null)
            {
                Console.WriteLine("  Failed to get list of Fields!");
                return false;
            }
            bool hasStats = fields.Contains(config.statsField, StringComparer.InvariantCultureIgnoreCase);
            bool hasHistory = fields.Contains(config.historyField, StringComparer.InvariantCultureIgnoreCase);

            if (config.updateStats && !hasStats)
            {
                Console.WriteLine($"  field [{config.statsField}] is missing");
                if (!config.createFields) return false;
                if (!mc.CreateField(config.statsField))
                    return false;
            }

            if (config.updateStats && !hasHistory)
            {
                Console.WriteLine($"  field [{config.historyField}] is missing");
                if (!config.createFields) return false;
                if (!mc.CreateField(config.historyField))
                    return false;
            }

            return true;
        }

        static bool ReadFiles(bool withHistory)
        {
            Console.WriteLine($"Reading {(withHistory ? "play history" : "file list")}");
            var fields = new List<string> { "key", "name", "date imported" };
            if (withHistory)
            {
                fields.Add(config.statsField);
                fields.Add(config.historyField);
            }

            string data = mc.SearchFiles(config.MCfilter, fields);
            if (data == null) return false;

            data = Regex.Replace(data, $"\"{config.statsField}\":", "\"Stats\":", RegexOptions.IgnoreCase);
            data = Regex.Replace(data, $"\"{config.historyField}\":", "\"History\":", RegexOptions.IgnoreCase);
            
            if (!Util.TryJsonDeserialize<MCFile[]>(data, out files) || files == null)
            {
                Console.WriteLine("  Failed to deserialize MCWS response!");
                return false;
            }
            Console.WriteLine($"  {files.Length} files read");
            return files.Length > 0;
        }

        static bool ProcessFiles()
        {
            Console.WriteLine("Munching data");
            Statistics stats = new Statistics(files, config);
            if (!stats.Compute())
                return false;

            if (stats.totalPlays == 0)
                Console.WriteLine($"\nERROR: Looks like there's no play History recorded in the [{config.historyField}] field.\nPlease make sure your MC is configured to record PlayHistory. Check the yabb forum for details.");
            else
                Console.WriteLine($"  {stats.totalPlays} play history records processed");
            
            return stats.totalPlays > 0;
        }

        static bool RunExpression(string expression, string name)
        {
            Console.WriteLine($"Executing {name} expression");
            int ok = 0;
            int err = 0;
            int count = 0;
            foreach (var file in files)
            {
                if (count++ % 9 == 0)
                    Console.Write($"  file {count} of {files.Length}\r");

                if (!mc.EvaluateExpression(file.Key, expression, out _))
                    err++;
                else
                    ok++;
            }
            Console.WriteLine($"  {ok} files processed, {err} errors");
            return true;
        }

        static bool UpdatePlayLists()
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
                        Console.WriteLine($"  Updating List: {list.name}");
                        mc.DeletePlaylist(mclist.id);
                    }
                    else
                        Console.WriteLine($"  Creating List: {list.name}");

                    if (!mc.BuildPlaylist(list.name, list.files.Select(f => f.Key).ToList(), out int id))
                        Console.WriteLine($"    FAILED!");
                }
            }
            return true;
        }

        static bool UpdateStats()
        {
            Console.WriteLine($"Updating statistics field [{config.statsField}]");
            int ok = 0;
            int err = 0;
            int same = 0;
            int count = 0;
            foreach (var file in files)
            {
                if (count++ % 9 == 0)
                    Console.Write($"  updating file {count} of {files.Length}\r");

                if (file.Stats == file.newStats)
                    same++;
                else if (!mc.SetField(file.Key, config.statsField, file.newStats))
                {
                    err++;
                    Console.WriteLine($"  FAILED to set field [{config.statsField}] on file {file.Key}:{file.Name}");
                }
                else ok++;
            }

            Console.WriteLine($"  Updated {ok} files, {same} unchanged, {err} errors");
            return true;
        }

    }
}
