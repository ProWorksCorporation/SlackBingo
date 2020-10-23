const adminUsers = (process.env.DISCORD_ADMIN_USERS || '').split(',');
const wordListUrl = process.env.WORD_LIST_URL;
const END_GAME = 'EndGame';
const request = require('request');
const defaultWords = ["ProWorks", "Umbraco", "Hackathon", "Community", "Friendly", "OpenSource", "Examine", "CodeGarden", "Heartcore", "Uno", "Cloud", "BackOffice", "FrontEnd", "CMS", "UmbracoForms", "Courier", "Deploy", "Headless", "Gridsome", "LoadBalancing", "JAMstack", "Staging", "Authoring", "Production", "Sitemap", "Team", "Collaboration", "Migration", "CSS", "Hacktoberfest", "Unicore", "SingleSignOn", "VueJS", "NoUmbraco9", "PreviewAPI", "GraphQL", "BlockListEditor", "Grid", "StackedContent", "Contentment", "WYSIWYG", "Documentation", "GoldPartner", "UmbracoCertified", "MVP", "CodePatch", "DUUGfest", "USFest", "Retreat", "Packages", "Roundtable", "UmbracoTees", "UmbraFriday", "Training"];
const nextWordPhrases = ['The next word is', 'And then we have', 'Up next is', 'Next is', 'Your next word is', 'Subsequently', 'And now', 'Next up', 'And then', 'Do I hear a BINGO for', 'Anyone missing', 'How about', 'Try', "Let's try"];

