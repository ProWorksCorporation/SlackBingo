using System;

namespace SlackBingo.Models
{
    public class BingoActivity
    {
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public DateTimeOffset DateTime { get; set; }
        public string Activity { get; set; }
        public string Error { get; set; }
    }
}
