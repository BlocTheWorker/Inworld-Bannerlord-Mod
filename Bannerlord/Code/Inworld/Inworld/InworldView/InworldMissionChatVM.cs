using Helpers;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using NAudio.Wave;
using System.IO;
using TaleWorlds.MountAndBlade;
using Inworld.InMission;
using Inworld.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.InputSystem;

namespace Inworld.InworldView
{
    public class InworldMissionChatVM : ViewModel
    {
        private readonly ConversationManager _conversationManager;
        private readonly bool _isLinksDisabled;
        private static bool _isCurrentlyPlayerSpeaking;
        private bool _isProcessingOption;
        private BasicCharacterObject _currentDialogCharacter;
        private Func<string> _getContinueInputText;
        private MBBindingList<ConversationItemVM> _answerList;
        private string _dialogText;
        private string _currentCharacterNameLbl;
        private string _continueText;
        private string _relationText;
        private string _persuasionText;
        private bool _isLoadingOver;
        private string _moreOptionText;
        private string _goldText;
        private ConversationAggressivePartyItemVM _defenderLeader;
        private ConversationAggressivePartyItemVM _attackerLeader;
        private MBBindingList<ConversationAggressivePartyItemVM> _defenderParties;
        private MBBindingList<ConversationAggressivePartyItemVM> _attackerParties;
        private ImageIdentifierVM _conversedHeroBanner;
        private bool _isAggressive;
        private bool _isRelationEnabled;
        private bool _isBannerEnabled;
        private bool _isPersuading;
        private bool _isRequireClick;
        private bool _isRequireType;
        private bool _isCurrentCharacterValidInEncyclopedia;
        private int _selectedSide;
        private int _relation;
        private int _minRelation;
        private int _maxRelation;
        private PowerLevelComparer _powerComparer;
        private ConversationItemVM _currentSelectedAnswer;
        private PersuasionVM _persuasion;
        private HintViewModel _relationHint;
        private HintViewModel _factionHint;
        private HintViewModel _goldHint;
        private WebSocket _webSocket;
        private string _newDialogText; 
        private string _conversationText;
        private bool isConnected;
        public bool SelectedAnOptionOrLinkThisFrame { get; set; }
        private WaveOutEvent _player;
        private List<RawSourceWaveStream> _streamQueue;
        CharacterEngine.EmotionBehavior lastEmotion = CharacterEngine.EmotionBehavior.NEUTRAL;
        private bool updatedVillager;
        private bool IsWaitingToSend;
        private string jsonToSend;
        private bool isFirstEmotionArrived;

