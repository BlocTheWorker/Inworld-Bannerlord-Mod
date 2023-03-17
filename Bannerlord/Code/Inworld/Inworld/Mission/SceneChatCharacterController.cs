using Inworld.Behavior;
using Inworld.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Inworld.InMission
{
    internal class SceneChatCharacterController : MissionLogic
    {
        private readonly string CREATE_URI = "http://127.0.0.1:3000/createNewCharacter";
        private readonly string UPDATE_URI = "http://127.0.0.1:3000/updateCharacter";

        private readonly int WAIT_TIME = 3;
        private float initialChecker;
        private bool initialPass;
        private Dictionary<Hero, Agent> _nonExistingCharacters;
        private Dictionary<Agent, string> _existingCharacters;
        private object lockObject = new object();
        private int initWaitingCharCount = -1;
        private bool isFinishedAll = false;
        private InworldStateCampaignBehavior _campaignBehavior;
        private bool NeedsWait = false;

        public SceneChatCharacterController()
        {
            _nonExistingCharacters = new Dictionary<Hero, Agent>();
            _existingCharacters = new Dictionary<Agent, string>();
            State.ChatViewSharedState.IsReadyToChat = false;
            State.ChatViewSharedState.HaveError = false;

            _campaignBehavior = Campaign.Current.GetCampaignBehavior<InworldStateCampaignBehavior>();
        }

        public override void OnMissionTick(float dt)
        {
            Location location = CampaignMission.Current.Location;
            if (this.Mission.Mode == MissionMode.Tournament || location == null || location.StringId.Contains("arena") || location.StringId.Contains("house_") || location.StringId.Contains("alley")) {
                if (State.ChatViewSharedState.IsChatRequiresType) {
                    State.ChatViewSharedState.IsChatRequiresType = false;
                }
                return;
            }

            if (!initialPass) {
                initialChecker += dt;
                if (initialChecker >= WAIT_TIME) {
                    if (PlayerEncounter.InsideSettlement && location.StringId.Contains("lordshall"))
                    {
                        State.ChatViewSharedState.IsSceneChat = true;
                    } else
                    {
                        State.ChatViewSharedState.IsSceneChat = false;
                    }

                    foreach (Agent a in this.Mission.Agents) {
                        if (!a.IsHuman) continue;
                        if (a.IsHero) {
                            
                            Hero hero = GetHero(a.Character);
                            if(hero == null)
                                continue;
                            if (hero.IsNotable) continue;
                            if (hero.IsPlayerCompanion) continue;
                            if (!hero.IsLord) continue;
                            if (hero == Hero.MainHero) continue;
                            if (!_nonExistingCharacters.ContainsKey(hero))
                            {
                                if (hero == null) continue;
                                if (hero == Hero.MainHero) continue;
                                _nonExistingCharacters.Add(hero, a);
                            }
                        }
                    }
                    initialChecker = WAIT_TIME;
                    initialPass = true;
                }
            }

            if(initialChecker >= WAIT_TIME) {
                if(_nonExistingCharacters.Count > 0)
                {
                    NeedsWait = true;
                    initWaitingCharCount = _nonExistingCharacters.Count;
                    foreach (var character in _nonExistingCharacters)
                    {
                        CreateCharacter(character.Key, character.Value);
                    }
                    _nonExistingCharacters.Clear();
                } else
                {
                    if (!NeedsWait)
                        initWaitingCharCount = 0;
                }
            }

            if (!isFinishedAll) {
                if (initWaitingCharCount == 0) {
                    isFinishedAll = true;
                    State.ChatViewSharedState.IsReadyToChat = true;
                    if (State.ChatViewSharedState.IsSceneChat)
                        InformationManager.DisplayMessage(new InformationMessage("All characters in the scene are ready to chat", Colors.Green));

                    UpdateCharacterData();
                }
            }
        }

        public async void CreateVillagerOrTownsman()
        {
            try
            {
                string payload = CharacterEngine.GetVillagerTownsmanBackground();
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using (var client = new HttpClient())
                {
                    var response = await client.PostAsync(UPDATE_URI, content);
                    if (response.IsSuccessStatusCode)
                    {
                        State.ChatViewSharedState.IsReadyToChat = true;
                        InformationManager.DisplayMessage(new InformationMessage("Character ready to chat", Colors.Green));
                    } else
                    {
                        State.ChatViewSharedState.IsReadyToChat = true;
                        State.ChatViewSharedState.HaveError = true;
                    }
                }
            } catch
            {
                State.ChatViewSharedState.IsReadyToChat = true;
                State.ChatViewSharedState.HaveError = true;
            }
        }

        public bool IsCharacterReady(Agent agent)
        {
            return _existingCharacters.ContainsKey(agent);
        }

        public string GetCharacterId(Agent agent)
        {
            if (!_existingCharacters.ContainsKey(agent)) return "error";
            return _existingCharacters[agent];
        }

        private async Task CreateCharacter(Hero hero, Agent a)
        {
            using (var client = new HttpClient())
            {
                string id = CharacterEngine.GetUniqueId(hero);
                string payload = CharacterPayload(hero, id);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(CREATE_URI, content);
                if (response.IsSuccessStatusCode) {
                    var result = await response.Content.ReadAsStringAsync();
                    lock (lockObject)
                    {
                        initWaitingCharCount--;
                        if(!_existingCharacters.ContainsKey(a))
                            _existingCharacters.Add(a, id);
                    }
                } else
                {
                    //This is failsafe for nonblocking user
                    initWaitingCharCount--;
                }
            }
        }


        private async Task UpdateCharacterData()
        {
            // Update motivations and personality
            var agents = this.Mission.Agents;
            var client = new HttpClient();
            foreach (Agent agent in agents)
            {
                if (agent.IsHuman && agent.IsHero && !agent.IsPlayerControlled)
                {
                    Hero hero = GetHero(agent.Character);

                    if (hero == null)
                        continue;
                    if (hero.IsNotable) continue;
                    if (hero.IsPlayerCompanion) continue;
                    if (!hero.IsLord) continue;
                    if (hero == Hero.MainHero) continue;

                    string id = CharacterEngine.GetUniqueId(hero);
                    string motivation = CharacterEngine.GetMotivationForHero(hero);
                    JObject dataObject = new JObject();
                    int relationWithPlayer = ((int)hero.GetRelationWithPlayer());

                    JObject personalityObject = new JObject();
                    personalityObject.Add("positive", relationWithPlayer < 0 ? relationWithPlayer : 0);
                    personalityObject.Add("peaceful", relationWithPlayer);
                    personalityObject.Add("open", 0);
                    personalityObject.Add("extravert", 0);

                    JObject initialMoodObject = new JObject();
                    initialMoodObject.Add("joy", relationWithPlayer);
                    initialMoodObject.Add("fear", 0);
                    initialMoodObject.Add("trust", relationWithPlayer);
                    initialMoodObject.Add("surprise", 0);

                    JArray temporaryFacts = new JArray(CollectSceneTemporaryInformation(hero, agent));
                    
                    dataObject.Add("temporaryFacts", temporaryFacts);
                    dataObject.Add("personality", personalityObject);
                    dataObject.Add("initialMood", initialMoodObject);
                    dataObject.Add("id", id);
                    dataObject.Add("motivation", motivation);

                    string dataPayload = JsonConvert.SerializeObject(dataObject);
                    var content = new StringContent(dataPayload, Encoding.UTF8, "application/json");
                    await client.PostAsync(UPDATE_URI, content);
                }
            }
        }

        private string[] CollectSceneTemporaryInformation(Hero hero, Agent agent)
        {
            List<string> list = new List<string>();
            try
            {
                if (_campaignBehavior == null)
                {
                    _campaignBehavior = Campaign.Current.GetCampaignBehavior<InworldStateCampaignBehavior>();
                }

                list = _campaignBehavior.GetRelatedDeathKnowledge(hero);

                string currentEnv = "You are currently keep room of " + Settlement.CurrentSettlement.Name + " along with " + GetExcludedHeroNames(hero);
                list.Add(currentEnv);

                int hour = CampaignTime.Now.GetHourOfDay;
                string timeAndStuff = "Currently its " + CharacterEngine.GetTimeToString(hour) + " and a couple capable soldiers are protecting the room.";
                list.Add(timeAndStuff);
                list.Add(CharacterEngine.AppereanceData(hero));
                list.Add(CharacterEngine.AppereanceData(Hero.MainHero));

                if (hero.Father != null)
                {
                    string otherInfo = "{character} has a father, " + hero.Father.Name;
                    if (!hero.Father.IsAlive) otherInfo += " but he is deceased.";
                    else otherInfo += " and he is still alive at age " + (int)hero.Father.Age + ".";
                    list.Add(otherInfo);
                } else
                {
                    list.Add("{Character} doesn't want to talk about its father, it's a mystery for " + (hero.IsFemale? "her" : "him") );
                }

                if (hero.Mother != null)
                {
                    string otherInfo = "{character} has a mother, " + hero.Mother.Name;
                    if (!hero.Mother.IsAlive) otherInfo += " but she is deceased.";
                    else otherInfo += " and she is still alive at age " + (int)hero.Mother.Age + ".";
                    list.Add(otherInfo);
                }
                else
                {
                    list.Add("{Character} doesn't want to talk about its mother, it's a mystery for " + (hero.IsFemale ? "her" : "him"));
                }

                if (hero.Siblings.Count() > 0)
                {
                    string otherInfo = "{character} has siblings ";
                    foreach (Hero sibling in hero.Siblings)
                    {
                        otherInfo += (sibling.IsFemale ? "sister" : "brother") + " " + sibling.Name + ",";
                        if (!sibling.IsAlive) otherInfo += " but " + sibling.Name + " is deceased, ";
                    }
                    list.Add(otherInfo);
                }
                else
                {
                    list.Add("{Character} doesn't have any siblings");
                }

                if (hero.Children.Count() > 0)
                {
                    string otherInfo = "{character} has children ";
                    foreach (Hero child in hero.Children)
                    {
                        otherInfo += (child.IsFemale ? "daughter" : "son") + " " + child.Name + ",";
                        if (!child.IsAlive) otherInfo += " but " + child.Name + " is deceased, ";
                        else otherInfo += " at age around " + ((int)child.Age);
                    }
                    list.Add(otherInfo);
                }
                else
                {
                    list.Add("{Character} doesn't have any children");
                }

                if (hero.Spouse != null)
                {
                    string otherInfo = "{character} is married with ";
                    if (hero.Spouse == Hero.MainHero) otherInfo += "{player}.";
                    else otherInfo += hero.Spouse.Name + " from " + hero.Spouse.Clan.Name;
                    otherInfo.Add(otherInfo);
                }
                else
                {
                    string otherInfo = "{character} is single";
                    list.Add(otherInfo);
                }

                if(hero.Clan != null)
                {
                    string clanInfo = "{Character} is from " + hero.Clan.Name.ToString() + " clan and ruler of clan is " + hero.Clan.Leader.Name.ToString();
                    list.Add(clanInfo);
                }                

                list.AddRange(_campaignBehavior.GetRecentBattlesToTalkAbout(hero));

                var currentAnim = agent.GetCurrentAction(0);

                if (currentAnim.Name.Contains("writer"))
                {
                    list.Add("{Character} was taking notes and reading, before interrupted with conversation");
                } else if (currentAnim.Name.Contains("sit"))
                {
                    list.Add("{Character} was sitting and thingking before interrupted with conversation");
                } else
                {
                    list.Add("{Character} was standing and thinking before interrupted with conversation");
                }

                if(MBRandom.RandomFloat < 0.8f)
                    list.Add("{Character} does not know any poems or tales.");


            } 
            catch
            {
            }
            return list.ToArray();
        }


        internal string GetExcludedHeroNames(Hero hero)
        {
            string everyone = "";
            foreach (Agent a in this.Mission.Agents)
            {
                if(a.IsHuman && a.IsHero)
                {
                    if(a.Name != hero.Name.ToString())
                    {
                        everyone += a.Name + ",";
                    }
                }
            }
            return everyone;
        }

        private string CharacterPayload(Hero hero, string id)
        {
            string description = CharacterEngine.GetBackgroundData(hero);
            string isFemale = hero.IsFemale? "true": "false";
            string[] factsArray = CharacterEngine.GetFacts(hero);
            string facts = "";
            foreach(string fact in factsArray)
            {
                facts += "\"" + fact + "\",";
            }
            facts = facts.Remove(facts.Length - 1, 1);
            string payload = "{\"id\": \"{id}\", \"name\": \"{name}\",\"description\": \"{description}\", \"isFemale\": {isFemale}, \"age\": {age}, \"facts\": [ {facts} ], \"personality\": { \"positive\": {positive}, \"peaceful\": {peaceful}, \"open\": {open}, \"extravert\": {extravert} }}";
            payload = payload.Replace("{id}", id);
            payload = payload.Replace("{name}", hero.Name.ToString());
            payload = payload.Replace("{description}", description);
            payload = payload.Replace("{isFemale}", isFemale);
            payload = payload.Replace("{age}", ((int)hero.Age).ToString());
            payload = payload.Replace("{facts}", facts);
            int heroRelation = CharacterRelationManager.GetHeroRelation(hero, Hero.MainHero);
            payload = payload.Replace("{positive}", heroRelation.ToString());
            payload = payload.Replace("{extravert}", MBRandom.RandomInt(-60, 50).ToString());
            payload = payload.Replace("{open}", MBRandom.RandomInt(-50, 100).ToString());
            payload = payload.Replace("{peaceful}", heroRelation.ToString());
            return payload;
        }

        private Hero GetHero(BasicCharacterObject cobj)
        {
            if (!State.ChatViewSharedState.IsSceneChat)
            {
                Hero herro = Campaign.Current.ConversationManager.OneToOneConversationHero;
                return herro;
            }

            Settlement sett = PlayerEncounter.EncounterSettlement;
            foreach (Hero h in sett.HeroesWithoutParty)
            {
                if(h.CharacterObject.Id == cobj.Id)
                {
                    return h;
                }
            }

            foreach(var p in sett.Parties)
            {
                if(p.LeaderHero != null && p.LeaderHero.CharacterObject.Id == cobj.Id)
                {
                    return p.LeaderHero;
                }
            }

            return null;
        }
    }
}
