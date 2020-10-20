using Newtonsoft.Json;

namespace SlackBingo.Models
{
    public class BingoCardMessage
    {
        [JsonProperty("blocks")]
        public string Blocks => JsonConvert.SerializeObject(new[] { new { type = "section", text = new { type = "mrkdwn", text = $"<@{UserName}> your card is:\r\n```{Text}```" } } });

        [JsonProperty("as_user")]
        public bool AsUser { get; } = true;

        [JsonProperty("username")]
        public string BotUserName { get; } = "Bingo Card";

        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("user")]
        public string UserName { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
