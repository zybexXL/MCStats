using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Reflection;

namespace ZStats
{
    internal static class Util
    {
        internal static string GetEmbeddedResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.ToLower().EndsWith(name.ToLower()));
            if (resourceName == null) return null;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        internal static bool TryJsonDeserialize<T>(string json, out T data)
        {
            data = default(T);
            try
            {
                data = JsonDeserialize<T>(json);
                return true;
            }
            catch { }
            return false;
        }

        internal static T JsonDeserialize<T>(string json)
        {
            if (json != null)
            {
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T), settings);
                    return (T)ser.ReadObject(ms);
                }
            }
            return default(T);
        }

        internal static string JsonSerialize<T>(T obj, bool indent = true)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, true, indent, "  "))
                {
                    var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T), settings);
                    serializer.WriteObject(writer, obj);
                    writer.Flush();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        internal static string JsonSerialize(Dictionary<string, string> keyValues)
        {
            if (keyValues == null) return "{ }";
            var tuples = keyValues.Select(v => $"\"{v.Key}\": {(v.Value == null ? "null" : $"\"{v.Value}\"")}").ToList();
            string json = $"{{ {string.Join(", ", tuples)} }}";
            return json;
        }
    }
}
