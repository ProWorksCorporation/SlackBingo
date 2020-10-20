using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using SlackBingo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static SlackBingo.Models.BingoGame;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;

namespace SlackBingo
{
    public static class Bingo
    {
        private const string BingoPartition = "Bingo";
        private static readonly string _slackApiToken = Environment.GetEnvironmentVariable("SlackApiToken");
        private static readonly string _postEphemeralUrl = Environment.GetEnvironmentVariable("SlackPostEphemeralUrl");
        private static readonly string[] _admins = Environment.GetEnvironmentVariable("BingoAdmins")?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
        private static readonly string[] _commands = Enum.GetNames(typeof(MoveType)).Concat(new[] { "help" }).Select(s => s.ToLowerInvariant()).ToArray();
        private static readonly string[] _adminCommands = { "kill", "list", "get" };
        private static readonly string[] _allCommands = _commands.Concat(_adminCommands).ToArray();
        private static readonly Regex _textPattern = new Regex("^(?<command>" + string.Join('|', _allCommands) + ")( (?<gameId>[0-9a-z_/-]+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [FunctionName("Bingo")]
        public static async Task<AppResponse> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Table("bingo")] CloudTable table,
            ILogger log,
            CancellationToken token)
        {
            var response = (AppResponse)null;
            try
            {
                var data = (req.Method.ToLowerInvariant() == "post" ? await req.ReadFormAsync(token) : (IEnumerable<KeyValuePair<string, StringValues>>)req.Query).ToDictionary(x => x.Key, x => x.Value.ToString());
                log.LogInformation($"Received {req.Method.ToUpperInvariant()} request - {JsonConvert.SerializeObject(data, Formatting.Indented)}");
                var userId = data.TryGetValue("user_id", out var value) ? value : null;
                var userName = data.TryGetValue("user_name", out value) ? value : null;
                var text = data.TryGetValue("text", out value) ? value : null;
                var channelId = data.TryGetValue("channel_id", out value) ? value : null;
                var channelName = data.TryGetValue("channel_name", out value) ? value : null;

                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userName)) return response = "Invalid request.  Please try `/bingo help` for a list of valid commands";

                var validCommands = _admins.Contains(userId) ? _allCommands : _commands;
                var match = _textPattern.Match(text ?? "");
                var command = match.Success ? match.Groups["command"].Value.ToLowerInvariant() : null;
                var argument = match.Success && match.Groups["gameId"].Success ? match.Groups["gameId"].Value : null;
                if (!match.Success || !validCommands.Contains(command) || command == "help") return response = "You can use one of the following commands:\r\n\r\n    `/bingo " + string.Join("`\r\n    `/bingo ", validCommands) + "`";
                if ((command == "kill" || command == "get") && string.IsNullOrWhiteSpace(argument)) return response = "You must provide a valid gameId with this command.  See `/bingo list` for a list of valid game IDs";

