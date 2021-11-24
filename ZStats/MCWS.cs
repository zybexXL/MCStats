using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace ZStats
{
    public class MCWS
    {
        string host;
        string user;
        string pass;

        HttpClient http;
        bool debug = false;

        public MCWS(string server, string username, string password, bool verbose = false)
        {
            host = server.ToLower();
            user = username;
            pass = password;
            debug = verbose;

            if (!host.Contains(":") && !host.EndsWith("/")) host = $"{host}:52199";
            
            if (!host.StartsWith("http"))
                host = $"http://{host}";

            host = host.TrimEnd('/') + "/MCWS/v1/";

            HttpClientHandler handler = new HttpClientHandler();
            http = new HttpClient();
            http.BaseAddress = new Uri(host);
            var authToken = Encoding.ASCII.GetBytes($"{user}:{pass}");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",Convert.ToBase64String(authToken));
            http.DefaultRequestHeaders.ConnectionClose = true;      // MC is slow with connection=keep-alive
        }   

        ~MCWS()
        {
            http?.Dispose();
        }

        private int HttpGet(string url, out string answer, bool printDebug=true)
        {
            answer = null;
            try
            {
                string debugstr = url;
                if (debugstr.Length > 75) debugstr = debugstr.Substring(0, 75) + "(...)";
                if (debug && printDebug) Console.WriteLine($"  -> MCWS call: {debugstr}");

                using (HttpResponseMessage response = http.GetAsync(url).Result)
                {
                    answer = response.Content.ReadAsStringAsync().Result;

                    debugstr = answer != null && answer.Length < 75 ? $"'{answer.Replace('\r', '.').Replace('\n', '.')}'" : $"{answer.Length} bytes";
                    if (debug && printDebug) Console.WriteLine($"  <- MCWS says: {response.StatusCode}, {debugstr}");

                    return (int)(response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                if (debug)
                    Console.WriteLine($"  HTTP Exception: {ex.Message} :: {ex.InnerException?.Message}");
            }
            return 0;
        }

        public bool Authenticate()
        {
            var status = HttpGet("Authenticate", out string result);
            if (status == 0)
                Console.WriteLine($"Could not connect to MCWS at {host}");
            if (status == 401)
                Console.WriteLine("Invalid MCWS credentials - please check username and password");
            if (status != 200)
                Console.WriteLine($"Connection to {host} failed with status code {status}");
            else
                Console.WriteLine($"Connected to {host}");
            return status == 200;
        }

        public string SearchFiles(string filter, List<string> fields)
        {
            string url = "Files/Search?Action=JSON";
            if (fields != null && fields.Count > 0)
                url += $"&Fields={Uri.EscapeUriString(string.Join(",", fields))}";
            if (!string.IsNullOrEmpty(filter))
                url += $"&Query={Uri.EscapeUriString(filter)}";

            int status = HttpGet(url, out string result);
            if (status != 200) return null;
            return result;
        }

        public List<string> GetFields()
        {
            if (HttpGet("Library/Fields", out string xml) != 200)
                return null;

            var matches = Regex.Matches(xml, "<Field Name=\"(.+?)\"");
            return matches.Cast<Match>().Select(f => f.Groups[1].Value).ToList();
        }

        public bool SetField(int filekey, string field, string value, bool formattedValue=true)
        {
            value = Uri.EscapeUriString(value);
            field = Uri.EscapeUriString(field);
            string formatted = formattedValue ? "1" : "0";
            int status = HttpGet($"File/SetInfo?File={filekey}&Field={field}&Value={value}&Formatted={formatted}", out string xml, false);
            if (xml.Contains("Information=\"No changes.\""))
                status = 200;
            return status == 200;
        }

        public bool EvaluateExpression(int filekey, string expression, out string result)
        {
            expression = Uri.EscapeUriString(expression);
            result = "";
            int status = HttpGet($"File/GetFilledTemplate?File={filekey}&Expression={expression}", out string xml, false);
            var match = Regex.Match(xml, @"<Item Name=""Value"">(.*?)</Item>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success) result = Uri.UnescapeDataString(match.Groups[1].Value);
            return status == 200;
        }

        public List<MCPlaylist> GetPlayLists()
        {
            if (HttpGet("Playlists/List", out string xml) != 200)
                return null;

            var ids = Regex.Matches(xml, @"<Field Name=""ID"">(\d+)").Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).ToList();
            var names = Regex.Matches(xml, @"<Field Name=""Path"">(.+?)<").Cast<Match>().Select(m => m.Groups[1].Value).ToList();

            List<MCPlaylist> lists = new List<MCPlaylist>();
            for (int i = 0; i < ids.Count; i++)
                lists.Add(new MCPlaylist(names[i], ids[i]));
            return lists;
        }

        public bool DeletePlaylist(string name)
        {
            int status = HttpGet($"Playlist/Delete?PlaylistType=Path&Playlist={Uri.EscapeUriString(name)}", out _);
            return status == 200;
        }

        public bool DeletePlaylist(int id)
        {
            int status = HttpGet($"Playlist/Delete?PlaylistType=ID&Playlist={id}", out _);
            return status == 200;
        }

        public bool CreatePlaylist(string name, out int playlistID, bool overwrite = true)
        {
            string mode = overwrite ? "Overwrite" : "Rename";
            playlistID = 0; 
            int status = HttpGet($"Playlists/Add?Type=Playlist&Path={Uri.EscapeUriString(name)}&CreateMode={mode}", out string xml);
            var m = Regex.Match(xml ?? "", @"""PlaylistID"">(\d+)<");
            if (status == 200 && m.Success)
                int.TryParse(m.Groups[1].Value, out playlistID);
            return status == 200;
        }

        public bool BuildPlaylist(string name, List<int> fileIDs, out int playlistID)
        {
            if (fileIDs == null || fileIDs.Count == 0)
                return CreatePlaylist(name, out playlistID);

            string keys = string.Join(",", fileIDs);
            playlistID = 0;
            int status = HttpGet($"Playlist/Build?Playlist={Uri.EscapeUriString(name)}&Keys={keys}", out string xml);
            var m = Regex.Match(xml ?? "", @"""PlaylistID"">(\d+)<"); 
            if (status == 200 && m.Success)
                int.TryParse(m.Groups[1].Value, out playlistID);
            return status == 200;
        }

        public bool CreateField(string name)
        {
            Console.WriteLine($"  Creating field '{name}'");
            name = Uri.EscapeUriString(name);
            return HttpGet($"Library/CreateField?Name={name}", out string xml) == 200;
        }
    }
}
