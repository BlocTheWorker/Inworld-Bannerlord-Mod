using Inworld.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using static Inworld.Helper.InCampaignHelper;

namespace Inworld.Behavior
{
    public class InworldSaveDefiner : SaveableTypeDefiner
    {
        public InworldSaveDefiner() : base( typeof(InworldSaveDefiner).GetHashCode() )
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(MemorableEvent), 1);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<Hero, string>));
            ConstructContainerDefinition(typeof(List<MemorableEvent>));
        }
    }

    internal class MemorableEvent
    {
        // 0: Battle, 1: 
        [SaveableField(1)]
        public int eventType;
        [SaveableField(2)]
        public string textData;
        [SaveableField(3)]
        public Hero actorHero;
        [SaveableField(4)]
        public Hero affectedHero;
        [SaveableField(5)]
        public CampaignTime date;
    }

    internal class InworldStateCampaignBehavior : CampaignBehaviorBase
    {
        //This is not important at the moment but might be hyper important in the future!
        internal readonly string MOD_VERSION = "1.0.0";

        [SaveableField(1)]
        private bool _inworldInitialized;
        [SaveableField(2)]
        private Dictionary<Hero, string> _heroLog;
        [SaveableField(3)]
        private List<MemorableEvent> _memorableEvents;
        [SaveableField(4)]
        private string _modver;
        [SaveableField(5)]
        private string _modIdentifier;

        private bool _isVersionChecked = false;

        public InworldStateCampaignBehavior()
        {
            _memorableEvents = new List<MemorableEvent>();
            _heroLog = new Dictionary<Hero, string>();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
            CampaignEvents.MakePeace.AddNonSerializedListener(this, new Action<IFaction, IFaction, MakePeaceAction.MakePeaceDetail>(this.OnPeaceDeclared));
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, new Action<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>(this.OnWarDeclared));
            CampaignEvents.OnClanLeaderChangedEvent.AddNonSerializedListener(this, new Action<Hero, Hero>(this.OnClanLeaderChanged));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, new Action<MapEvent>(this.OnMapEventEnd));
            CampaignEvents.BeforeMissionOpenedEvent.AddNonSerializedListener(this, new Action(this.OnBeforeMission));
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, new Action<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>(this.OnHeroKilled));
            CampaignEvents.ClanChangedKingdom.AddNonSerializedListener(this, new Action<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>(this.OnClanChangedKingdom));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnLoaded));
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnLoaded));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(this.HourlyCheck));
        }

        private void HourlyCheck()
        {
            if (!_isVersionChecked) VerCheck();
        }

        private void OnLoaded(CampaignGameStarter obj)
        {
            if (!_inworldInitialized)
            {
                UpdateClanWarKnowledge();
                _modver = MOD_VERSION;
                _inworldInitialized = true;
            }
        }

        // Check every day to remove very old events that are no longer remembered
        private void DailyTick()
        {
           
            if (_memorableEvents == null) return;
            var memorableArr = _memorableEvents.ToArray();
            for (int i =0; i < memorableArr.Length; i++) {
                MemorableEvent ev = memorableArr[i];
                if(ev.date.ElapsedDaysUntilNow > 45) {
                    _memorableEvents.Remove(ev);
                }
            }
        }


        public List<string> GetRecentBattlesToTalkAbout(Hero hero)
        {
            List<string> strList = new List<string>();
            if (_memorableEvents == null) return strList;
            foreach (var ev in _memorableEvents)
            {
                if(ev.actorHero.Clan == hero.Clan || ev.affectedHero.Clan == hero.Clan)
                {
                    string data = ev.textData;
                    data += ". Around " + ev.date.ElapsedDaysUntilNow + " days ago";
                    strList.Add(data);
                }
            }
            return strList;
        }

        public List<string> GetRelatedDeathKnowledge(Hero hero)
        {
            List<string> strList = new List<string>();

            if (_heroLog == null) _heroLog = new Dictionary<Hero, string>();

            foreach (var pair in _heroLog)
            {
                Hero h = pair.Key;
                if( h.Clan == hero.Clan)
                {
                   strList.Add(pair.Value);
                }
            }

            return strList;
        }

        private void OnHeroKilled(
          Hero victim,
          Hero killer,
          KillCharacterAction.KillCharacterActionDetail detail,
          bool showNotification)
        {
            try
            {
                if(victim != null && killer == null && detail == KillCharacterAction.KillCharacterActionDetail.DiedInBattle)
                {
                    string message = victim.Name.ToString() + " from " + victim.Clan.Name.ToString() + " has died in battle in " + CampaignTime.Now.GetYear;
                    if (!_heroLog.ContainsKey(victim))
                        _heroLog.Add(victim, message);
                    return;
                }

                if (killer == null || killer.Clan == null || killer.Clan != Clan.PlayerClan) return;
                if (_heroLog == null) _heroLog = new Dictionary<Hero, string>();
                string data = null;
                if (detail == KillCharacterAction.KillCharacterActionDetail.Executed)
                {
                    data = victim.Name.ToString() + " from " + victim.Clan.Name.ToString() + ", " + (victim.IsFemale ? "she" : "he") + " has been executed by " + killer.Name.ToString() + " from " + killer.Clan.Name.ToString() + " in " + CampaignTime.Now.GetYear;
                    if (!_heroLog.ContainsKey(victim))
                        _heroLog.Add(victim, data);
                }
                else if (detail == KillCharacterAction.KillCharacterActionDetail.DiedInBattle)
                {
                    data = victim.Name.ToString() + " from " + victim.Clan.Name.ToString() + ", " + (victim.IsFemale ? "she" : "he") + " has been killed in battle by " + killer.Name.ToString() + " from " + killer.Clan.Name.ToString() + " in " + CampaignTime.Now.GetYear;
                    if (!_heroLog.ContainsKey(victim))
                        _heroLog.Add(victim, data);
                }
            } catch
            {

            }
        }

        private void OnBeforeMission()
        {
            List<string> stringList = new List<string>();

            string dateAndStuff = "Today is day " + CampaignTime.Now.GetDayOfSeason + " of season " + GameTexts.FindText("str_season_" + (object)CampaignTime.Now.GetSeasonOfYear) + " and year is " + CampaignTime.Now.GetYear + " in Calradia. Effects of season is very noticeable.";
            stringList.Add(dateAndStuff);

            if (PlayerEncounter.EncounterSettlement != null)
            {
                string locationInformation = "{Character} is currently in {LOCATION} which is owned by {CLAN} clan. Owner and ruler of {LOCATION} is {RULER}. {LOCATION} is a {LOCTYPE}";

                locationInformation = locationInformation.Replace("{LOCATION}", PlayerEncounter.EncounterSettlement.Name.ToString());
                locationInformation = locationInformation.Replace("{CLAN}", PlayerEncounter.EncounterSettlement.OwnerClan.Name.ToString());
                locationInformation = locationInformation.Replace("{RULER}", PlayerEncounter.EncounterSettlement.Owner.Name.ToString());
                locationInformation = locationInformation.Replace("{LOCTYPE}", PlayerEncounter.EncounterSettlement.IsTown ? "town" : "village");
                
                stringList.Add(locationInformation);

                var partiesAroundList = InCampaignHelper.GetMobilePartiesAroundPosition(PlayerEncounter.EncounterSettlement.GetPosition2D, MobileParty.MainParty.SeeingRange * 2);
                if (partiesAroundList.Count() > 0)
                {
                    int split = 1;
                    List<string> parties = new List<string>();
                    string partiesAround = "Parties around this location are: ";
                    foreach (var party in partiesAroundList)
                    {
                        if (party.IsGarrison || party.IsMilitia || party.IsMainParty) continue;
                        partiesAround += party.Name.ToString() + " with around " + party.MemberRoster.TotalHealthyCount + " warriors, ";
                        split++;
                        if (split % 3 == 0)
                        {
                            split = 1;
                            parties.Add(partiesAround);
                            partiesAround = "Parties around this location are: ";
                        }
                    }

                    stringList.AddRange(parties);
                }
                else
                {
                    stringList.Add("There are no parties or warbands or armies around this location.");
                }
            }
            
            string text = Settlement.CurrentSettlement.EncyclopediaText.ToString();
            string trimmedDescription = TrimSentence(text, 299);
            stringList.Add(trimmedDescription);

            string formattedString = "[" + String.Join(", ", stringList.Select(s => "\"" + s + "\"")) + "]";

            InCampaignHelper.MakeCallToUpdateCommonInformation("scene", formattedString);
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom from, Kingdom to, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool arg5)
        {
            UpdateClanWarKnowledge();
        }

        private void OnMapEventEnd(MapEvent ev)
        {
            if (ev.IsFinished && !ev.DiplomaticallyFinished && ev.Winner != null)
            {
                try
                {
                    var winnerSide = ev.PartiesOnSide(ev.Winner == ev.AttackerSide ? BattleSideEnum.Attacker : BattleSideEnum.Defender);
                    var loserSide = ev.PartiesOnSide(ev.Winner != ev.AttackerSide ? BattleSideEnum.Attacker : BattleSideEnum.Defender);
                    if (ev.IsFieldBattle)
                    {
                        string nearAddition = "";
                        try
                        {
                            nearAddition += " near " + InCampaignHelper.GetSettlementsAroundPosition(ev.Position, 300).First().Name.ToString();
                        }
                        catch { }
                        string battleInformation = "In " + ev.BattleStartTime.GetYear + ", battle won by " + winnerSide.First().Party.Name.ToString() + " against " + loserSide.First().Party.Name.ToString() + nearAddition;
                        InCampaignHelper.MakeCallToUpdateCommonInformation("event", "\"" + battleInformation + "\"");
                    }
                    else if (ev.IsSiegeAssault && !ev.DiplomaticallyFinished)
                    {
                        string siegeInformation = "In " + ev.BattleStartTime.GetYear + ", siege of " + ev.MapEventSettlement.Name.ToString() + " ended with victory by " + winnerSide.First().Party.Name.ToString() + " against " + loserSide.First().Party.Name.ToString();
                        InCampaignHelper.MakeCallToUpdateCommonInformation("event", "\"" + siegeInformation + "\"");
                    }

                    if (_memorableEvents == null) _memorableEvents = new List<MemorableEvent>();

                    Hero winnerHero = ev.GetLeaderParty(ev.Winner == ev.AttackerSide ? BattleSideEnum.Attacker : BattleSideEnum.Defender).LeaderHero;
                    Hero defeatedHero = ev.GetLeaderParty(ev.Winner == ev.AttackerSide ? BattleSideEnum.Defender : BattleSideEnum.Attacker).LeaderHero;
                    if (winnerHero != null && defeatedHero != null)
                    {
                        try
                        {
                            MemorableEvent memorableEvent = new MemorableEvent();
                            memorableEvent.actorHero = winnerHero;
                            memorableEvent.affectedHero = defeatedHero;
                            memorableEvent.eventType = 0;
                            memorableEvent.date = CampaignTime.Now;
                            memorableEvent.textData = "{Character} knows that " + winnerHero.Name.ToString() + " from " + winnerHero.Clan.Name.ToString() + " defeated " + defeatedHero.Name.ToString() + " in battle in " + memorableEvent.date.GetYear;
                            _memorableEvents.Add(memorableEvent);
                        } catch {

                        }
                    }
                } catch
                {

                }
            }
        }

        private void OnClanLeaderChanged(Hero h1, Hero h2)
        {
            string clanchangeinfo = "In " + CampaignTime.Now.GetYear + ", " + h1.Name.ToString() + " died and " + h2.Name.ToString() + " become leader of clan " + h2.Clan.Name.ToString();
            InCampaignHelper.MakeCallToUpdateCommonInformation("event", "\"" + clanchangeinfo + "\"");
        }

        private void OnPeaceDeclared(IFaction fac1, IFaction fac2, MakePeaceAction.MakePeaceDetail detail)
        {
            UpdateClanWarKnowledge();
        }

        private void OnWarDeclared(IFaction fac1, IFaction fact2, DeclareWarAction.DeclareWarDetail detail)
        {
            UpdateClanWarKnowledge();
        }

        private void UpdateClanWarKnowledge()
        {
            string payload = this.GetWarAndClanInformation();
            InCampaignHelper.MakeCallToUpdateCommonInformation("war", payload);
        }

        private string GetWarAndClanInformation()
        {
            List<string> clanInformationList = new List<string>();
            List<WarPeaceState> warStates = new List<WarPeaceState>();
            foreach(Kingdom k in Campaign.Current.Kingdoms)
            {
                string clanInformation = "";
                foreach(var stance in k.Stances)
                {
                    WarPeaceState wps = new WarPeaceState()
                    {
                        kingdom = stance.Faction1,
                        kingdom2 = stance.Faction2,
                        IsWar = stance.IsAtWar,
                        StartTime = stance.WarStartDate
                    };

                    if (!warStates.Contains(wps)) {
                        warStates.Add(wps);
                    }
                }
                clanInformation += k.Name.ToString() + " has following clans under its rule: ";
                foreach (Clan c in k.Clans)
                {
                    clanInformation += c.Name.ToString() + ",";
                }
                clanInformationList.Add(clanInformation);
            }

            List<string> entries = new List<string>();
            foreach(WarPeaceState wps in warStates)
            {
                string entry = wps.kingdom.Name.ToString() + " and " + wps.kingdom2.Name.ToString() + " are at " + (wps.IsWar? "war" : "peace");
                entries.Add(entry);
            }

            List<string> rulerEntries = new List<string>();
            foreach(Kingdom k in Kingdom.All)
            {
                string entry = k.Name.ToString() + " is currently ruled by " + k.Leader.Name.ToString() + " from " + k.Leader.Clan.Name.ToString() + " clan";
                rulerEntries.Add(entry);
            }

            entries = entries.Concat(rulerEntries).ToList();
            entries = entries.Concat(clanInformationList).ToList();
            string merge = String.Join("||", entries);
            merge = merge.Replace("||", "\",\"");
            string finalPayload = "\"" + merge + "\"";
            return "[" + finalPayload + "]";
        }

        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            this.AddConversations(campaignGameStarter);
        }

        private async void VerCheck()
        {
            if (_isVersionChecked) return;
            if (string.IsNullOrEmpty(_modIdentifier))
            {
                _modIdentifier = Guid.NewGuid().ToString();
            }

            var result = await InCampaignHelper.CheckVersion(_modIdentifier);

            if (!result)
            {
                InformationManager.ShowInquiry(new InquiryData("Inworld Version Issue", "It seems like your existing data (character, conversations) are created for different save game. If you dismiss this, it will update AI with different information and cause it to get confused. If you select refresh, we will create everything again from scratch. ", true, true, "Refresh", "Dismiss", async () => 
                {
                    await MakeRefresh("freshStart");
                    await InCampaignHelper.CheckVersion(_modIdentifier);
                } , null), true);
            }
            _isVersionChecked = true;
        }


        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("inworldInitialized", ref _inworldInitialized);
            dataStore.SyncData("inworldMemorableEvents", ref _memorableEvents);
            dataStore.SyncData("inworldHeroLog", ref _heroLog);
            dataStore.SyncData("inworldModVersion", ref _modver);
            dataStore.SyncData("inworldModSaveIdentifier", ref _modIdentifier);
        }

        public void AddConversations(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("direct_question", "hero_main_options", "talking_with_text", "{=}I need to talk with you.", new ConversationSentence.OnConditionDelegate(this.conversation_player_wants_chat_on_condition), (ConversationSentence.OnConsequenceDelegate)(this.conversation_player_wants_chat_on_consequence));
            starter.AddDialogLine("direct_question_reaction_tmp", "talking_with_text", "talking_with_text_2", "{=}Okay..", () => true, (ConversationSentence.OnConsequenceDelegate)null);
            starter.AddPlayerLine("direct_question_exit", "talking_with_text_2", "lord_pretalk", "{=}Never mind.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null);

            starter.AddPlayerLine("town_or_village_direct_question", "town_or_village_children_player_no_rhyme", "town_or_village_talking_with_text_children", "{=}Tell me something..", new ConversationSentence.OnConditionDelegate(this.conversation_player_wants_chat_villager_on_condition), (ConversationSentence.OnConsequenceDelegate)(this.conversation_player_wants_chat_villager_on_consequence));
            starter.AddDialogLine("town_or_village_direct_question_reaction_tmp2", "town_or_village_talking_with_text_children", "town_or_village_talking_with_text_children2", "{=}Okay..", () => true, (ConversationSentence.OnConsequenceDelegate)null);
            starter.AddPlayerLine("town_or_village_direct_question_exit2", "town_or_village_talking_with_text_children2", "close_window", "{=}Never mind.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null);

            starter.AddPlayerLine("town_or_village_direct_question", "town_or_village_player", "town_or_village_talking_with_text", "{=}I want to talk with you.", new ConversationSentence.OnConditionDelegate(this.conversation_player_wants_chat_villager_on_condition), (ConversationSentence.OnConsequenceDelegate)(this.conversation_player_wants_chat_villager_on_consequence));
            starter.AddDialogLine("town_or_village_direct_question_reaction_tmp", "town_or_village_talking_with_text", "town_or_village_talking_with_text_2", "{=}Okay..", () => true, (ConversationSentence.OnConsequenceDelegate)null);
            starter.AddPlayerLine("town_or_village_direct_question_exit", "town_or_village_talking_with_text_2", "close_window", "{=}Never mind.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null);
        }

        private void conversation_player_wants_chat_villager_on_consequence()
        {
            Campaign.Current.ConversationManager.UpdateCurrentSentenceText();
            State.ChatViewSharedState.IsChatRequiresType = true;
            State.ChatViewSharedState.IsVillagerChat = true;
        }

        private bool conversation_player_wants_chat_villager_on_condition()
        {
            if (!State.ChatViewSharedState.IsReadyToChat) return false;
            State.ChatViewSharedState.IsChatRequiresType = false;
            State.ChatViewSharedState.IsVillagerChat = false;
            return true;
        }

        private void conversation_player_wants_chat_on_consequence()
        {
            State.ChatViewSharedState.IsVillagerChat = false;
            State.ChatViewSharedState.IsChatRequiresType = true;
        }

        private bool conversation_player_wants_chat_on_condition()
        {
            if(Campaign.Current.ConversationManager.OneToOneConversationHero != null)
            {
                if (Campaign.Current.ConversationManager.OneToOneConversationHero.IsNotable || Campaign.Current.ConversationManager.OneToOneConversationHero.IsPlayerCompanion)
                    return false;
            }

            State.ChatViewSharedState.IsVillagerChat = false;
            if (!State.ChatViewSharedState.IsReadyToChat) return false;
            State.ChatViewSharedState.IsChatRequiresType = false;
            return true;
        }

        [TaleWorlds.Library.CommandLineFunctionality.CommandLineArgumentFunction("reset_everything", "inworld")]
        public static string RefreshInworld(List<string> strings)
        {
            try
            {
                MakeRefresh("freshStart");
                return "Everything will be regenerated soon";
            }
            catch
            {
                return "Something happened";
            }
        }

        [TaleWorlds.Library.CommandLineFunctionality.CommandLineArgumentFunction("delete_everything", "inworld")]
        public static string DeleteInworld(List<string> strings)
        {
            try
            {
                MakeRefresh("deleteEverything");
                return "Everything will be deleted soon";
            }
            catch
            {
                return "Something happened";
            }
        }

        static async Task MakeRefresh(string endpoint)
        {
            var client = new HttpClient();
            var requestContent = new StringContent("");
            var response = await client.PostAsync("http://127.0.0.1:3000/"+ endpoint, requestContent);
        }
    }
}
