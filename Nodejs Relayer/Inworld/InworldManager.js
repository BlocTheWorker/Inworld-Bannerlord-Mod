import {
    InworldClient,
    SessionToken
} from '@inworld/nodejs-sdk';
import {
    createRequire
} from "module";
import InworldLoginSystem from "./InworldLoginSystem.js"
import axios from 'axios';

import {
    Low
} from 'lowdb'
import {
    JSONFileSync
} from 'lowdb/node'

import { Sanitizer } from './InworldDecencyHelper.js'

const db = new Low(new JSONFileSync('conversationHistory.json'))
db.read()
db.data ||= { conversations: [], saveInformation: {} }

const require = createRequire(import.meta.url);
const CHAR_TEMPLATE = require("../Templates/WorldBuilding/CharacterTemplate.json");
const VILLAGER_TEMPLATE = require("../Templates/WorldBuilding/VillagerTemplate.json");
const COMMON_KNOWLEDGE = require("../Templates/WorldBuilding/CommonKnowledge.json");
const VOICE_DEFINITIONS = require("../Templates/VoiceMap/VoiceDefinitions.json").voices;
const MALE_VOICES = VOICE_DEFINITIONS.filter(voice => voice.gender == "MALE");
const FEMALE_VOICES = VOICE_DEFINITIONS.filter(voice => voice.gender == "FEMALE");

const WORKSPACE_NAME = process.env.INWORLD_WORKSPACE;
const COMMON_KNOWLEDGE_URI = "https://studio.inworld.ai/v1alpha/workspaces/" + WORKSPACE_NAME + "/common-knowledges?pageSize=500"
const CREATE_URI = "https://studio.inworld.ai/v1alpha/workspaces/" + WORKSPACE_NAME + "/characters?skipAutoCreate=false";
const GET_CHARACTERS = "https://studio.inworld.ai/v1alpha/workspaces/" + WORKSPACE_NAME + "/characters";
const GET_CHAR_URI = "https://studio.inworld.ai/v1alpha/workspaces/" + WORKSPACE_NAME + "/characters/{id}?view=CHARACTER_ITEM_VIEW_WITH_META"
const defaultConfigurationConnection = {
    autoReconnect: true,
    disconnectTimeout: 3600 * 60
}
const VILLAGER_ID = "unique-villager-fact";

Array.prototype.random = function () {
    return this[Math.floor((Math.random() * this.length))];
}

function getRandomFloat(min, max, decimals) {
    const str = (Math.random() * (max - min) + min).toFixed(decimals);
    return parseFloat(str);
}

export default class InworldClientManager {
    currentCapabilities = {
        audio: true,
        emotions: true
    }

    currentConversationCharacter = null;
    currentScene = null;
    isConnected = false;
    isWaitingResponse = false;
    responseMessage = "";
    characterList = [];
    requestedCharacterIdentifier = null;
    conversationDate = null;
    languageFilter = null;

    constructor() {
        this.languageFilter = new Sanitizer();
        this.loginHelper = new InworldLoginSystem();
        this.CreateClient();
        this.PopulateExistingCharacterList().then(() => {
            this.CreateCommonKnowledge().then(() => {
                this.IsVillagerTemplateAvailable();
            })
        })
    }

    CheckAndSetSaveVersion(saveId){
        if(db.data?.saveInformation?.id){
            return (saveId == db.data.saveInformation.id);
        } else {
            db.data = {
                ...db.data,
                saveInformation: {
                    id: saveId
                }
            };
            db.write();
            return true; 
        }
    }

    IsVillagerTemplateAvailable() {
        let isExist = this.IsCharacterExists(VILLAGER_ID);
        if (!isExist) {
            console.log("[LOG] Villager template doesnt exist")
            this.CreateVillagerTemplate();
        } else {
            console.log("[LOG] Villager template exists")
        }
    }

    async CreateVillagerTemplate() {
        await this.CreateANewVillager();
    }

    IsCharacterExists(cuid) {
        for (let k = 0; k < this.characterList.length; k++) {
            let singleCharacter = this.characterList[k];
            for (let iter = 0; iter < singleCharacter.facts.length; iter++) {
                if (singleCharacter.facts[iter].uuid == cuid) {
                    return true;
                }
            }
        }
        return false;
    }

