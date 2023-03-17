using SandBox.Conversation.MissionLogics;
using SandBox.View.Missions;
using System;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Inworld.InworldView
{
    [OverrideView(typeof(MissionConversationView))]
    public class InworldMissionConversationView : MissionView, IConversationStateHandler
    {
        private InworldMissionChatVM _dataSource;
        private GauntletLayer _gauntletLayer;
        private ConversationManager _conversationManager;
        private MissionConversationCameraView _conversationCameraView;
        private MissionGauntletEscapeMenuBase _escapeView;
        private SpriteCategory _conversationCategory;

        public MissionConversationLogic ConversationHandler { get; private set; }

        public InworldMissionConversationView() => this.ViewOrderPriority = 49;

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            MissionGauntletEscapeMenuBase escapeView = this._escapeView;

            if (_conversationManager != null && _gauntletLayer != null && _gauntletLayer.Input != null)
            {
                if (Input.IsKeyDown(InputKey.Tab) || this.IsGameKeyReleasedInAnyLayer("Leave", true) || this._gauntletLayer.Input.IsKeyDown(InputKey.Tab))
                {
                    this._dataSource.BackToChat();
                    return;
                }
                else if (Input.IsKeyDown(InputKey.Enter) || this.IsGameKeyReleasedInAnyLayer("Confirm", true) || this._gauntletLayer.Input.IsKeyDown(InputKey.Enter))
                {
                    if (this._dataSource.GetType() == typeof(InworldMissionChatVM))
                    {
                        this._dataSource.SendPlayerInput();
                    }
                }
            }

            if ((escapeView != null ? (!escapeView.IsActive ? 1 : 0) : 1) == 0 || this._gauntletLayer == null)
                return;
            InworldMissionChatVM dataSource1 = this._dataSource;

            if (dataSource1 != null)
            {
                dataSource1.OnTick(dt);
            }

            if ((dataSource1 != null ? (dataSource1.AnswerList.Count <= 0 ? 1 : 0) : 0) != 0 && this.Mission.Mode != MissionMode.Barter)
            {
                if (!this.IsReleasedInSceneLayer("ContinueClick", false))
                {
                    if (this.IsReleasedInGauntletLayer("ContinueKey", true))
                    {
                        InworldMissionChatVM dataSource2 = this._dataSource;
                        if ((dataSource2 != null ? (!dataSource2.SelectedAnOptionOrLinkThisFrame ? 1 : 0) : 0) == 0)
                            goto label_7;
                    }
                    else
                        goto label_7;
                }

                if(!State.ChatViewSharedState.IsChatRequiresType)
                    this._dataSource?.ExecuteContinue();
            }
        label_7:
            if (this._gauntletLayer != null && this.IsGameKeyReleasedInAnyLayer("ToggleEscapeMenu", true))
                this.MissionScreen.OnEscape();
            if (this._dataSource != null)
                this._dataSource.SelectedAnOptionOrLinkThisFrame = false;
            if (this.MissionScreen.SceneLayer.Input.IsKeyDown(InputKey.RightMouseButton))
                this._gauntletLayer?.InputRestrictions.SetMouseVisibility(false);
            else
                this._gauntletLayer?.InputRestrictions.SetInputRestrictions();
        }

        public override void OnMissionScreenFinalize()
        {
            Campaign.Current.ConversationManager.Handler = (IConversationStateHandler)null;
            if (this._dataSource != null)
            {
                this._dataSource?.OnFinalize();
                this._dataSource = (InworldMissionChatVM)null;
            }
            this._gauntletLayer = (GauntletLayer)null;
            this.ConversationHandler = (MissionConversationLogic)null;
            base.OnMissionScreenFinalize();
        }

        public override void EarlyStart()
        {
            base.EarlyStart();
            this.ConversationHandler = this.Mission.GetMissionBehavior<MissionConversationLogic>();
            this._conversationCameraView = this.Mission.GetMissionBehavior<MissionConversationCameraView>();
            Campaign.Current.ConversationManager.Handler = (IConversationStateHandler)this;
        }

        public override void OnMissionScreenActivate()
        {
            base.OnMissionScreenActivate();
            if (this._dataSource == null)
                return;
            this.MissionScreen.SetLayerCategoriesStateAndDeactivateOthers(new string[2]
            {
        "Conversation",
        "SceneLayer"
            }, true);
            ScreenManager.TrySetFocus((ScreenLayer)this._gauntletLayer);
        }

        void IConversationStateHandler.OnConversationInstall()
        {
            this.MissionScreen.SetConversationActive(true);
            SpriteData spriteData = UIResourceManager.SpriteData;
            TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext;
            ResourceDepot uiResourceDepot = UIResourceManager.UIResourceDepot;
            this._conversationCategory = spriteData.SpriteCategories["ui_conversation"];
            this._conversationCategory.Load((ITwoDimensionResourceContext)resourceContext, uiResourceDepot);
            this._dataSource = new InworldMissionChatVM(new Func<string>(this.GetContinueKeyText));
            this._gauntletLayer = new GauntletLayer(this.ViewOrderPriority, "Conversation");
            this._gauntletLayer.LoadMovie("InworldConversation", (ViewModel)this._dataSource);
            GameKeyContext category = HotKeyManager.GetCategory("ConversationHotKeyCategory");
            this._gauntletLayer.Input.RegisterHotKeyCategory(category);
            if (!this.MissionScreen.SceneLayer.Input.IsCategoryRegistered(category))
                this.MissionScreen.SceneLayer.Input.RegisterHotKeyCategory(category);
            this._gauntletLayer.IsFocusLayer = true;
            this._gauntletLayer.InputRestrictions.SetInputRestrictions();
            this._escapeView = this.Mission.GetMissionBehavior<MissionGauntletEscapeMenuBase>();
            this.MissionScreen.AddLayer((ScreenLayer)this._gauntletLayer);
            this.MissionScreen.SetLayerCategoriesStateAndDeactivateOthers(new string[2]
            {
        "Conversation",
        "SceneLayer"
            }, true);
            ScreenManager.TrySetFocus((ScreenLayer)this._gauntletLayer);
            this._conversationManager = Campaign.Current.ConversationManager;
            InformationManager.ClearAllMessages();
        }

        public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
        {
            base.OnMissionModeChange(oldMissionMode, atStart);
            if (oldMissionMode != MissionMode.Barter || this.Mission.Mode != MissionMode.Conversation)
                return;
            ScreenManager.TrySetFocus((ScreenLayer)this._gauntletLayer);
        }

        void IConversationStateHandler.OnConversationUninstall()
        {
            this.MissionScreen.SetConversationActive(false);
            if (this._dataSource != null)
            {
                this._dataSource?.OnFinalize();
                this._dataSource = (InworldMissionChatVM)null;
            }
            this._conversationCategory.Unload();
            this._gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus((ScreenLayer)this._gauntletLayer);
            this._gauntletLayer.InputRestrictions.ResetInputRestrictions();
            this.MissionScreen.SetLayerCategoriesStateAndToggleOthers(new string[1]
            {
        "Conversation"
            }, false);
            this.MissionScreen.SetLayerCategoriesState(new string[1]
            {
        "SceneLayer"
            }, true);
            this.MissionScreen.RemoveLayer((ScreenLayer)this._gauntletLayer);
            this._gauntletLayer = (GauntletLayer)null;
            this._escapeView = (MissionGauntletEscapeMenuBase)null;
        }

        private string GetContinueKeyText()
        {
            GameTexts.SetVariable("CONSOLE_KEY_NAME", HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId("ConversationHotKeyCategory", "ContinueKey")));
            return GameTexts.FindText("str_click_to_continue_console").ToString();
        }

        void IConversationStateHandler.OnConversationActivate() => this.MissionScreen.SetLayerCategoriesStateAndDeactivateOthers(new string[2]
        {
          "Conversation",
          "SceneLayer"
        }, true);

        void IConversationStateHandler.OnConversationDeactivate() => MBInformationManager.HideInformations();

        void IConversationStateHandler.OnConversationContinue() => this._dataSource.OnConversationContinue();

        void IConversationStateHandler.ExecuteConversationContinue()
        {
            
            if (State.ChatViewSharedState.IsChatRequiresType)
                return;
            this._dataSource.ExecuteContinue();
        }

        private bool IsGameKeyReleasedInAnyLayer(string hotKeyID, bool isDownAndReleased) => this.IsReleasedInSceneLayer(hotKeyID, isDownAndReleased) | this.IsReleasedInGauntletLayer(hotKeyID, isDownAndReleased);

        private bool IsReleasedInSceneLayer(string hotKeyID, bool isDownAndReleased)
        {
            if (isDownAndReleased)
            {
                SceneLayer sceneLayer = this.MissionScreen.SceneLayer;
                return sceneLayer != null && sceneLayer.Input.IsHotKeyDownAndReleased(hotKeyID);
            }
            SceneLayer sceneLayer1 = this.MissionScreen.SceneLayer;
            return sceneLayer1 != null && sceneLayer1.Input.IsHotKeyReleased(hotKeyID);
        }

        private bool IsReleasedInGauntletLayer(string hotKeyID, bool isDownAndReleased)
        {
            if (isDownAndReleased)
            {
                GauntletLayer gauntletLayer = this._gauntletLayer;
                return gauntletLayer != null && gauntletLayer.Input.IsHotKeyDownAndReleased(hotKeyID);
            }
            GauntletLayer gauntletLayer1 = this._gauntletLayer;
            return gauntletLayer1 != null && gauntletLayer1.Input.IsHotKeyReleased(hotKeyID);
        }
    }
}
