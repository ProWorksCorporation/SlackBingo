using System;
using System.Collections.Generic;

namespace SlackBingo.Models
{
    public class BingoData
    {
        public string ChannelName { get; set; }

        public ICollection<BingoPlayer> Players { get; set; }

        public IList<string> WordSet { get; set; }

        public int NextIndex { get; set; }

        public int SideSize { get; set; }

        public DateTimeOffset? Started { get; set; }

        public ICollection<BingoActivity> Activities { get; set; }
    }
}
