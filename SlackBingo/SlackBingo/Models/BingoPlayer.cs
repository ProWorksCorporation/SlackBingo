using System.Collections.Generic;

namespace SlackBingo.Models
{
    public class BingoPlayer
    {
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string Format { get; set; } = "table";
        public IList<IList<string>> Card { get; set; }
    }
}
