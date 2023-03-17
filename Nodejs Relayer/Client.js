import * as dotenv from 'dotenv'
dotenv.config()
import websocketPlugin from "@fastify/websocket"
import Fastify from 'fastify'
const fastify = Fastify({
  logger: false
});
fastify.register(websocketPlugin);
import InworldClientManager from "./Inworld/InworldManager.js";
OverrideConsoleForProduction();

//
// NOTE: I regret big time not to select TypeScript for this project! :(
//

// Lets have our internal manager solution.
const clientManager = new InworldClientManager();
// Main entry
fastify.get('/', async (request, reply) => {
  return "You shouldn't be seeing this. Dont try to hack the system! :)"
});

// Create websocket 
fastify.register(async function (fastify) {
  fastify.get('/chatWithCharacter', {
    websocket: true,
  }, (connection, req) => {
    connection.socket.on('message', message => {
      let msgObject = JSON.parse(message.toString());
      if (msgObject.type == "connect") {
        console.log("[LOG] Message: ", msgObject)
        clientManager.ConnectToCharacter(msgObject.characterId, msgObject.playerName, msgObject.gameDate, async (msg) => {
          console.log(msg);
          if (msg.type == 'AUDIO') {
            if (process.env?.DISABLE_VOICE !== "true") {
              connection.socket.send(JSON.stringify({
                "data": msg.audio.chunk,
                "type": "audio"
              }));
            }
          } else if (msg.emotions) {
            connection.socket.send(JSON.stringify({
              "emotion": msg.emotions,
              "type": "emotion"
            }));
          } else if (msg.isText()) {
            let responseMessage = msg.text.text;
            connection.socket.send(JSON.stringify({
              "message": responseMessage,
              "type": "text"
            }));
          }
        });
      } else if (msgObject.type == "chat") {
        clientManager.SendMessageToCharacter(msgObject.message);
      } else if (msgObject.type == "endchat") {
        clientManager.TerminateConnection();
      }
    })
  })
});

// Create a new character
fastify.post('/checkCharacters', async (request, reply) => {
  return await clientManager.CheckCharacters(request.body.characters);
});
fastify.post('/checkCharacter', async (request, reply) => {
  return await clientManager.CheckCharacters([request.body.character]);
});
fastify.post("/createNewCharacter", async (request, reply) => {
  return await clientManager.CreateANewCharacter(request.body.id, request.body.name, request.body.description, request.body.isFemale, request.body.age, request.body.facts, request.body.personality);
});
fastify.post("/updateCommonKnowledge", async (request, reply) => {
  return await clientManager.UpdateCommonKnowledge(request.body.type, request.body.information);
});
fastify.post("/updateCharacter", async (request, reply) => {
  let body = request.body;
  return await clientManager.UpdateCharacter(body.id, body.name, body.description, body.pronoun, body.motivation, body.facts, body.temporaryFacts, body.exampleDialog, body.exampleDialogStyle,
    body.lifeStage, body.hobbyOrInterests, body.characterRole, body.personality, body.initialMood, body.voice, body.changeGenderVoice, body.gender, body.age);
});

fastify.delete("/deleteCharacter", async (request, reply) => {
  return await clientManager.DeleteCharacter(request.body.name);
});

fastify.delete('/deleteEverything', async (request, reply) => {
  return await clientManager.DeleteEverything();
});

fastify.post("/freshStart", async (request, reply) => {
  return await clientManager.RefreshEverything();
});

fastify.post("/createSave", (request, reply) => {
  let res = clientManager.CheckAndSetSaveVersion(request.body.id);
  if (res) {
    return {
      "isMatch": true
    }
  } else {
    return {
      "isMatch": false
    }
  }
})
// Run the server!
const StartEngine = async () => {
  try {
    await fastify.listen({
      port: 3000
    })
  } catch (err) {
    fastify.log.error(err);
    console.error(err);
    process.exit(1)
  }
}

StartEngine();