    GetInternalIdIfUUID(id) {
        console.log("Connect request to id " + id)
        if (id == VILLAGER_ID) {
            for (let i = 0; i < this.characterList.length; i++) {
                let characterObject = this.characterList[i];
                if (characterObject.facts[0]) {
                    for (let iter = 0; iter < characterObject.facts.length; iter++) {
                        if (characterObject.facts[iter].uuid == id) {
                            let internalId = characterObject.name.replace("workspaces/" + WORKSPACE_NAME + "/characters/", "");
                            console.log("[LOG] This is a villager id. We will use " + internalId)
                            return internalId;
                        }
                    }
                }
            }
        }

        var pattern = new RegExp('^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$', 'i');
        if (pattern.test(id)) {
            for (let i = 0; i < this.characterList.length; i++) {
                let characterObject = this.characterList[i];
                if (characterObject.facts[0]) {
                    for (let iter = 0; iter < characterObject.facts.length; iter++) {
                        if (characterObject.facts[iter].uuid == id) {
                            let internalId = characterObject.name.replace("workspaces/" + WORKSPACE_NAME + "/characters/", "");
                            return internalId;
                        }
                    }
                }
            }
        } else {
            return id;
        }
    }

    ConnectToCharacter(characterId, playerName, gameDate, callback) {
        this.requestedCharacterIdentifier = characterId;
        this.conversationDate = gameDate;
        let id = this.GetInternalIdIfUUID(characterId);
        console.log("[LOG] Connecting to " + id);
        let scene = "workspaces/" + WORKSPACE_NAME + "/characters/{CHARACTER_NAME}".replace("{CHARACTER_NAME}", id);
        this.client.setUser({
            fullName: playerName
        })
        this.client.setScene(scene);
        this.client.setOnMessage(callback);
        this.connection = this.client.build();
        return this.connection;
    }

    SendMessageToCharacter(message) {
        message = this.languageFilter.CleanInput(message);
        this.connection.sendText(message)
    }

    TerminateConnection() {
        this.connection.close();
    }

    async PopulateExistingCharacterList() {
        let token = await this.GetToken();
        let headerConfig = {
            'authorization': 'Bearer ' + token,
            'content-type': 'text/plain;charset=UTF-8',
            'origin': 'https://studio.inworld.ai',
            'referer': "https://studio.inworld.ai/workspaces/" + WORKSPACE_NAME + "/characters",
            'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
        }
        let response = await axios.get(GET_CHARACTERS, {
            headers: headerConfig
        });
        this.characterList = response.data.characters;
    }

    async CreateCommonKnowledge() {
        let commonknowledgeList = await this.GetAllCurrentCommonKnowledges();
        let expectedList = COMMON_KNOWLEDGE.list;
        for (let i = 0; i < expectedList.length; i++) {
            let singleKnowledge = expectedList[i];
            let filtered = commonknowledgeList.commonKnowledges.filter(el => el.displayName == singleKnowledge.displayName);
            if (filtered.length == 0) {
                console.log("Creating a new common knowledge");
                await this.AddCommonKnowledge(singleKnowledge);
            }
        }
        this.entireCommonKnowledge = await this.GetAllCurrentCommonKnowledges();
        console.log("All common knowledge has been processed.");
        return;
    }

    async GetAllCurrentCommonKnowledges() {
        let token = await this.GetToken();
        let headerConfig = {
            'authorization': 'Bearer ' + token,
            'content-type': 'text/plain;charset=UTF-8',
            'origin': 'https://studio.inworld.ai',
            'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
            'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
        }
        let response = await axios.get(COMMON_KNOWLEDGE_URI, {
            headers: headerConfig
        });
        return response.data;
    }

    async AddCommonKnowledge(commonKnowledge) {
        let token = await this.GetToken();
        let normalizedUri = COMMON_KNOWLEDGE_URI.replace("?pageSize=500", "");
        let headerConfig = {
            'authorization': 'Bearer ' + token,
            'content-type': 'text/plain;charset=UTF-8',
            'origin': 'https://studio.inworld.ai',
            'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
            'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
        }
        await axios.post(normalizedUri, commonKnowledge, {
            headers: headerConfig
        });
    }

