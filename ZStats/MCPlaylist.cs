﻿using System;
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
        public int max;
        public Token sortToken;
        public List<MCFile> files;

        public MCPlaylist(string name, Token sort, int count)
        {
            this.name = name;
            this.sortToken = sort;
            this.max = count;
        }

        public MCPlaylist(string name, int id)
        {
            this.name = name;
            this.id = id;
        }
    }
}
