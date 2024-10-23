using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;

namespace ZStats
{
    internal static class Util
    {
        static DateTime MCEpoch = new DateTime(1899, 12, 30);

        internal static string GetEmbeddedResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.ToLower().EndsWith(name.ToLower()));
            if (resourceName == null) return null;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        internal static int[] SumArrays(List<List<int>> arrays)
        {
            int[] sum = new int[arrays[0].Count];
            foreach (var array in arrays)
                for (int i = 0; i < sum.Length; i++)
                    sum[i] += array[i];

            return sum;
        }

        internal static DateTime Excel2Datetime(double days1900)
        {
            return MCEpoch.AddDays(days1900);
        }

        internal static double Datetime2Excel(DateTime date)
        {
            return (date - MCEpoch).TotalDays;
        }

        internal static DateTime Epoch2Datetime(long epoch)
        {
            return new DateTime(1970, 1, 1).AddSeconds(epoch).ToLocalTime();
        }
    }
}
