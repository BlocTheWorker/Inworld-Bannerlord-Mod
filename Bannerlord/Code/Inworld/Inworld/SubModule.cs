using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using Inworld.Behavior;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Conversation;
using Inworld.InMission;
using SandBox.CampaignBehaviors;
using TaleWorlds.ModuleManager;
using System.Diagnostics;

namespace Inworld
{
    internal class PatchNativeCaller
    {
        [HarmonyPatch(typeof(DefaultVoiceOverModel), "GetSoundPathForCharacter")]
        internal class Patch_DefaultVoiceOverModel
        {
            public static bool Prefix(CharacterObject character, VoiceObject voiceObject, ref string __result)
            {
                __result = "";
                return false;
            }
        }
        
        [HarmonyPatch(typeof(ConversationScreenButtonWidget), "OnUpdate")]
        internal class Patch_ConversationScreenButtonWidget_OnUpdate
        {
            public static void Postfix(ConversationScreenButtonWidget __instance)
            {
                if (!State.ChatViewSharedState.IsChatRequiresType)
                    return;
                if (__instance.AnswerList == null || __instance.ContinueButton == null)
                    return;
                __instance.ContinueButton.IsVisible = false;
                __instance.ContinueButton.IsEnabled = false;
            }
        }

        [HarmonyPatch(typeof(CommonVillagersCampaignBehavior), "conversation_town_or_village_start_on_condition")]
        internal class Patch_CommonVillagersCampaignBehavior_conversation_town_or_village_start_on_condition
        {
            public static void Postfix(ref bool __result)
            {
                State.ChatViewSharedState.IsVillagerChat = __result;
            }
        }
    }

    public class SubModule : MBSubModuleBase
    {
        internal Process Client;
        internal bool didIStarted;

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            StartClient();
            DoNoHarmonyPatch();
        }
        
        private void StartClient()
        {
            if (didIStarted) return;
            try
            {
                string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string parentFolder = System.IO.Directory.GetParent((System.IO.Directory.GetParent(System.IO.Directory.GetParent(path).FullName)).FullName).FullName;
                string modRelayerFolder = System.IO.Path.Combine(parentFolder, "ModRelayer\\");

                // string moduleFullPath = ModuleHelper.GetModuleFullPath("Inworld");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    WorkingDirectory = modRelayerFolder,
                    FileName = modRelayerFolder + "InworldClient.exe",
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                didIStarted = true;
                Client = Process.Start(startInfo);
            } catch
            {

            }
        }

        protected override void OnSubModuleUnloaded()
        {
            if(Client != null)
            {
                Client.Refresh();
                if(!Client.HasExited)
                    Client.Close(); 
            }
        }

        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            if (mission.IsFieldBattle || mission.IsSallyOutBattle || mission.IsSiegeBattle) return;
            if (mission.Mode == MissionMode.Tournament) return;
            mission.AddMissionBehavior(new SceneChatCharacterController());
            mission.AddMissionBehavior(new SceneImmersivenessBonusController()); 
        }

        private void DoNoHarmonyPatch()
        {
            try
            {
                new Harmony("com.bloc.inworld").PatchAll();
            }
            catch (System.Exception e)
            {
                InformationManager.DisplayMessage(new InformationMessage("[Inworld] Something went wrong. Mod might not work properly", Colors.Red));
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (gameStarterObject.GetType() == typeof(CampaignGameStarter))
            {
                base.OnCampaignStart(game, gameStarterObject);
                CampaignGameStarter starter = (CampaignGameStarter)gameStarterObject;
                starter.AddBehavior(new InworldStateCampaignBehavior());
            }
        }
    }
}