    async UpdateCommonKnowledge(type, information) {
        try {
            let knowledgeObject = this.GetTypeKnowledge(type);
            information = this.languageFilter.CleanInput(information);
            console.log("[LOG] Old ", knowledgeObject)
            if (type == "war" || type == "scene") {
                // rewrote all
                knowledgeObject.memoryRecords = information;
            } else {
                // preserve previous records and delete first if exceeds 1000
                let records = knowledgeObject.memoryRecords;
                if (records.length >= 1000) {
                    records.shift();
                }
                records.push(information);
                knowledgeObject.memoryRecords = records;
            }
            let updateLocation = knowledgeObject.name + "";
            if (updateLocation == undefined || updateLocation == "" || updateLocation == "undefined" || !updateLocation) {
                console.error("Cannot update information " + updateLocation + " with type " + type);
                return;
            }
            console.log("Updating", knowledgeObject.name);
            let uriToUpdate = "https://studio.inworld.ai/v1alpha/" + updateLocation;
            let token = await this.GetToken();
            let headerConfig = {
                'authorization': 'Bearer ' + token,
                'content-type': 'text/plain;charset=UTF-8',
                'origin': 'https://studio.inworld.ai',
                'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/knowledge',
                'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
            }
            let patched = await axios.patch(uriToUpdate, knowledgeObject, {
                headers: headerConfig
            }).catch(err => {
                knowledgeObject.name = updateLocation;
                if (err.response) {
                    if (err.status == 404) {
                        return null;
                    } else {
                        throw err;
                    }
                }
            });
            knowledgeObject.name = updateLocation;
            if (patched == null) return false;
            this.UpdateLocalRecords(patched.data);
            return true;
        } catch (ex) {
            console.fatalError(ex)
            return false;
        }
    }

    UpdateLocalRecords(data) {
        for (let i = 0; i < this.entireCommonKnowledge.commonKnowledges.length; i++) {
            let ob = this.entireCommonKnowledge.commonKnowledges[i]
            if (ob.name == data.name) {
                this.entireCommonKnowledge.commonKnowledges[i] = data;
            }
        }
    }

    GetTypeKnowledge(type) {
        let search = "Ongoing Wars"
        if (type == "event") {
            search = "Events Knowledge"
        } else if (type == "player") {
            search = "Player "
        } else if (type == "war") {
            search = "Ongoing Wars"
        } else if (type == "scene") {
            search = "Current Environment"
        } else {
            throw "Unexpected type";
        }

        let filtered = this.entireCommonKnowledge.commonKnowledges.filter(
            knowledge => {
                if (knowledge.displayName.toLowerCase().includes(search.toLowerCase()))
                    return knowledge;
            }
        )

        if (filtered.length == 0)
            throw "Hmm.."
        return filtered[0];
    }

    CreateClient() {
        this.client = new InworldClient();
        this.client.setApiKey({
            key: process.env.INWORLD_KEY,
            secret: process.env.INWORLD_SECRET,
        });
        this.SetConfiguration();
        this.client.setGenerateSessionToken(this.GenerateSessionTokenOverride.bind(this));
    }

    // We will not use this because eh... why should we lose a data am I right?
    CompareTwoCalradianDates(dateStr1, dateStr2) {
        try {
            const daysInYear = 4 * 21;
            const [day1, month1, year1] = dateStr1.split("-").map(Number);
            const [day2, month2, year2] = dateStr2.split("-").map(Number);
            const daysSinceYear0_1 = (year1 * daysInYear) + ((month1 - 1) * 21) + (day1 - 1);
            const daysSinceYear0_2 = (year2 * daysInYear) + ((month2 - 1) * 21) + (day2 - 1);
            return Math.abs(daysSinceYear0_2 - daysSinceYear0_1);
        } catch {
            return 10000;
        }
    }