function BingoGame()
{
    const me = this;

    const randomizeWordList = function (words) {
        var allWords = words.slice(0, words.length);
        var randomized = [];

        while (allWords.length > 0) {
            const idx = Math.floor(Math.random() * allWords.length);
            randomized.push(allWords[idx]);
            allWords.splice(idx, 1);
        }

        return randomized;
    };

    const generateCard = function (game) {
        const words = randomizeWordList(game.wordSet);
        let card = [];
        var row = [];

        for (var i = 0; card.length < game.sideSize && i < words.length; i++) {
            row.push(words[i]);
            if (row.length >= game.sideSize) {
                card.push(row);
                row = [];
            }
        }

        return card;
    };

    const downloadPage = function (url) {
        return new Promise((resolve, reject) => {
            request(url, (error, response, body) => {
                if (error || response.statusCode !== 200) resolve([error, null]);
                else resolve([null, body]);
            });
        });
    };

    const getWordList = async function () {
        const result = await downloadPage(wordListUrl)
        if (result[0] || typeof result[1] !== 'string' || !result[1].length) return defaultWords;

        var words = result[1].replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n');
        return words.filter(x => x.trim().length > 0 && x.substring(0, 2) !== '//');
    };

    const hasBingo = function (usedWords, sideSize, card) {
        // Check for a completed row
        var valid = card.reduce((acc1, x) => acc1 || x.reduce((acc2, y) => acc2 && usedWords.indexOf(y) >= 0, true), false);

        // Check for a completed column
        for (var col = 0; col < sideSize && !valid; col++) {
            var invalid = false;

            for (var row = 0; row < sideSize && !invalid; row++) invalid = usedWords.indexOf(card[row][col]) < 0;

            valid = !invalid;
        }

        if (!valid)
        {
            var invalid = false;

            for (var i = 0; i < sideSize && !invalid; i++) invalid = usedWords.indexOf(card[i][i]) < 0;

            valid = !invalid;

            if (!valid)
            {
                invalid = false;

                for (var i = sideSize - 1; i >= 0 && !invalid; i--) invalid = usedWords.indexOf(card[i][i]) < 0;
    
                valid = !invalid;
            }
        }

        return valid;
    };

    me.createTable = function (format, rows) {
        if (!rows || !rows.length) return "No rows were returned";
        if (!Array.isArray(rows)) return "Not an array";
        const types = rows.reduce((acc, x, idx) => Array.isArray(x) ? acc : (acc + (acc ? ', ' : '') + `#${idx} is ${typeof(x)}`), '');
        if (types.length > 0) return "An element is not an array - " + types;

        const table = rows.map(x => x.map(y => `${y}`));
        const columns = table[0].length;
        if (table.find(x => x.length != columns)) return '';

        const longestValue = table.reduce((acc1, x) => Math.max(acc1, x.reduce((acc2, y) => Math.max(acc2, y.length), 0)), 0);
        let emptyLine = "", lineFormatter = null, prefixSuffix = "```";

        switch (format)
        {
            case "csv":
                lineFormatter = arr => arr.join(',') + '\r\n';
                break;
            case "tsv":
                lineFormatter = arr => arr.join('\t') + '\r\n';
                break;
            default:
                lineFormatter = arr => {
                    var line = arr.slice(0, arr.length);

                    for (var i = 0; i < line.length; i++) {
                        const val = line[i];
                        const spaces = longestValue - val.length;
                        if (spaces === 0) continue;

                        line[i] = new Array(Math.floor(spaces / 2) + 1).join(' ') + val + new Array(Math.ceil(spaces / 2) + 1).join(' ');
                    }

                    return '`' + line.join('`   `') + '`\r\n';
                };
                emptyLine = "\r\n";
                prefixSuffix = "";
                break;
        }

        return prefixSuffix + table.map(x => lineFormatter(x)).join(`${emptyLine}`) + prefixSuffix;
    };

    const endGame = function (game, userId, userName) {
        game.started = null;
        game.activities.push({userId, userName, date:new Date(), action:END_GAME});
    };

    const join = function (game, userId, userName, initialFormat) {
        const player = {userId, userName, format:'table'};
        game.players.push(player);
        if (!game.started) return initialFormat ? format(game, player, initialFormat) : null;

        player.card = generateCard(game);
        return initialFormat ? format(game, player, initialFormat) : card(game, player);
    };

    const start = async function (game) {
        const words = await getWordList();

        game.started = new Date();
        game.nextIndex = 1;
        const lastEnd = game.activities.find(x => x.action === END_GAME);
        if (lastEnd) {
            while (game.activities.length > 0 && game.activities[0] !== lastEnd) game.activities.splice(0, 1);
        }

        const multiplier = 1.6 + (game.players.length > 100 ? 1.0 : (Math.sqrt(game.players.length) / 10));
        game.wordSet = randomizeWordList(words.slice(0, Math.ceil(game.sideSize * game.sideSize * multiplier)));

        for (var i = 0; i < game.players.length; i++) game.players[i].card = generateCard(game);

        return [null, `The game has started.  Check your DM channel for your card.\r\n\r\nThe first word is \`${game.wordSet[0]}\``];
    };

    const next = function (game) {
        return game.wordSet[game.nextIndex++];
    };

    const bingo = function (game, player) {
        const usedWords = game.wordSet.slice(0, game.nextIndex);
        const isBingo = hasBingo(usedWords, game.sideSize, player.card);

        if (!isBingo) return card(game, player, 'table', true);

        endGame(game, player.userId, player.userName);
    };

    const leave = function (game, userId, userName) {
        const idx = game.players.findIndex(x => x.userId === userId);
        if (idx < 0) return;

        game.players.splice(idx, 1);

        if (game.players.length === 0) { endGame(game, userId, userName); return `${userName} has left the game, and the game has ended`; }

        return `${userName} has left the game`;
    };

    const format = function (game, player, arg) {
        const format = (arg || '').toLocaleLowerCase();

        switch (format) {
            case "table":
            case "tsv":
            case "csv":
                player.format = format;
                return `Your format has been set to \`${format}\`\r\n` + (game.started ? card(game, player) : '');
            default:
                return `Your current format is \`${player.format}\`.  Valid formats are \`table\`, \`csv\`, and \`tsv\`.  See \`!bingo help\` for more information.`;
        }
    };

    const card = function (game, player, cardFormat, bingoChecked) {
        const usedWords = game.wordSet.slice(0, game.nextIndex);
        const isBingo = bingoChecked ? false : hasBingo(usedWords, game.sideSize, player.card);

        let result = [];
        let cardWithUsed = player.card.map(x => x.map(y => usedWords.indexOf(y) >= 0 ? `**${y}**` : y));

        result.push(isBingo ? "YOU HAVE A BINGO!!!\r\n\r\n" : "");
        result.push("Your card is:\r\n");
        result.push(me.createTable(cardFormat || player.format, cardWithUsed));
        result.push("\r\n\r\n\r\nThe words which have been called so far are:\r\n`");

        for (var i = 0; i < usedWords.length; i++) {
            if (i > 0) result.push("`,`");
            result.push(usedWords[i]);
        }

        result.push("`");

        return result.join("");
    };

    me.move = async function (game, userId, userName, action, args) {
        const player = (game.players || []).find(x => x.userId === userId);
        const arg = args && args.length ? args[0] : null;
        var error = null;
        var result = null;

        switch (action) {
            case "join":
                if (player) error = "You have already joined this game. Use `!bingo leave` if you'd like to leave the game";
                else { error = join(game, userId, userName, arg); result = `Welcome to the game ${userName}`; }
                break;
            case "start":
                if (game.started) error = "The game is already in progress";
                else {
                    if (!player) join(game, userId, userName);
                    if (arg) format(game, player, arg);
                    const rslt = await start(game);
                    error = rslt[0];
                    result = (!player ? `Welcome to the game ${userName}\r\n` : '') + rslt[1];
                }
                break;
            case "next":
                if (!player) error = "You are not a player in this game";
                else if (!game.started) error = "The game has not yet started";
                else if (game.nextIndex >= game.wordSet.length) error = "All words have been used";
                else result = nextWordPhrases[Math.floor(Math.random() * nextWordPhrases.length)] + ` \`${next(game)}\``;
                break;
            case "bingo":
                if (!player) error = "You are not a player in this game";
                else if (!game.started) error = "The game has not yet started";
                else { error = bingo(game, player); result = error ? "Nice try, but not yet.  You need all words in a column, row, or one of the two full diagonals.  Keep going." : `${userName} HAS A BINGO!!!\r\n\r\nThe game is over, but can be restarted with the current players with just a \`!bingo start\``; }
                break;
            case "leave":
                if (!player) error = "You are not a player in this game";
                else result = leave(game, userId, userName);
                break;
            case "format":
                if (!player) error = "You are not a player in this game";
                else error = format(game, player, arg);
                break;
            case "card":
                if (!player) error = "You are not a player in this game";
                else if (!player.card) error = "You do not have a current card";
                else if (!game.started) error = "The game has not yet started";
                else error = card(game, player, arg);
                break;
            default:
                result = `You can use one of the following commands:
    \`!bingo join [{format}]\` Joins a game so that you will get a card the next time the game is started, optionally choosing your card format (see the format command below)
    \`!bingo start\`           Starts a game
    \`!bingo next\`            Calls out the next word
    \`!bingo bingo\`           Declares that you have a bingo (validation is performed...)
    \`!bingo leave\`           Leaves a game
    \`!bingo format {format}\` Switches how your card is sent to you between an inline table (table), a comma-separated values list (csv), and a tab-separated values list (tsv)
    \`!bingo card\`            Displays your card and which words have been called already
    \`!bingo help\`            Shows this help screen`;
                if (adminUsers.indexOf(userId) >= 0) error = `You have the following additional commands available to you:
    \`!bingo list\`            Lists all ongoing games
    \`!bingo get {channel}\`   Displays details about a given channel's game
    \`!bingo kill {channel}\`  Kills the game in a given channel`;
                break;
        }

        (game.activities || []).push({userId, userName, date:new Date(), action, error, result});

        return [error, result];
    }

    me.isValidCommand = function (userId, command) {
        var cmds = ['join', 'start', 'next', 'bingo', 'leave', 'format', 'card', 'help'];

        if (adminUsers.indexOf(userId) >= 0) cmds.splice(0, 0, ...['list', 'get', 'kill']);

        return cmds.indexOf(command.toLocaleLowerCase()) >= 0;
    }
}

module.exports = new BingoGame();