        public InworldMissionChatVM(Func<string> getContinueInputText, bool isLinksDisabled = false)
        {
            this.AnswerList = new MBBindingList<ConversationItemVM>();
            this.AttackerParties = new MBBindingList<ConversationAggressivePartyItemVM>();
            this.DefenderParties = new MBBindingList<ConversationAggressivePartyItemVM>();
            this._conversationManager = Campaign.Current.ConversationManager;
            
            this._getContinueInputText = getContinueInputText;
            this._isLinksDisabled = isLinksDisabled;
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener((object)this, new Action<Tuple<PersuasionOptionArgs, PersuasionOptionResult>>(this.OnPersuasionProgress));
            this.Persuasion = new PersuasionVM(this._conversationManager);
            this.IsAggressive = Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && this._conversationManager.ConversationParty != null && FactionManager.IsAtWarAgainstFaction(this._conversationManager.ConversationParty.MapFaction, Hero.MainHero.MapFaction);
            if (this.IsAggressive)
            {
                List<MobileParty> mobilePartyList1 = new List<MobileParty>();
                List<MobileParty> mobilePartyList2 = new List<MobileParty>();
                MobileParty conversationParty = this._conversationManager.ConversationParty;
                MobileParty mainParty = MobileParty.MainParty;
                if (PlayerEncounter.PlayerIsAttacker)
                {
                    mobilePartyList2.Add(mainParty);
                    mobilePartyList1.Add(conversationParty);
                    PlayerEncounter.Current.FindAllNpcPartiesWhoWillJoinEvent(ref mobilePartyList2, ref mobilePartyList1);
                }
                else
                {
                    mobilePartyList2.Add(conversationParty);
                    mobilePartyList1.Add(mainParty);
                    PlayerEncounter.Current.FindAllNpcPartiesWhoWillJoinEvent(ref mobilePartyList1, ref mobilePartyList2);
                }
                this.AttackerLeader = new ConversationAggressivePartyItemVM(PlayerEncounter.PlayerIsAttacker ? mainParty : conversationParty);
                this.DefenderLeader = new ConversationAggressivePartyItemVM(PlayerEncounter.PlayerIsAttacker ? conversationParty : mainParty);
                double num1 = 0.0;
                double num2 = 0.0;
                double defenderPower = num1 + (double)this.DefenderLeader.Party.Party.TotalStrength;
                double attackerPower = num2 + (double)this.AttackerLeader.Party.Party.TotalStrength;
                foreach (MobileParty party in mobilePartyList1)
                {
                    if (party != conversationParty && party != mainParty)
                    {
                        defenderPower += (double)party.Party.TotalStrength;
                        this.DefenderParties.Add(new ConversationAggressivePartyItemVM(party));
                    }
                }
                foreach (MobileParty party in mobilePartyList2)
                {
                    if (party != conversationParty && party != mainParty)
                    {
                        attackerPower += (double)party.Party.TotalStrength;
                        this.AttackerParties.Add(new ConversationAggressivePartyItemVM(party));
                    }
                }
                string defenderColor = this.DefenderLeader.Party.MapFaction == null || !(this.DefenderLeader.Party.MapFaction is Kingdom) ? Color.FromUint(this.DefenderLeader.Party.MapFaction.Banner.GetPrimaryColor()).ToString() : Color.FromUint(((Kingdom)this.DefenderLeader.Party.MapFaction).PrimaryBannerColor).ToString();
                string attackerColor = this.AttackerLeader.Party.MapFaction == null || !(this.AttackerLeader.Party.MapFaction is Kingdom) ? Color.FromUint(this.AttackerLeader.Party.MapFaction.Banner.GetPrimaryColor()).ToString() : Color.FromUint(((Kingdom)this.AttackerLeader.Party.MapFaction).PrimaryBannerColor).ToString();
                this.PowerComparer = new PowerLevelComparer(defenderPower, attackerPower);
                this.PowerComparer.SetColors(defenderColor, attackerColor);
            }
            else
            {
                this.DefenderLeader = new ConversationAggressivePartyItemVM((MobileParty)null);
                this.AttackerLeader = new ConversationAggressivePartyItemVM((MobileParty)null);
            }
            if (this._conversationManager.SpeakerAgent != null && (CharacterObject)this._conversationManager.SpeakerAgent.Character != null && this._conversationManager.SpeakerAgent.Character.IsHero && this._conversationManager.SpeakerAgent.Character != CharacterObject.PlayerCharacter)
                this.Relation = (int)((CharacterObject)this._conversationManager.SpeakerAgent.Character).HeroObject.GetRelationWithPlayer();

            this.IsRequireClick = true;
            this.IsRequireType = false;
            _player = new WaveOutEvent();
            float soundVolume = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.MasterVolume) * NativeOptions.GetConfig(NativeOptions.NativeOptionsType.SoundVolume);
            _player.Volume = soundVolume * 0.7f;  // Lets decrease the volume to allow sound blend into environment
            _streamQueue = new List<RawSourceWaveStream>();            
            this.ExecuteSetCurrentAnswer((ConversationItemVM)null);
            this.RefreshValues();
        }

        string conversationCharacterId;
        bool printedOneTimeWarning;

        private void SetConversationCharacterId()
        {
            if (State.ChatViewSharedState.IsVillagerChat)
            {
                conversationCharacterId = "unique-villager-fact";
                return;
            }

            SceneChatCharacterController sController = Mission.Current.GetMissionBehavior<SceneChatCharacterController>();
            conversationCharacterId = sController.GetCharacterId((Agent)_conversationManager.OneToOneConversationAgent);
        }

        private void UpdateVillagerInformation()
        {
            State.ChatViewSharedState.IsReadyToChat = false;
            SceneChatCharacterController sController = Mission.Current.GetMissionBehavior<SceneChatCharacterController>();
            sController.CreateVillagerOrTownsman();
        }

