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
        public string hostURL { get; private set; }
        string user;
        string pass;

        HttpClient http;
        bool debug = false;
        
        public int status { get; private set; }

        public MCWS(string server, string username, string password, bool verbose = false)
        {
            hostURL = server.ToLower();
            user = username;
            pass = password;
            debug = verbose;

            if (!hostURL.Contains(":") && !hostURL.EndsWith("/")) hostURL = $"{hostURL}:52199";
            
            if (!hostURL.StartsWith("http"))
                hostURL = $"http://{hostURL}";

            hostURL = hostURL.TrimEnd('/') + "/MCWS/v1/";

            HttpClientHandler handler = new HttpClientHandler();
            http = new HttpClient();
            http.BaseAddress = new Uri(hostURL);
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

                    status = (int)(response.StatusCode);
                    return status;
                }
            }
            catch (Exception ex)
            {
                if (debug)
                    Console.WriteLine($"  HTTP Exception: {ex.Message} :: {ex.InnerException?.Message}");
            }
            status = 0;
            return 0;
        }

        public bool GetVersion(out Version version, out string appName, out string platform, out string servername)
        {
            appName = platform = servername = null;
            version = new Version();
            
            if (HttpGet("Alive", out string xml) != 200)
                return false;
            
            var props = Regex.Matches(xml, "<Item Name=\"(.+?)\">(.+?)</Item>").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            props.TryGetValue("ProgramName", out appName);
            props.TryGetValue("Platform", out platform);
            props.TryGetValue("FriendlyName", out servername);
            if (props.TryGetValue("ProgramVersion", out string ver)) version = Version.Parse(ver);
            else return false;
            
            return true;
        }

        public bool Authenticate()
        {
            return HttpGet("Authenticate", out string xml) == 200;
        }

        public string SearchFiles(string filter, List<string> fields)
        {
            string url = "Files/Search?Action=JSON";
            if (fields != null && fields.Count > 0)
                url += $"&Fields={Uri.EscapeUriString(string.Join(",", fields))}";
            if (!string.IsNullOrEmpty(filter))
                url += $"&Query={Uri.EscapeUriString(filter)}";

            return HttpGet(url, out string result) == 200 ? result : null;
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
            HttpGet($"File/SetInfo?File={filekey}&Field={field}&Value={value}&Formatted={formatted}", out string xml, false);
            if (xml != null && xml.Contains("Information=\"No changes.\""))
                status = 200;
            return status == 200;
        }

        public bool EvaluateExpression(int filekey, string expression, out string result)
        {
            result = "";
            expression = Uri.EscapeUriString(expression);
            if (HttpGet($"File/GetFilledTemplate?File={filekey}&Expression={expression}", out string xml, false) != 200)
                return false;
            var match = Regex.Match(xml ?? "", @"<Item Name=""Value"">(.*?)</Item>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success) result = Uri.UnescapeDataString(match.Groups[1].Value);
            return true;
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
            return HttpGet($"Playlist/Delete?PlaylistType=Path&Playlist={Uri.EscapeUriString(name)}", out _) == 200;
        }

        public bool DeletePlaylist(int id)
        {
            return HttpGet($"Playlist/Delete?PlaylistType=ID&Playlist={id}", out _) == 200;
        }

        public bool ClearPlaylist(int id)
        {
            return HttpGet($"Playlist/Clear?PlaylistType=ID&Playlist={id}", out _) == 200;
        }

        public bool RemovePlaylistDuplicates(int id)
        {
            return HttpGet($"Playlist/RemoveDuplicates?PlaylistType=ID&Playlist={id}", out _) == 200;
        }

        public bool CreatePlaylist(string name, out int playlistID, bool overwrite = true)
        {
            playlistID = 0;
            string mode = overwrite ? "Overwrite" : "Rename";
            if (HttpGet($"Playlists/Add?Type=Playlist&Path={Uri.EscapeUriString(name)}&CreateMode={mode}", out string xml) != 200)
                return false;
            var m = Regex.Match(xml ?? "", @"""PlaylistID"">(\d+)<");
            return m.Success && int.TryParse(m.Groups[1].Value, out playlistID);
        }

        public bool AddPlaylistFiles(int id, List<int> fileIDs, bool removeDuplicates = false)
        {
            if (fileIDs == null || fileIDs.Count == 0)
                return true;
            
            string keys = string.Join(",", fileIDs);
            bool ok = (HttpGet($"Playlist/AddFiles?PlaylistType=ID&Playlist={id}&Keys={keys}", out string xml) == 200);
            if (ok && removeDuplicates)
                ok &= RemovePlaylistDuplicates(id);
            return ok;
        }

        public bool BuildPlaylist(string name, List<int> fileIDs, out int playlistID)
        {
            playlistID = 0;
            if (fileIDs == null || fileIDs.Count == 0)
                return CreatePlaylist(name, out playlistID);

            string keys = string.Join(",", fileIDs);
            if (HttpGet($"Playlist/Build?Playlist={Uri.EscapeUriString(name)}&Keys={keys}", out string xml) != 200)
                return false;
            var m = Regex.Match(xml ?? "", @"""PlaylistID"">(\d+)<"); 
            return m.Success && int.TryParse(m.Groups[1].Value, out playlistID);
        }

        public bool CreateField(string name)
        {
            Console.WriteLine($"  Creating field '{name}'");
            name = Uri.EscapeUriString(name);
            return HttpGet($"Library/CreateField?Name={name}", out string xml) == 200;
        }
    }
}
