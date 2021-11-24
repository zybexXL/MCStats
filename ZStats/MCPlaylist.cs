using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZStats
{
    public class MCPlaylist
    {
        public int id;
        public string name;
        public string sort;
        public int max;
        public SortToken sortToken;
        public List<MCFile> files;

        public MCPlaylist(string name, string sort, int count)
        {
            this.name = name;
            this.sort = sort.ToLower().Trim('[', ']');
            this.max = count;
        }

        public MCPlaylist(string name, int id)
        {
            this.name = name;
            this.id = id;
        }
    }
}