                try
                {
                    switch (command)
                    {
                        case "kill": return response = await Kill(table, argument, token);
                        case "get": return response = await Get(table, argument, token);
                        case "list": return response = await List(table, token);
                        default: return response = await Play(table, channelId, command, argument, userId, userName, channelName, log, token);
                    }
                }
                catch (Exception ex)
                {
                    log.LogError("Could not execute {command} for {userName} ({userId}) in {channelName} ({channelId}) - {error}", command, userName, userId, channelName, channelId, ex);
                    return response = "Oops, something went wrong";
                }
            }
            finally
            {
                log.LogInformation("Sending {type} response:\r\n{response}", response?.ResponseType.ToString() ?? "unknown", response?.Text);
            }
        }

        private static async Task<AppResponse> Play(CloudTable table, string gameId, string move, string argument, string userName, string displayName, string channelName, ILogger log, CancellationToken token)
        {
            if (!Enum.TryParse<MoveType>(move, true, out var action)) return "Invalid move";

            var row = await GetGame(table, gameId, token);
            var data = row?.Content;

            if (data == null && action != MoveType.Join) return "A game has not yet been created.  Try `/bingo join` to start a new game";
            if (data == null) row = new TableRow<BingoData> { Content = data = new BingoData { ChannelName = channelName, SideSize = 5, Activities = new List<BingoActivity>(), Players = new List<BingoPlayer>() }, PartitionKey = BingoPartition, RowKey = gameId };

            try
            {
                var game = new BingoGame(data, userName, displayName);
                var (error, result) = await game.TryMove(action, argument, log, token);

                switch (action)
                {
                    case MoveType.Join: return error == null ? (true, $"{displayName} has joined the game." + (result == null ? "  You will receive a card once the game has been started with `/bingo start`." : "  The game has already started, so you can view your card with `/bingo card`")) : (false, error);
                    case MoveType.Next: return error == null ? (true, $"The next word is `{result}`") : (false, error);
                    case MoveType.Leave: return error == null ? (true, $"{displayName} has left the game") : (false, error);
                    case MoveType.Bingo: return error == null ? (true, $"{displayName} has won!") : (false, error);
                    case MoveType.Start:
                        if (error != null) return error;

                        await PostCards(gameId, data.Players, log, token);

                        return (true, $"The game has been started.  The first word is `{result}`");
                    default: return error ?? result;
                }
            }
            finally
            {
                await table.ExecuteAsync(TableOperation.InsertOrReplace(row));
            }
        }

        private static async Task PostCards(string gameId, IEnumerable<BingoPlayer> players, ILogger log, CancellationToken token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _slackApiToken);
                foreach (var player in players)
                {
                    await PostCard(client, gameId, player.UserName, player.Format, player.Card, log, token);
                }
            }
        }

        private static async Task PostCard(HttpClient client, string gameId, string userName, string format, IList<IList<string>> card, ILogger log, CancellationToken token)
        {
            var message = new BingoCardMessage { Channel = gameId, UserName = userName, Text = CreateTable(format, card) };
            var body = JsonConvert.SerializeObject(message);

            using (var req = new HttpRequestMessage(HttpMethod.Post, _postEphemeralUrl))
            using (var content = new StringContent(body, new UTF8Encoding(false), "application/json"))
            {
                req.Content = content;

                using (var resp = await client.SendAsync(req, token))
                {
                    var result = await resp.Content.ReadAsStringAsync();
                    log.LogInformation("Got Slack ephemeral message response.  Request={request}, Response={response}", body, result);
                }
            }
        }

        private static async Task<AppResponse> Kill(CloudTable table, string gameId, CancellationToken token)
        {
            var game = await GetGame(table, gameId, token);
            if (game == null) return "No such game found";

            await table.ExecuteAsync(TableOperation.Delete(game), token);
            return "Game deleted";
        }

        private static async Task<AppResponse> List(CloudTable table, CancellationToken token)
        {
            var query = table.CreateQuery<TableRow<BingoData>>();
            var rows = (await query.ExecuteAsync(token)).ToList();
            if (rows.Count == 0) return "No games currently in progress";

            return CreateTable("table", rows.Select(x => new[] { x.RowKey, x.Content.ChannelName, x.Content.SideSize.ToString(), x.Content.Players?.Count.ToString(), x.Content.WordSet?.Count.ToString(), x.Content.NextIndex.ToString(), x.Content.Started?.ToString() }),
                "Game ID", "Channel Name", "Side Size", "Players", "Word Count", "Next Index", "Started");
        }

        private static async Task<AppResponse> Get(CloudTable table, string gameId, CancellationToken token) => JsonConvert.SerializeObject((await GetGame(table, gameId, token))?.Content, Formatting.Indented);

        private static async Task<TableRow<BingoData>> GetGame(CloudTable table, string gameId, CancellationToken token) => (await table.ExecuteAsync(TableOperation.Retrieve<TableRow<BingoData>>(BingoPartition, gameId), token))?.Result as TableRow<BingoData>;

        public class AppResponse
        {
            [JsonIgnore]
            public bool Public { get; set; }

            [JsonProperty("response_type")]
            public string ResponseType => Public ? "in_channel" : "ephemeral";

            [JsonProperty("text")]
            public string Text { get; set; }

            public static implicit operator AppResponse(string value) => new AppResponse { Text = value };
            public static implicit operator AppResponse((bool, string) value) => new AppResponse { Public = value.Item1, Text = value.Item2 };
        }
    }
}
