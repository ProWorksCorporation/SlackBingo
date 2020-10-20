using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlackBingo.Models
{
    public class BingoGame
    {
        private static readonly string[] AllWordList =
        {

        };
        private readonly BingoData _data;
        private readonly string _userName;
        private readonly string _displayName;

        public BingoGame(BingoData data, string userName, string displayName)
        {
            _data = data;
            _userName = userName;
            _displayName = displayName;
        }

        public (string Error, string Result) TryMove(MoveType type, string argument)
        {
            var error = (string)null;
            var result = (string)null;
            var player = _data.Players.FirstOrDefault(x => x.UserName == _userName);

            switch (type)
            {
                case MoveType.Join:
                    if (player != null) error = "You have already joined this game.  Use '/bingo leave' if you'd like to leave the game";
                    else if (_data.Started.HasValue) error = "Game is already in progress.  Please wait for the current game to end and then you can join";
                    else _data.Players.Add(new BingoPlayer { UserName = _userName, DisplayName = _displayName });
                    break;
                case MoveType.Start:
                    if (player == null) error = "You are not a player in this game";
                    else if (_data.Started.HasValue) error = "Game is already in progress";
                    else if (_data.Players.Count < 1) error = "You must have at least two people in the game to play";
                    else result = DoStart();
                    break;
                case MoveType.Next:
                    if (player == null) error = "You are not a player in this game";
                    else if (!_data.Started.HasValue) error = "Game has not yet started";
                    else if (_data.NextIndex >= _data.WordSet.Count) error = "All words have been used";
                    else result = _data.WordSet[_data.NextIndex++];
                    break;
                case MoveType.Bingo:
                    if (player == null) error = "You are not a player in this game";
                    else if (!_data.Started.HasValue) error = "Game has not yet started";
                    else error = DoBingo(player.Card);
                    break;
                case MoveType.Leave:
                    if (player == null) error = "You are not a player in this game";
                    else DoLeave(player);
                    break;
                case MoveType.Format:
                    if (player == null) error = "You are not a player in this game";
                    else result = DoFormat(player, argument);
                    break;
                case MoveType.Card:
                    if (player == null) error = "You are not a player in this game";
                    else if (player.Card == null) error = "You do not have a current card";
                    else if (!_data.Started.HasValue) error = "Game has not yet started";
                    else result = DoCard(player);
                    break;
            }

            if (_data.Activities == null) _data.Activities = new List<BingoActivity>();
            _data.Activities.Add(new BingoActivity { UserName = _userName, DisplayName = _displayName, DateTime = DateTimeOffset.UtcNow, Activity = type.ToString(), Error = error });

            return (error, result);
        }

        private string DoJoin()
        {
            var player = new BingoPlayer { UserName = _userName, DisplayName = _displayName };
            _data.Players.Add(player);

            if (!_data.Started.HasValue) return null;

            player.Card = GenerateCard();
            return DoCard(player);
        }

        private string DoStart()
        {
            _data.Started = DateTimeOffset.UtcNow;
            _data.NextIndex = 1;

            var lastEndGame = _data.Activities.LastOrDefault(x => x.Activity == "EndGame");
            if (lastEndGame != null)
            {
                BingoActivity removed;

                do
                {
                    removed = _data.Activities.First();
                    _data.Activities.Remove(removed);
                } while (removed != lastEndGame);
                _data.Activities.Remove(lastEndGame);
            }

            var multiplier = 1.6 + (_data.Players.Count > 100 ? 1.0 : (Math.Sqrt(_data.Players.Count) / 10.0));
            _data.WordSet = RandomizeWordList(WordList.List.Take((int)Math.Ceiling(_data.SideSize * _data.SideSize * multiplier))).ToList();
            foreach (var player in _data.Players)
            {
                player.Card = GenerateCard();
            }

            return _data.WordSet[0];
        }

        private string DoBingo(IList<IList<string>> card)
        {
            var usedWords = _data.WordSet.Take(_data.NextIndex).ToList();

            // Check for a completed row
            var valid = card.Any(row => row.All(word => usedWords.Contains(word)));

            // Check for a completed column
            for (var col = 0; col < _data.SideSize && !valid; col++)
            {
                var invalid = false;

                for (var row = 0; row < _data.SideSize && !invalid; row++)
                {
                    invalid = !usedWords.Contains(card[row][col]);
                }

                valid = !invalid;
            }

            if (!valid)
            {
                var invalid = false;

                // Check for a diagonal from upper left to lower right
                for (var i = 0; i < _data.SideSize && !invalid; i++)
                {
                    invalid = !usedWords.Contains(card[i][i]);
                }

                valid = !invalid;

                if (!valid)
                {
                    invalid = false;

                    // Check for a diagonal from upper right to lower left
                    for (var i = _data.SideSize - 1; i >= 0 && !invalid; i--)
                    {
                        invalid = !usedWords.Contains(card[i][i]);
                    }

                    valid = !invalid;
                }
            }

            if (!valid) return "You do not have a bingo";

            EndGame();

            return null;
        }

        private void DoLeave(BingoPlayer player)
        {
            _data.Players.Remove(player);
            if (_data.Players.Count == 0) EndGame();
        }

        private string DoFormat(BingoPlayer player, string argument)
        {
            var format = argument?.ToLowerInvariant() ?? "";
            switch (format)
            {
                case "table":
                case "csv":
                case "tsv":
                    player.Format = format;
                    return $"Your format has been set to {format}";
                default:
                    return $"Your current format is {player.Format}.  You can specify another format with one of the following commands:\r\n\r\n    /bingo format table        -- A table displayed inline in Slack\r\n\r\n    /bingo format csv          -- Comma-separated display\r\n\r\n    /bingo format tsv          -- Tab-separated display, for pasting directly into Excel or the like";
            }
        }

        private string DoCard(BingoPlayer player)
        {
            var response = new StringBuilder("Your card is:\r\n```");

            response.Append(CreateTable(player.Format, player.Card));
            response.Append("```\r\n\r\n\r\nThe words which have been called so far are:\r\n`");

            for(var i = 0; i < _data.NextIndex; i++)
            {
                if (i > 0) response.Append("`, `");
                response.Append(_data.WordSet[i]);
            }

            response.Append("`");

            return response.ToString();
        }

        private void EndGame()
        {
            _data.Started = null;
            _data.Activities.Add(new BingoActivity { UserName = _userName, DisplayName = _displayName, DateTime = DateTimeOffset.UtcNow, Activity = "EndGame" });
        }

        private IList<IList<string>> GenerateCard()
        {
            var card = new List<IList<string>>(_data.SideSize);
            var row = 0;

            for (var i = 0; i < _data.SideSize; i++) card.Add(new List<string>(_data.SideSize));

            foreach (var word in RandomizeWordList(_data.WordSet))
            {
                card[row].Add(word);
                if (card[row].Count >= _data.SideSize) { row++; }
                if (row >= _data.SideSize) break;
            }

            return card;
        }

        public static IEnumerable<string> RandomizeWordList(IEnumerable<string> words = null)
        {
            if (words == null) words = AllWordList;

            var prng = new Random();
            var set = new List<string>(words);

            while (set.Count > 0)
            {
                var nextIdx = prng.Next(set.Count);
                yield return set[nextIdx];
                set.RemoveAt(nextIdx);
            }
        }

        public static string CreateTable(string format, IEnumerable<IEnumerable<string>> rows, params string[] headers)
        {
            var table = rows.Select(x => x.Select(y => y ?? "").ToList()).ToList();
            if (table.Count == 0) return "";
            if (headers == null || headers.Length == 0) { headers = table[0].ToArray(); table.RemoveAt(0); }

            var allData = new List<IEnumerable<string>> { headers }.Concat(table).ToList();
            var columns = headers.Length;
            if (columns == 0 || table.Any(x => x.Count != columns)) return "Invalid table";

            var longestValue = allData.SelectMany(x => x).Max(x => x.Length);
            var spec = new FormatSpec();

            switch (format)
            {
                case "csv":
                    spec.Spacer = "";
                    spec.SpacerLine = "";
                    spec.LineFormat = string.Join(",", headers.Select((_, idx) => "{" + idx + "}")) + "\r\n";
                    spec.EmptyLine = "";
                    break;
                case "tsv":
                    spec.Spacer = "";
                    spec.SpacerLine = "";
                    spec.LineFormat = string.Join("\t", headers.Select((_, idx) => "{" + idx + "}")) + "\r\n";
                    spec.EmptyLine = "";
                    break;
                default:
                    spec.PadValues = true;
                    spec.Spacer = new string('*', (columns * (longestValue + 3)) + 1);
                    spec.SpacerLine = $"{spec.Spacer}\r\n";
                    spec.LineFormat = "* " + string.Join(" * ", headers.Select((_, idx) => "{" + idx + ",-" + longestValue + "}")) + " *\r\n";
                    spec.EmptyLine = string.Format(spec.LineFormat, headers.Select(_ => "").ToArray());
                    break;
            }

            return
                spec.SpacerLine +
                spec.EmptyLine +
                string.Join(spec.EmptyLine + spec.SpacerLine + spec.EmptyLine, allData.Select(x => string.Format(spec.LineFormat, x.Select(y => (spec.PadValues && y.Length < (longestValue - 1) ? new string(' ', (longestValue - y.Length) / 2) : "") + y).ToArray()))) +
                spec.EmptyLine +
                spec.Spacer;
        }

        public enum MoveType
        {
            Join,
            Start,
            Next,
            Leave,
            Bingo,
            Format,
            Card
        }

        private class FormatSpec
        {
            public bool PadValues { get; set; }
            public string Spacer { get; set; }
            public string SpacerLine { get; set; }
            public string LineFormat { get; set; }
            public string EmptyLine { get; set; }
        }
    }
}