    async GenerateSessionTokenOverride() {
        db.read()
        db.data ||= { conversations: [], saveInformation: {} }
        const {
            conversations
        } = db.data
        const userData = conversations.find((el) => el.cid === this.requestedCharacterIdentifier);
        this.SetConfiguration();
        const token = await this.client.generateSessionToken();
        const actualToken = new SessionToken({
            expirationTime: token.getExpirationTime(),
            token: token.getToken(),
            type: token.getType(),
            sessionId: userData ? userData.session : token.getSessionId(),
        });
        if (!userData && VILLAGER_ID != this.requestedCharacterIdentifier) {
            conversations.push({
                cid: this.requestedCharacterIdentifier,
                session: actualToken.getSessionId()
            });

            db.data = {
                ...db.data,
                conversations
            };
            db.write();
        }
        return actualToken;
    }

    SetConfiguration() {
        this.client.setConfiguration({
            connection: defaultConfigurationConnection,
            capabilities: this.currentCapabilities
        });
    }

    async GetToken(isForce = false) {
        let logintoken = isForce ? await this.loginHelper.RefreshToken() : await this.loginHelper.GetPortalToken();
        logintoken = logintoken.replace(/(\r\n|\n|\r)/gm, "");
        return logintoken;
    }

    async GetCharacterData(char_id) {
        let token = await this.GetToken();
        let headerConfig = {
            'authorization': 'Bearer ' + token,
            'content-type': 'application/json',
            'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36',
        }
        let uri = GET_CHAR_URI.replace("{id}", char_id)
        let response = await axios.get(uri, {
            headers: headerConfig
        }).catch(err => {
            if(err.response.code == 5)
                throw "Cannot find character " + char_id;
            else 
                throw err;
        })
        return response.data;
    }

    GetCharacterCachedData(char_id) {
        for (let k = 0; k < this.characterList.length; k++) {
            let singleCharacter = this.characterList[k];
            for (let iter = 0; iter < singleCharacter.facts.length; iter++) {
                if (singleCharacter.facts[iter].uuid == char_id) {
                    return singleCharacter;
                }
            }
        }
        return null;
    }

