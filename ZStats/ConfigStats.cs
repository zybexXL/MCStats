using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZStats
{
    public class ConfigStats
    {
        Dictionary<string, string> props = new Dictionary<string, string>();

        public int order;
        public bool enabled { get { return getProp("enabled", "0") == "1"; } }
        public bool append { get { return getProp("append", "0") == "1"; } }
        public string UpdateField { get { return getProp("updatefield", "Play Stats"); } }
        public string GroupByField { get { return getProp("groupbyfield", "key"); } }
        public string Template { get { return getProp("template", "[total];[year];[month];[week];[today];[today-1];[year-1];[month-1];[week-1]"); } }
        public bool valid => !string.IsNullOrWhiteSpace(UpdateField) && !string.IsNullOrWhiteSpace(GroupByField) && !string.IsNullOrWhiteSpace(Template);


        public ConfigStats(bool enabled = false)
        {
            props["enabled"] = enabled ? "1" : "0";
        }

        public bool setProp(string key, string value)
        {
            props[key.ToLower()] = value;
            return true;
        }

        string getProp(string key, string defaultValue = "")
        {
            if (!props.TryGetValue(key, out string value) || string.IsNullOrEmpty(value))
                return defaultValue;
            return value;
        }
    }
}
