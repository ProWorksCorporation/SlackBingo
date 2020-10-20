using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;

namespace SlackBingo.Models
{
    public class TableRow : TableEntity
    {
        public TableRow()
        {
            Timestamp = DateTimeOffset.UtcNow;
        }

        public string Path { get; set; }
    }

    public class TableRow<T> : TableRow
    {
        public T Content { get; set; }

        public string Json
        {
            get => JsonConvert.SerializeObject(Content);
            set => Content = value == null ? default : JsonConvert.DeserializeObject<T>(value);
        }
    }

    public enum TableDataType
    {
        Card,
        Game,
        Player
    }
}