    async CheckCharacters(charArray) {
        let missingChars = []
        for (let i = 0; i < charArray.length; i++) {
            try {
                for (let k = 0; k < this.characterList.length; k++) {
                    let singleCharacter = this.characterList[k];
                    let found = false;
                    for (let iter = 0; iter < singleCharacter.facts.length; iter++) {
                        if (characterObject.facts[iter].uuid == charArray[i]) {
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                missingChars.push(charArray[i]);
            } catch {}
        }
        return missingChars;
    }

    async RefreshEverything() {
        await this.DeleteEverything();
        await this.CreateCommonKnowledge();
        await this.CreateVillagerTemplate();
        return "Everything will be refreshed soon.";
    }

    async DeleteEverything() {

        for (let j = 0; j < this.characterList.length; j++) {
            let character = this.characterList[j];
            await this.DeleteCharacterInternal(character);
        }

        for (let i = 0; i < this.entireCommonKnowledge.commonKnowledges.length; i++) {
            let commonKnowledge = this.entireCommonKnowledge.commonKnowledges[i];
            await this.DeleteKnowledgeInternal(commonKnowledge);
        }

        this.entireCommonKnowledge.commonKnowledges = [];
        this.characterList = [];

        db.data = {
            conversations: [],
            saveInformation: {}
        };
        db.write();

        return "Everything will be deleted soon.";
    }

    async DeleteKnowledgeInternal(commonKnowledge) {
        if (commonKnowledge) {
            let token = await this.GetToken();
            let headerConfig = {
                'authorization': 'Bearer ' + token,
                'content-type': 'text/plain;charset=UTF-8',
                'origin': 'https://studio.inworld.ai',
                'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
                'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
            }
            await axios.delete("https://studio.inworld.ai/v1alpha/" + commonKnowledge.name, {
                headers: headerConfig
            });
            return {
                "isSuccess": true,
                "message": "Updated"
            };
        } else {
            return {
                "isSuccess": false,
                "message": "Given id does not exist"
            };
        }
    }

    async DeleteCharacterInternal(existingData) {
        if (existingData) {
            let token = await this.GetToken();
            let headerConfig = {
                'authorization': 'Bearer ' + token,
                'content-type': 'text/plain;charset=UTF-8',
                'origin': 'https://studio.inworld.ai',
                'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
                'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
            }
            await axios.delete("https://studio.inworld.ai/v1alpha/" + existingData.name, {
                headers: headerConfig
            });
            return {
                "isSuccess": true,
                "message": "Updated"
            };
        } else {
            return {
                "isSuccess": false,
                "message": "Given id does not exist"
            };
        }
    }

    async DeleteCharacter(id) {
        try {
            let existingData = this.GetCharacterCachedData(id);
            return await this.DeleteCharacterInternal(existingData)
        } catch (e) {
            console.fatalError(e);
            return {
                "isSuccess": false,
                "message": "Somethign happened, check error log."
            };
        }
    }

    GetFactLocation(existingData, type) {
        for (let k = 0; k < existingData.facts.length; k++) {
            if (existingData.facts[k].uuid.includes(type)) {
                return k;
            }
        }
        return -1;
    }

    async UpdateCharacter(id, name, description, pronoun, motivation, facts, temporaryFacts,
        exampleDialog, exampleDialogStyle, lifeStage, hobbyOrInterests, characterRole, personality, initialMood, voice, changeVoice, gender, age) {
        let existingData = this.GetCharacterCachedData(id);

        if (existingData == null) {
            return {
                "isSuccess": false,
                "reason": "Doesnt exist"
            };
        }

        console.log("Update requested for " + id, existingData.defaultCharacterDescription.givenName)

        name = this.languageFilter.CleanInput(name);
        description = this.languageFilter.CleanInput(description);
        facts = this.languageFilter.CleanInput(facts);
        temporaryFacts = this.languageFilter.CleanInput(temporaryFacts);

        if (name) {
            existingData.defaultCharacterDescription.givenName = name;
        }

        if (description) {
            existingData.defaultCharacterDescription.description = description;
        }

        if (facts) {
            let loc = this.GetFactLocation(existingData, id)
            existingData.facts[loc].text = facts
        }

        try {
            if (temporaryFacts) {
                let loc = this.GetFactLocation(existingData, "temporary-scene-fact");

                if (loc == -1) {
                    existingData.facts.push({
                        "uuid": "temporary-scene-fact",
                        "text": temporaryFacts,
                        "tags": [
                            "TAG_MEMORY"
                        ]
                    })
                } else {
                    existingData.facts[loc] = {
                        "uuid": "temporary-scene-fact",
                        "text": temporaryFacts,
                        "tags": [
                            "TAG_MEMORY"
                        ]
                    }
                }

            } else {
                let loc = this.GetFactLocation(existingData, id);
                if (loc == -1) {
                    existingData.facts.push({
                        "uuid": "temporary-scene-fact",
                        "text": [""],
                        "tags": [
                            "TAG_MEMORY"
                        ]
                    })
                } else {
                    existingData.facts[loc] = {
                        "uuid": "temporary-scene-fact",
                        "text": [""],
                        "tags": [
                            "TAG_MEMORY"
                        ]
                    }
                }

            }
        } catch (e) {
            console.log("Error happened during temporary update", e)
        }

        if (pronoun) {
            existingData.defaultCharacterDescription.pronoun = pronoun;
        }

        if (motivation) {
            existingData.defaultCharacterDescription.motivation = motivation;
        }

        if (exampleDialog) {
            existingData.defaultCharacterDescription.exampleDialog = exampleDialog;
        }

        if (exampleDialogStyle) {
            existingData.defaultCharacterDescription.exampleDialogStyle = exampleDialogStyle;
        }

        if (lifeStage) {
            existingData.defaultCharacterDescription.lifeStage = lifeStage;
        }

        if (hobbyOrInterests && Array.isArray(hobbyOrInterests)) {
            existingData.defaultCharacterDescription.hobbyOrInterests = hobbyOrInterests;
        }

        if (characterRole) {
            existingData.defaultCharacterDescription.characterRole = characterRole;
        }

        if (personality) {
            existingData.personality = personality;
        }

        if (initialMood) {
            existingData.initialMood = initialMood;
        }

        if (voice) {
            existingData.defaultCharacterAssets.voice = voice;
        }

        if (changeVoice) {
            existingData.defaultCharacterAssets.voice.gender = "VOICE_GENDER_" + (gender == "female" ? "FEMALE" : "MALE");
            let randomVoice = gender == "female" ? FEMALE_VOICES.random() : MALE_VOICES.random();
            existingData.defaultCharacterAssets.voice.baseName = randomVoice.name;
        }

        if (age) {
            let pitch = 0;
            if (age < 18) pitch = getRandomFloat(2, 5)
            else pitch = getRandomFloat(-2, 0.5)
            existingData.defaultCharacterAssets.voice.pitch = pitch;
            existingData.defaultCharacterAssets.voice.speakingRate = getRandomFloat(0.5, 1);
        }

        try {
            let token = await this.GetToken();
            let headerConfig = {
                'authorization': 'Bearer ' + token,
                'content-type': 'text/plain;charset=UTF-8',
                'origin': 'https://studio.inworld.ai',
                'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
                'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
            }
            console.log("[LOG] UPDATING ", existingData);
            let response = await axios.patch("https://studio.inworld.ai/v1alpha/" + existingData.name, JSON.stringify(existingData), {
                headers: headerConfig
            });

            if (response.data) {
                let updatedData = response.data;
                for (let k = 0; k < this.characterList.length; k++) {
                    let singleCharacter = this.characterList[k];
                    if (singleCharacter.name == updatedData.name) {
                        this.characterList[k] = updatedData;
                        break;
                    }
                }
            }
            return {
                "isSuccess": true
            };
        } catch (ex) {
            console.log(ex);
            return {
                "isSuccess": false,
                "reason": "Something happened"
            };
        }
    }

    async CreateANewVillager(trial = 0) {

        trial++;

        if(trial > 2) return;

        let NEW_CHAR = JSON.parse(JSON.stringify(VILLAGER_TEMPLATE));
        NEW_CHAR.defaultCharacterAssets.voice.gender = "VOICE_GENDER_MALE";
        let randomVoice = MALE_VOICES.random();
        NEW_CHAR.defaultCharacterAssets.voice.baseName = randomVoice.name;
        let pitch = getRandomFloat(-2, 0.5)
        NEW_CHAR.defaultCharacterAssets.voice.pitch = pitch;
        NEW_CHAR.defaultCharacterAssets.voice.speakingRate = getRandomFloat(0.5, 1.5);
        this.entireCommonKnowledge.commonKnowledges.forEach(knowledge => {
            NEW_CHAR.commonKnowledges.push(knowledge.name)
        });
        
        let token = await this.GetToken();
        try {
            let headerConfig = {
                'authorization': 'Bearer ' + token,
                'content-type': 'text/plain;charset=UTF-8',
                'origin': 'https://studio.inworld.ai',
                'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
                'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
            }
            console.log("[LOG] Creating villager")
            NEW_CHAR = this.languageFilter.CleanInput(NEW_CHAR);
            let response = await axios.post(CREATE_URI, JSON.stringify(NEW_CHAR), {
                headers: headerConfig
            }).catch(async (err) => {
                if (err.response.data.message == "unauthenticated") {
                    token = await this.GetToken(true);
                    return await this.CreateANewVillager(trial);
                } else {
                    throw err;
                }
            });
            let recallUrl = response.data.name
            let character_id = recallUrl.split("/")[3]
            let fetchUri = "https://studio.inworld.ai/v1alpha/" + recallUrl;
            let isFinished = false;
            const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
            while (!isFinished) {
                await delay(5000);
                let checkResult = await axios.get(fetchUri, {
                    headers: headerConfig
                }).catch(async (err) => {
                    if (err.response.data.message == "unauthenticated") {
                        token = await this.GetToken(true);
                        return await this.CreateANewVillager(trial);
                    } else {
                        if(err.response.code != 5)
                            throw err;
                    }
                });
                isFinished = checkResult.data.done;
            }
            let newCharacter = await this.GetCharacterData(character_id);
            this.characterList.push(newCharacter);
            return newCharacter;
        } catch (error) {
            console.fatalError(error);
            return {
                "failed": true,
                "error": error
            };
        }
    }

    DoesCharacterExist(cuid) {
        for (let i = 0; i < this.characterList.length; i++) {
            let characterObject = this.characterList[i];
            if (characterObject.facts[0]) {
                for (let k = 0; k < characterObject.facts.length; k++) {
                    if (characterObject.facts[k].uuid == cuid) {
                        return characterObject;
                    }
                }
            }
        }
        return null;
    }

    async CreateANewCharacter(cuid, name, description, isFemale, age, facts, personality, trial = 0) {
        trial++;
        if(trial > 2) { 
            return {
                "failed": true,
                "error": error
            };
        }

        let existingChar = this.DoesCharacterExist(cuid);
        if (existingChar != null) {
            console.log("Character exists", existingChar)
            return existingChar;
        }

        console.log("Creating character", cuid, name, description, isFemale, age, facts, personality, trial)
        let NEW_CHAR = JSON.parse(JSON.stringify(CHAR_TEMPLATE));
        NEW_CHAR.defaultCharacterDescription.givenName = name;
        NEW_CHAR.defaultCharacterDescription.description = description;
        NEW_CHAR.defaultCharacterAssets.voice.gender = "VOICE_GENDER_" + (isFemale ? "FEMALE" : "MALE");
        let randomVoice = isFemale ? FEMALE_VOICES.random() : MALE_VOICES.random();
        NEW_CHAR.defaultCharacterAssets.voice.baseName = randomVoice.name;
        let pitch = 0;
        if (age < 18) pitch = getRandomFloat(2, 5)
        else pitch = getRandomFloat(-1, 0.5)
        NEW_CHAR.defaultCharacterAssets.voice.pitch = pitch;
        NEW_CHAR.defaultCharacterAssets.voice.speakingRate = getRandomFloat(0.5, 1);
        NEW_CHAR.facts.push({
            "uuid": cuid,
            "text": facts,
            "tags": [
                "TAG_MEMORY"
            ]
        });

        NEW_CHAR.facts.push({
            "uuid": "temporary-scene-fact",
            "text": [],
            "tags": [
                "TAG_MEMORY"
            ]
        });

        NEW_CHAR.personality = personality;

        this.entireCommonKnowledge.commonKnowledges.forEach(knowledge => {
            NEW_CHAR.commonKnowledges.push(knowledge.name)
        })

        let token = await this.GetToken();
        try {
            let headerConfig = {
                'authorization': 'Bearer ' + token,
                'content-type': 'text/plain;charset=UTF-8',
                'origin': 'https://studio.inworld.ai',
                'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
                'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
            }
            NEW_CHAR = this.languageFilter.CleanInput(NEW_CHAR);
            let response = await axios.post(CREATE_URI, JSON.stringify(NEW_CHAR), {
                headers: headerConfig
            }).catch(async err => {
                if (err.response.data.message == "unauthenticated") {
                    token = await this.GetToken(true);
                    return await this.CreateANewCharacter(cuid, name, description, isFemale, age, facts, personality, trial);
                } else {
                    console.fatalError("Error while creating character");
                    throw { "error": "Error while creating character. DATA: ", data: NEW_CHAR };
                }
            });
            let recallUrl = response.data.name
            let character_id = recallUrl.split("/")[3]
            let fetchUri = "https://studio.inworld.ai/v1alpha/" + recallUrl;
            let isFinished = false
            const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
            while (!isFinished) {
                await delay(3000);
                let checkResult = await axios.get(fetchUri, {
                    headers: headerConfig
                }).catch(async (err) => {
                    if (err.response.data.message == "unauthenticated") {
                        token = await this.GetToken();
                        headerConfig = {
                            'authorization': 'Bearer ' + token,
                            'content-type': 'text/plain;charset=UTF-8',
                            'origin': 'https://studio.inworld.ai',
                            'referer': 'https://studio.inworld.ai/workspaces/' + WORKSPACE_NAME + '/characters',
                            'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36'
                        }
                    } 
                });
                isFinished = checkResult?.data.done
                if (isFinished) {
                    console.log("[LOG]", checkResult, checkResult.data);
                }
            }
            let newCharacter = await this.GetCharacterData(character_id);
            this.characterList.push(newCharacter);
            await this.UpdateCharacter(cuid, name, description, null, null, null, [], null, null, null, null, null, null, null, NEW_CHAR.defaultCharacterAssets.voice, false, isFemale? "female": "male", age);
            
            return newCharacter;
        } catch (error) {
            console.fatalError(error);
            return {
                "failed": true,
                "error": error
            };
        }
    }
}