        public void OnTick(float dt)
        {
            this.IsRequireType = State.ChatViewSharedState.IsChatRequiresType;
            this.IsRequireClick = !this.IsRequireType;

            if (PlayerEncounter.InsideSettlement)
            {
                if (State.ChatViewSharedState.IsVillagerChat && !updatedVillager)
                {
                    UpdateVillagerInformation();
                    updatedVillager = true;
                }
            }

            if (this.IsRequireType)
            {
                if (!isConnected)
                {
                    if (State.ChatViewSharedState.IsReadyToChat)
                    {
                        ConnectToChat();
                    } else
                    {
                        if (!printedOneTimeWarning)
                        {
                            printedOneTimeWarning = true;
                            InformationManager.DisplayMessage(new InformationMessage("Working on making character ready to chat", Colors.Yellow));
                        }
                    }
                } else
                {
                   if(_streamQueue.Count > 0 && isFirstEmotionArrived)
                   {
                        var first = _streamQueue[0];
                        if(_player.PlaybackState == PlaybackState.Stopped) {
                            _streamQueue.RemoveAt(0);
                            SetAnimationFromLastEmotion();
                            _player.Init(first);
                            _player.Play();
                        }
                    } 
                    else if( _streamQueue.Count == 0 && _player.PlaybackState == PlaybackState.Stopped)
                    {
                        string idle = CharacterEngine.MapIdleEmotion(lastEmotion);
                        FindAndSetFactialAnimation(_conversationManager.OneToOneConversationAgent, "convo_" + idle);
                    }
                }

                float soundVolume = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.MasterVolume) * NativeOptions.GetConfig(NativeOptions.NativeOptionsType.SoundVolume);
                if(_player.Volume != soundVolume)
                    _player.Volume = soundVolume * 0.75f;
            }
        }


        private void OnPersuasionProgress(
          Tuple<PersuasionOptionArgs, PersuasionOptionResult> result)
        {
            this.Persuasion?.OnPersuasionProgress(result);
            this.AnswerList.ApplyActionOnAllItems((Action<ConversationItemVM>)(a => Helper.ReflectionHelper.InvokeInternalMethod(a, "OnPersuasionProgress",result)));
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            this.ContinueText = this._getContinueInputText();
            this.MoreOptionText = GameTexts.FindText("str_more_brackets").ToString();
            this.PersuasionText = GameTexts.FindText("str_persuasion").ToString();
            this.RelationHint = new HintViewModel(GameTexts.FindText("str_tooltip_label_relation"));
            this.GoldHint = new HintViewModel(new TextObject("{=o5G8A8ZH}Your Denars"));
            this._answerList.ApplyActionOnAllItems((Action<ConversationItemVM>)(x => x.RefreshValues()));
            this._defenderParties.ApplyActionOnAllItems((Action<ConversationAggressivePartyItemVM>)(x => x.RefreshValues()));
            this._attackerParties.ApplyActionOnAllItems((Action<ConversationAggressivePartyItemVM>)(x => x.RefreshValues()));
            this._defenderLeader.RefreshValues();
            this._attackerLeader.RefreshValues();
            this._currentSelectedAnswer.RefreshValues();
        }

        public void OnConversationContinue()
        {
            if (ConversationManager.GetPersuasionIsActive() && (!ConversationManager.GetPersuasionIsActive() || this.IsPersuading))
            {
                List<ConversationSentenceOption> curOptions = this._conversationManager.CurOptions;
                if ((curOptions != null ? curOptions.Count : 0) > 1)
                    return;
            }
            this.Refresh();
        }

        public void ExecuteLink(string link)
        {
            if (this._isLinksDisabled)
                return;
            Campaign.Current.EncyclopediaManager.GoToLink(link);
        }

        public void ExecuteConversedHeroLink()
        {
            if (this._isLinksDisabled || !(this._currentDialogCharacter is CharacterObject currentDialogCharacter))
                return;
            Campaign.Current.EncyclopediaManager.GoToLink(currentDialogCharacter.HeroObject?.EncyclopediaLink ?? currentDialogCharacter.EncyclopediaLink);
            this.SelectedAnOptionOrLinkThisFrame = true;
        }

        public void Refresh()
        {
            this.ExecuteCloseTooltip();
            this._isProcessingOption = false;
            this.IsLoadingOver = false;
            var conversationAgents = this._conversationManager.ConversationAgents;
            if ((conversationAgents != null ? (conversationAgents.Count > 0 ? 1 : 0) : 0) != 0)
            {
                this._currentDialogCharacter = this._conversationManager.SpeakerAgent.Character;
                this.CurrentCharacterNameLbl = this._currentDialogCharacter.Name.ToString();
                this.IsCurrentCharacterValidInEncyclopedia = false;
                if (this._currentDialogCharacter.IsHero && this._currentDialogCharacter != CharacterObject.PlayerCharacter)
                {
                    this.MinRelation = -100;
                    this.MaxRelation = 100;
                    Hero heroObject = ((CharacterObject)this._currentDialogCharacter).HeroObject;
                    if (heroObject.IsLord && !heroObject.IsMinorFactionHero && heroObject.Clan?.Leader == heroObject && heroObject.Clan?.Kingdom != null)
                    {
                        string stringId = heroObject.MapFaction.Culture.StringId;
                        TextObject textObject;
                        if (GameTexts.TryGetText("str_faction_noble_name_with_title", out textObject, stringId))
                        {
                            if (heroObject.Clan.Kingdom.Leader == heroObject)
                                textObject = GameTexts.FindText("str_faction_ruler_name_with_title", stringId);
                            StringHelpers.SetCharacterProperties("RULER", (CharacterObject)this._currentDialogCharacter);
                            this.CurrentCharacterNameLbl = textObject.ToString();
                        }
                    }
                    this.IsRelationEnabled = true;
                    this.Relation = Hero.MainHero.GetRelation(heroObject);
                    GameTexts.SetVariable("NUM", this.Relation.ToString());
                    this.RelationText = this.Relation <= 0 ? (this.Relation >= 0 ? this.Relation.ToString() : "-" + (object)MathF.Abs(this.Relation)) : "+" + (object)this.Relation;
                    if (heroObject.Clan == null || heroObject.Clan == CampaignData.NeutralFaction)
                    {
                        this.ConversedHeroBanner = new ImageIdentifierVM();
                        this.IsRelationEnabled = false;
                        this.IsBannerEnabled = false;
                    }
                    else
                    {
                        this.ConversedHeroBanner = heroObject != null ? new ImageIdentifierVM(heroObject.ClanBanner) : new ImageIdentifierVM();
                        this.FactionHint = new HintViewModel(heroObject != null ? heroObject.Clan.Name : TextObject.Empty);
                        this.IsBannerEnabled = true;
                    }
                    this.IsCurrentCharacterValidInEncyclopedia = Campaign.Current.EncyclopediaManager.GetPageOf(typeof(Hero)).IsValidEncyclopediaItem((object)heroObject);
                }
                else
                {
                    this.ConversedHeroBanner = new ImageIdentifierVM();
                    this.IsRelationEnabled = false;
                    this.IsBannerEnabled = false;
                    this.IsCurrentCharacterValidInEncyclopedia = Campaign.Current.EncyclopediaManager.GetPageOf(typeof(CharacterObject)).IsValidEncyclopediaItem((object)(CharacterObject)this._conversationManager.SpeakerAgent.Character);
                }
            }
            this.DialogText = this._conversationManager.CurrentSentenceText;
            this.AnswerList.Clear();
            InworldMissionChatVM._isCurrentlyPlayerSpeaking = this._conversationManager.SpeakerAgent.Character == Hero.MainHero.CharacterObject;
            this._conversationManager.GetPlayerSentenceOptions();
            List<ConversationSentenceOption> curOptions = this._conversationManager.CurOptions;
            int num = curOptions != null ? (curOptions.Count) : 0;
            if (num > 0 && !InworldMissionChatVM._isCurrentlyPlayerSpeaking)
            {
                for (int index = 0; index < num; ++index)
                    this.AnswerList.Add(new ConversationItemVM(new Action<int>(this.OnSelectOption), new Action(this.OnReadyToContinue), new Action<ConversationItemVM>(this.ExecuteSetCurrentAnswer), index, this._conversationManager.CurOptions[index]));
            }
            this.GoldText = CampaignUIHelper.GetAbbreviatedValueTextFromValue(Hero.MainHero.Gold);
            this.IsPersuading = ConversationManager.GetPersuasionIsActive();
            if (this.IsPersuading)
                this.CurrentSelectedAnswer = new ConversationItemVM();
            this.IsLoadingOver = true;
            this.Persuasion.RefreshPersusasion();
        }

        private void OnReadyToContinue() => this.Refresh();

        private void ExecuteDefenderTooltip()
        {
            if (PlayerEncounter.PlayerIsDefender)
                InformationManager.ShowTooltip(typeof(List<MobileParty>), (object)0);
            else
                InformationManager.ShowTooltip(typeof(List<MobileParty>), (object)1);
        }

        public void ExecuteCloseTooltip() => MBInformationManager.HideInformations();

        public void ExecuteHeroTooltip()
        {
            CharacterObject currentDialogCharacter = (CharacterObject)this._currentDialogCharacter;
            if (currentDialogCharacter == null || !currentDialogCharacter.IsHero)
                return;
            InformationManager.ShowTooltip(typeof(Hero), (object)currentDialogCharacter.HeroObject, (object)true);
        }

        private void ExecuteAttackerTooltip()
        {
            if (PlayerEncounter.PlayerIsAttacker)
                InformationManager.ShowTooltip(typeof(List<MobileParty>), (object)0);
            else
                InformationManager.ShowTooltip(typeof(List<MobileParty>), (object)1);
        }

        private void ExecuteHeroInfo()
        {
            if (this._conversationManager.ListenerAgent.Character == Hero.MainHero.CharacterObject)
                Campaign.Current.EncyclopediaManager.GoToLink(Hero.MainHero.EncyclopediaLink);
            else if (CharacterObject.OneToOneConversationCharacter.IsHero)
                Campaign.Current.EncyclopediaManager.GoToLink(CharacterObject.OneToOneConversationCharacter.HeroObject.EncyclopediaLink);
            else
                Campaign.Current.EncyclopediaManager.GoToLink(CharacterObject.OneToOneConversationCharacter.EncyclopediaLink);
        }

      
        public void ConnectToChat()
        {
            SetConversationCharacterId();
            isConnected = true;
            _webSocket = new WebSocket("ws://127.0.0.1:3000/chatWithCharacter");
            _webSocket.OnMessage += _webSocket_OnMessage;
            _webSocket.OnOpen += (sender, e) =>
            {
                string stringDate = CampaignTime.Now.GetDayOfSeason + "-" + CampaignTime.Now.GetDayOfSeason + "-" + CampaignTime.Now.GetYear;
                string initiater = "{ \"type\": \"connect\", \"characterId\": \"" + conversationCharacterId + "\", \"gameDate\": \"" + stringDate + "\",  \"playerName\": \"" + (State.ChatViewSharedState.IsVillagerChat ? "stranger" : Hero.MainHero.Name.ToString()) + "\" }";
                SendJsonAsync(initiater);
                return;
            };
            _webSocket.OnError += (sender, e) =>
            {

            };
            _webSocket.Connect();
        }

        private void _webSocket_OnMessage(object sender, MessageEventArgs e)
        {
            JObject responseObject = JObject.Parse(e.Data);
            if (responseObject.ContainsKey("type"))
            {
                string textType = responseObject["type"].ToString();
                if (textType == "text")
                {
                    NewDialogText += responseObject["message"].ToString();
                }
                else if(textType == "audio")
                {
                    PlaySoundAsync(responseObject["data"].ToString());
                    SetAnimationFromLastEmotion();
                }
                else if (textType == "emotion")
                {
                    if (!isFirstEmotionArrived) isFirstEmotionArrived = true;
                    SetAnimation(responseObject["emotion"]["behavior"].ToString());
                }
            }
        }

        public void SendJsonAsync(string json)
        {
            IsWaitingToSend = true;
            jsonToSend = json;
            _webSocket.Send(json);
        }


        public void SetAnimation(string emotion)
        {
            Enum.TryParse<CharacterEngine.EmotionBehavior>(emotion, true, out lastEmotion);
            SetAnimationFromLastEmotion();
        }

        private void SetAnimationFromLastEmotion()
        {
            if (_player == null || _player.PlaybackState == PlaybackState.Playing)
            {
                string talking = CharacterEngine.MapTalkEmotion(lastEmotion);
                FindAndSetFactialAnimation(_conversationManager.OneToOneConversationAgent, "talking_" + talking);
            }
        }

        private void FindAndSetFactialAnimation(IAgent agent, string facAnim)
        {
            foreach (Agent a in Mission.Current.Agents)
            {
                if (a == agent)
                {
                    string currentFacialAnim = a.GetAgentFacialAnimation();
                    a.SetAgentFacialAnimation(Agent.FacialAnimChannel.Mid, facAnim, true);
                    break;
                }
            }
        }

        public void PlaySoundAsync(string chunk)
        {
            var sampleRate = 22050;
            byte[] decodedBytes = Convert.FromBase64String(chunk);
            var ms = new MemoryStream(decodedBytes);
            var rs = new RawSourceWaveStream(ms, new WaveFormat(sampleRate, 16, 1));
            _streamQueue.Add(rs);   
        }

        public void BackToChat()
        {
            if (this.IsRequireType)
            {
                if (this._conversationManager.OneToOneConversationCharacter.Age < 18)
                    this._conversationManager.EndConversation();
                else if (this._conversationManager.CurOptions.Count > 0) {
                    this._conversationManager.ContinueConversation(); 
                } else {
                    if(this._conversationManager.OneToOneConversationCharacter.Age < 18)
                        this._conversationManager.EndConversation();
                    else
                        this._conversationManager.ContinueConversation();
                }

                State.ChatViewSharedState.IsChatRequiresType = false;

                if (_webSocket.IsAlive) {
                    string finalizer = "{ \"type\": \"endchat\"}";
                    _webSocket.Send(finalizer);
                    _webSocket.Close();
                }
                isConnected = false;
            }
        }

        public void SendPlayerInput()
        {
            if (this.ConversationText == String.Empty) return;
            this.ConversationText = this.ConversationText.Replace("'", "").Replace("\"", "").Replace("{", "").Replace("}", "");
            string message = "{\"type\": \"chat\",\"message\": \"" + this.ConversationText + "\"}";
            this.ConversationText = "";
            this.NewDialogText = "";
            SendJsonAsync(message);
        }

        private void OnSelectOption(int optionIndex)
        {
            if (this._isProcessingOption)
                return;
            this._isProcessingOption = true;
            this._conversationManager.DoOption(optionIndex);
            this.Persuasion?.RefreshPersusasion();
            this.SelectedAnOptionOrLinkThisFrame = true;
        }

        public void ExecuteFinalizeSelection() => this.Refresh();

        public void ExecuteContinue()
        {
            if (!State.ChatViewSharedState.IsReadyToChat)
            {
                if(_conversationManager.OneToOneConversationCharacter.Occupation != Occupation.Villager
                    &&
                   _conversationManager.OneToOneConversationCharacter.Occupation != Occupation.Townsfolk)
                {
                    this._conversationManager.ContinueConversation();
                    this._isProcessingOption = false;
                    return;
                }
                InformationManager.DisplayMessage(new InformationMessage("Character is not ready"));
                return;
            }
            this._conversationManager.ContinueConversation();
            this._isProcessingOption = false;
        }

        private void ExecuteSetCurrentAnswer(ConversationItemVM _answer)
        {
            this.Persuasion.SetCurrentOption(_answer?.PersuasionItem);
            if (_answer != null)
                this.CurrentSelectedAnswer = _answer;
            else
                this.CurrentSelectedAnswer = new ConversationItemVM();
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            CampaignEvents.PersuasionProgressCommittedEvent.ClearListeners((object)this);
            this.Persuasion?.OnFinalize();
        }


        [DataSourceProperty]
        public PersuasionVM Persuasion
        {
            get => this._persuasion;
            set
            {
                if (value == this._persuasion)
                    return;
                this._persuasion = value;
                this.OnPropertyChangedWithValue((object)value, nameof(Persuasion));
            }
        }

        [DataSourceProperty]
        public PowerLevelComparer PowerComparer
        {
            get => this._powerComparer;
            set
            {
                if (value == this._powerComparer)
                    return;
                this._powerComparer = value;
                this.OnPropertyChangedWithValue((object)value, nameof(PowerComparer));
            }
        }

        [DataSourceProperty]
        public int Relation
        {
            get => this._relation;
            set
            {
                if (this._relation == value)
                    return;
                this._relation = value;
                this.OnPropertyChangedWithValue((object)value, nameof(Relation));
            }
        }

        [DataSourceProperty]
        public int MinRelation
        {
            get => this._minRelation;
            set
            {
                if (this._minRelation == value)
                    return;
                this._minRelation = value;
                this.OnPropertyChangedWithValue((object)value, nameof(MinRelation));
            }
        }

        [DataSourceProperty]
        public int MaxRelation
        {
            get => this._maxRelation;
            set
            {
                if (this._maxRelation == value)
                    return;
                this._maxRelation = value;
                this.OnPropertyChangedWithValue((object)value, nameof(MaxRelation));
            }
        }

        [DataSourceProperty]
        public ConversationAggressivePartyItemVM DefenderLeader
        {
            get => this._defenderLeader;
            set
            {
                if (value == this._defenderLeader)
                    return;
                this._defenderLeader = value;
                this.OnPropertyChangedWithValue((object)value, nameof(DefenderLeader));
            }
        }

        [DataSourceProperty]
        public ConversationAggressivePartyItemVM AttackerLeader
        {
            get => this._attackerLeader;
            set
            {
                if (value == this._attackerLeader)
                    return;
                this._attackerLeader = value;
                this.OnPropertyChangedWithValue((object)value, nameof(AttackerLeader));
            }
        }

        [DataSourceProperty]
        public MBBindingList<ConversationAggressivePartyItemVM> AttackerParties
        {
            get => this._attackerParties;
            set
            {
                if (value == this._attackerParties)
                    return;
                this._attackerParties = value;
                this.OnPropertyChangedWithValue((object)value, nameof(AttackerParties));
            }
        }

        [DataSourceProperty]
        public MBBindingList<ConversationAggressivePartyItemVM> DefenderParties
        {
            get => this._defenderParties;
            set
            {
                if (value == this._defenderParties)
                    return;
                this._defenderParties = value;
                this.OnPropertyChangedWithValue((object)value, nameof(DefenderParties));
            }
        }

        [DataSourceProperty]
        public string MoreOptionText
        {
            get => this._moreOptionText;
            set
            {
                if (!(this._moreOptionText != value))
                    return;
                this._moreOptionText = value;
                this.OnPropertyChangedWithValue((object)value, nameof(MoreOptionText));
            }
        }

        [DataSourceProperty]
        public string GoldText
        {
            get => this._goldText;
            set
            {
                if (!(this._goldText != value))
                    return;
                this._goldText = value;
                this.OnPropertyChangedWithValue((object)value, nameof(GoldText));
            }
        }

        [DataSourceProperty]
        public string PersuasionText
        {
            get => this._persuasionText;
            set
            {
                if (!(this._persuasionText != value))
                    return;
                this._persuasionText = value;
                this.OnPropertyChangedWithValue((object)value, nameof(PersuasionText));
            }
        }

        [DataSourceProperty]
        public bool IsCurrentCharacterValidInEncyclopedia
        {
            get => this._isCurrentCharacterValidInEncyclopedia;
            set
            {
                if (this._isCurrentCharacterValidInEncyclopedia == value)
                    return;
                this._isCurrentCharacterValidInEncyclopedia = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsCurrentCharacterValidInEncyclopedia));
            }
        }

        [DataSourceProperty]
        public bool IsLoadingOver
        {
            get => this._isLoadingOver;
            set
            {
                if (this._isLoadingOver == value)
                    return;
                this._isLoadingOver = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsLoadingOver));
            }
        }

        [DataSourceProperty]
        public bool IsPersuading
        {
            get => this._isPersuading;
            set
            {
                if (this._isPersuading == value)
                    return;
                this._isPersuading = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsPersuading));
            }
        }

        [DataSourceProperty]
        public bool IsRequireClick
        {
            get => this._isRequireClick;
            set
            {
                if (this._isRequireClick == value)
                    return;
                this._isRequireClick = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsRequireClick));
            }
        }

        [DataSourceProperty]
        public bool IsRequireType
        {
            get => this._isRequireType;
            set
            {
                if (this._isRequireType == value)
                    return;
                this._isRequireType = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsRequireType));
            }
        }

        [DataSourceProperty]
        public string ContinueText
        {
            get => this._continueText;
            set
            {
                if (!(this._continueText != value))
                    return;
                this._continueText = value;
                this.OnPropertyChangedWithValue((object)value, nameof(ContinueText));
            }
        }

        [DataSourceProperty]
        public string CurrentCharacterNameLbl
        {
            get => this._currentCharacterNameLbl;
            set
            {
                if (!(this._currentCharacterNameLbl != value))
                    return;
                this._currentCharacterNameLbl = value;
                this.OnPropertyChangedWithValue((object)value, nameof(CurrentCharacterNameLbl));
            }
        }

        [DataSourceProperty]
        public MBBindingList<ConversationItemVM> AnswerList
        {
            get => this._answerList;
            set
            {
                if (this._answerList == value)
                    return;
                this._answerList = value;
                this.OnPropertyChangedWithValue((object)value, nameof(AnswerList));
            }
        }

        [DataSourceProperty]
        public string DialogText
        {
            get => this._dialogText;
            set
            {
                if (!(this._dialogText != value))
                    return;
                this._dialogText = value;
                this.OnPropertyChangedWithValue((object)value, nameof(DialogText));
            }
        }

        [DataSourceProperty]
        public bool IsAggressive
        {
            get => this._isAggressive;
            set
            {
                if (value == this._isAggressive)
                    return;
                this._isAggressive = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsAggressive));
            }
        }

        [DataSourceProperty]
        public int SelectedSide
        {
            get => this._selectedSide;
            set
            {
                if (value == this._selectedSide)
                    return;
                this._selectedSide = value;
                this.OnPropertyChangedWithValue((object)value, nameof(SelectedSide));
            }
        }

        [DataSourceProperty]
        public string RelationText
        {
            get => this._relationText;
            set
            {
                if (!(this._relationText != value))
                    return;
                this._relationText = value;
                this.OnPropertyChangedWithValue((object)value, nameof(RelationText));
            }
        }

        [DataSourceProperty]
        public bool IsRelationEnabled
        {
            get => this._isRelationEnabled;
            set
            {
                if (value == this._isRelationEnabled)
                    return;
                this._isRelationEnabled = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsRelationEnabled));
            }
        }

        [DataSourceProperty]
        public bool IsBannerEnabled
        {
            get => this._isBannerEnabled;
            set
            {
                if (value == this._isBannerEnabled)
                    return;
                this._isBannerEnabled = value;
                this.OnPropertyChangedWithValue((object)value, nameof(IsBannerEnabled));
            }
        }

        [DataSourceProperty]
        public ConversationItemVM CurrentSelectedAnswer
        {
            get => this._currentSelectedAnswer;
            set
            {
                if (this._currentSelectedAnswer == value)
                    return;
                this._currentSelectedAnswer = value;
                this.OnPropertyChangedWithValue((object)value, nameof(CurrentSelectedAnswer));
            }
        }

        [DataSourceProperty]
        public ImageIdentifierVM ConversedHeroBanner
        {
            get => this._conversedHeroBanner;
            set
            {
                if (this._conversedHeroBanner == value)
                    return;
                this._conversedHeroBanner = value;
                this.OnPropertyChangedWithValue((object)value, nameof(ConversedHeroBanner));
            }
        }

        [DataSourceProperty]
        public HintViewModel RelationHint
        {
            get => this._relationHint;
            set
            {
                if (this._relationHint == value)
                    return;
                this._relationHint = value;
                this.OnPropertyChangedWithValue((object)value, nameof(RelationHint));
            }
        }

        [DataSourceProperty]
        public HintViewModel FactionHint
        {
            get => this._factionHint;
            set
            {
                if (this._factionHint == value)
                    return;
                this._factionHint = value;
                this.OnPropertyChangedWithValue((object)value, nameof(FactionHint));
            }
        }

        [DataSourceProperty]
        public HintViewModel GoldHint
        {
            get => this._goldHint;
            set
            {
                if (this._goldHint == value)
                    return;
                this._goldHint = value;
                this.OnPropertyChangedWithValue((object)value, nameof(GoldHint));
            }
        }

        [DataSourceProperty]
        public string NewDialogText
        {
            get
            {
                return _newDialogText;
            }
            set
            {
                if (_newDialogText != value)
                {
                    _newDialogText = value;
                    OnPropertyChangedWithValue(value, "NewDialogText");
                }
            }
        }


        [DataSourceProperty]
        public string ConversationText
        {
            get
            {
                return _conversationText;
            }
            set
            {
                if (_conversationText != value)
                {
                    _conversationText = value;
                    OnPropertyChangedWithValue(value, "ConversationText");
                }
            }
        }

    }
}
