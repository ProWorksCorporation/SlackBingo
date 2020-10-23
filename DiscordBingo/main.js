if (typeof process.env.DISCORD_BOT_TOKEN !== 'string' || process.env.DISCORD_BOT_TOKEN.length == 0) {
    throw new Error("You must set the DISCORD_BOT_TOKEN environment variable");
}
if (typeof process.env.DISCORD_GAME_PATH !== 'string' || process.env.DISCORD_GAME_PATH.length == 0) {
    throw new Error("You must set the DISCORD_GAME_PATH environment variable");
}

const discordGamePath = process.env.DISCORD_GAME_PATH;
const fs = require('fs');
const discord = require('discord.io');
const bingo = require('./bingo.js');
let games = require(discordGamePath);

// Initialize Discord Bot
var bot = new discord.Client({
   token: process.env.DISCORD_BOT_TOKEN,
   autorun: true
});

bot.on('ready', function (evt) {
    console.log('Connected');
    console.log('Logged in as: ');
    console.log(bot.username + ' - (' + bot.id + ')');
});

bot.on('message', async function (userName, userId, channelId, message, evt) {
    // Our bot needs to know if it will execute a command
    // It will listen for messages that will start with `!`
    if (message.substring(0, 6) == '!bingo') {
        var args = (message.length > 7 ? message.substring(7) : '').split(' ');
        var cmd = args[0].toLocaleLowerCase();
        args = args.splice(1);

        if (!bingo.isValidCommand(userId, cmd)) cmd = 'help';

        console.log(`Processing ${cmd} ${args.join(' ')} command from ${userName} (${userId}) in ${channelId}`);

        try
        {
            var result = null;
            var game = null;

            switch (cmd) {
                case "list": result = list(args); break;
                case "kill": result = kill(args && args.length && args[0] && args[0].trim().length ? args[0] : channelId); break;
                case "get": result = get(channelId, args); break;
                default:
                    game = games[channelId];
                    if (cmd !== 'help' && !game) game = games[channelId] = games[channelId] || {channelId, players:[], nextIndex:0, sideSize:5, started: null, activities:[]};
                    result = await bingo.move(game || {}, userId, userName, cmd, args);
                    break;
            }

            if (result[0]) sendMessage(userId, result[0], `the error message to the user ${userName} (${userId})`);
            else if (cmd === 'start') {
                for (var i = 0; i < game.players.length; i++) {
                    const player = game.players[i];
                    const table = bingo.createTable(player.format, player.card);
                    sendMessage(player.userId, `Your Bingo card is:\r\n${table}`, `the bingo card to the user ${player.userName} (${player.userId})`);
                }
            }

            if (result[1]) sendMessage(channelId, result[1], `the success message to the channel ${channelId}`);
        }
        catch (err)
        {
            console.log("Unhandled exception - " + err);
            sendMessage(userId, 'Sorry we had trouble processing that.  Please try again later, or check with the bot admin', `the exception message to the user ${userName} (${userId})`);
        }
        finally
        {
            fs.writeFileSync(discordGamePath, JSON.stringify(games, null, 2));
        }
     }
});

function sendMessage(to, msg, type) {
    let msgs = Array.isArray(msg) ? msg : [`${msg}`];
    let messages = [];

    for (var i = 0; i < msgs.length; i++) {
        var fullMessage = msgs[i];

        while (fullMessage.length > 2000) {
            const message = fullMessage.substring(0, 2000);
            fullMessage = fullMessage.substring(2000);
            messages.push(message);
        }

        messages.push(fullMessage);
    }

    if (messages.length > 0) sendMessages(to, messages, type, 0);
}

function sendMessages(to, messages, type, index) {
    bot.sendMessage({to, message: messages[index]}, (e,r) => sendMessageCallback(type, e, r));

    // Space them out because the rate limit is 5 per 5 seconds
    if ((index + 1) < messages.length) setTimeout(() => sendMessages(to, messages, type, index + 1), 1250);
}

function sendMessageCallback (type, error, response) {
    if (!error) return;
    console.log(`There was an error posting ${type} - ${error}`);
}

function list (args) {
    const format = args && args.length && args[0] && args[0].trim().length ? args[0] : null;
    var rows = [['Channel', 'Side Size', 'Players', 'Word Count', 'Next Index', 'Started']];

    for (const [channel, game] of Object.entries(games)) {
        rows.push([channel, game.sideSize, game.players.length, game.wordSet ? game.wordSet.length : '', game.nextIndex, game.started]);
    }

    return [bingo.createTable(format, rows), null];
}

function kill (channelId) {
    const game = games[channelId];

    if (!game) return ["Could not find that game", null];

    delete games[channelId];
    return ["The game has been killed", null];
}

function get (channelId, args) {
    const chanId = args && args.length && args[0] && args[0].trim().length ? args[0] : channelId;
    const game = games[chanId];
    const includeActivities = args && args.length > 1 && ['true','include','activity','activities','includeactivity','includeactivities','include-activity','include-activities'].indexOf(args[1].toLocaleLowerCase()) >= 0 ? true : false;

    if (!game) return ["Could not find that game", null];

    var messages = [];
    var details = [
        "**Channel**:    ",
        chanId,
        "\r\n**Side Size**:  ",
        game.sideSize,
        "\r\n**Started**:    ",
        game.started,
        "\r\n**Next Index**: ",
        game.nextIndex,
        "\r\n**Words**:\r\n    ",
        (game.wordSet || []).map(x => `\`${x}\``).join(', ')
    ];
    messages.push(details.join(''));

    if (game.players && game.players.length > 0) {
        details = ["**Players**:"];
        for(var i = 0; i < game.players.length; i++) {
            const player = game.players[i];

            details.push("\r\n");
            details.push(player.userName);
            details.push("\r\n> ID:   ");
            details.push(player.userName);
            details.push("\r\n> Format: ");
            details.push(player.userName);
            if (player.card) {
                details.push("\r\n> Card:\r\n> ");
                details.push(bingo.createTable('csv', player.card).replace(/\n/g, "\n> "));
            }
            messages.push(details.join(''));
            details = [];
        }
    }

    if (includeActivities && game.activities && game.activities.length) {
        details = ["**Activities**:"];
        for (var i = 0; i < game.activities.length; i++) {
            const act = game.activities[i];

            details.push("\r\n");
            details.push(act.action);
            details.push(" by ");
            details.push(act.userName);
            details.push(" (");
            details.push(act.userId);
            details.push(") at ");
            details.push(act.date);
            if (act.error) {
                details.push("\r\n> Error: ");
                details.push(act.error.replace(/\n/g, "\n> "));
            }
            if (act.result) {
                details.push("\r\n> Result: ");
                details.push(act.result.replace(/\n/g, "\n> "));
            }
            messages.push(details.join(''));
            details = [];
        }
    }

    return [messages, null];
}