function OverrideConsoleForProduction() {
  console.log("\x1b[32m", `@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&GP#@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@BG#@@@@@@@BGB@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@J  .#@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@^ J@@@@@@@: 7@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&55Y5G@@@#P55G&@@&BB@@@#BB@@@#B#@@@B555G&@@@@#55@^ J@@&P555: 7@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@#^ .J@@#! .~^ .J@G .#@@? :&@@^ !@G^ .~: :5@#!  !@~ J@J. ^~.  7@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@G  Y@? ^#@@5  BG  #@@? .&@@^ !@^ ~@@@7  #7 :#@@^ JB  P@@#. !@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&. ?@7 ~@@@G  G#: ~5J.  75?  Y@J  ?5J: !@~ ^@@@^ J&^ ^Y5! .P@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&7!P@5!Y@@@#77B@#J~^^!5?^^^7G@@@G?^^^7P@@J!J@@@J!P@&Y!^^~J#@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&&&&&&@@@&&@@@@@@@@@@@@&@@@@@#GG#BPP#@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@P7?5@@@@@&JYB@@@@@@@@@@@@@@@@@@@@##B##BB#BB###G5YB#GY#B75@@@@@@@@@@#!G@@@@@B7~##^7&@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@&77^P@@@@5?!B@@@@@@@@@@@@@@@@@@BJBYB@@@@PB#5&@@P55#&!Y@7P@@@@@@@@&@&!#@@@@@@??@@7?@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@5&575@@P#Y!&BPGGPG&5B@BJ&B7YG55YG~P&&&G!G#B@@@J75PYJ@@?G@#PPJ&#PGBP7#GG#B#@?J@&7J@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@Y#@P!BGB@J?B7#@@#!BYG@B7&&7B@Y5@&5GB##&G?B@@@@P5P@@?5@?B#PB#JBJ#@@#JB7GBBB&J5@&!J@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@5B@@P?B@@?7#5P&&GJ#PP&G?B#?P@PJ&@PY&@@@@G?5##BGG&@#YPG?PPG#BJGGG#&5J#5G#B#&JG@&?Y@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@BY5@@&B@@#55#@#B##@@@#&&#@&##@&#&@&P5BBB#BP5J5GPGB#B&&BBB@#####@&###B#@&##&B?5&BJP&@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@#GYJPGGBGGPP#@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&&#&@@@@@@@@@@@@@@@@@@@@@@@@@@@###&#&&&@@&BG5??Y555555G&@@@@@@@@@@
  @@@@@@P~5@@@@@&5:J@@@@@@BBBB@@@@&BGB@@@@&B#@&BG#@@@@#B#&BBGGGGGG&@@BGGBBB&@@&BGG#@@@@@@@&BGGGB&@@&BBBBBB@@@@G~~#@@@&&BY75&@@@@@@@@
  @@@@@@G7Y@@@@@@#JP@@@@B?P&#57G@@@B~~?P&@P^P@@B^!JG@@5~B@#~:5PPGYG@@#!7&#P!5@@&J:#@@@@@#J5G##BP?J#@G~J##P7B@@P!?@@@@@@@@B77&@@@@@@@
  @@@@@@G?5BBGBG5JG@@@@@J5@@@@Y~#@@@J5&5JPY7B@@&7P&5YGYJ#@@?7&@@@@@@@&JJ@@#?5@@@G!#@@@@&!5@@@@@@&Y?@B7P@@P7B@@G7J&@@@@@@@@Y7P@@@@@@@
  @@@@@@G?P@@@@@@BJ?G@@&Y5GGGGY7B@@&Y5@@@GJ?B@@&JG@@&GJJ#@@5?&BPPPPPP5JJYJJ#@@@@BJ#@@@@&JP@@@@@@@GJ&#JJ5JJ#@@@BJ5@@@@@@@@#7?G@@@@@@@
  @@@@@@GJP@@@@@@@&?!&@&Y5@@@@G?B@@&J5@@@@B?G@@&YG@@@@P7B@@#?J&@@&#@@@Y5@#YJ&@@@P?#@@&#@B75&@@@@G7G@#JG@BYP&@@BJY&@@@@@@&!!5&@@@@@@@
  @@@@@@BJJ@@@@@@&5JB&BG5YB@@#G55#&GYJG@@#5P5#&P5YB@@BY5P#@@&PYPGP#@&GP5G@@PJP#&G5Y55YJP##PYPPP5Y#@#55P#@&G5P###BB&@@&&G7?G&@@@@@@@@
  @@@@&#G5?5GGGGGGB@@@&@@@&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@#GGB#&@@@@@@@@@&&&@@@@@@@@@@@@@#GPPPP55555P#@@@@@@@@@@@
  @@@@@&@@@@&&&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
  @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@`);
  console.log("\x1b[34m", "****************************************************");
  console.log("\x1b[32m", "Don't worry, you are suppose to see this!");
  console.log("\x1b[34m", "\n****************************************************\n\n");
  console.log("\x1b[31m", "DONT close this window if you want to use chat functionality. Close it only when you exit the game. You can simply close it when you are done with the game.");
  console.log("\x1b[32m", "Errors will shown here. If you encounter any error, dont close this and copy paste from here. This program does NOT keep logs.");
  console.fatalError = console.error;
  console.userLog = console.log;

  console.debug = (() => {});
  console.log = (function () {
    return function () {};
  })();
  console.trace = (function () {
    return function () {};
  })();
  console.warn = (function () {
    return function () {};
  })();
  console.info = (function () {
    return function () {};
  })();
  console.error = (function () {
    return function () {};
  })();
}