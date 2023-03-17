using SandBox.Missions.MissionLogics;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.Settlements;
using System.Collections.Generic;
using TaleWorlds.Engine;

namespace Inworld.InMission
{
    // Hyper simple controller for generating cavalry going from point A to B in every X seconds.
    internal class SceneImmersivenessBonusController : MissionLogic
    {
        CharacterObject characterToSpawn;
        bool _canGenerate;
        List<Agent> _agents;
        float timer = 0;
        bool didGenerate;
        MatrixFrame finalDestination;
        MatrixFrame startFrame;
        ItemObject horsie;
        ItemObject horsieHarness;
        bool didGiveOrder;
        bool didSetCloth;
        float overallDistance;

        public SceneImmersivenessBonusController()
        {
            horsie = Game.Current.ObjectManager.GetObject<ItemObject>("t2_battania_horse");
            horsieHarness = Game.Current.ObjectManager.GetObject<ItemObject>("light_harness");
            if (PlayerEncounter.EncounterSettlement != null && PlayerEncounter.EncounterSettlement.IsTown)
            {
                Location location = CampaignMission.Current.Location;
                if (location != null && location.StringId == "center")
                {
                    characterToSpawn = PlayerEncounter.EncounterSettlement.Culture.EliteBasicTroop;
                    while(characterToSpawn.UpgradeTargets.Length > 0) {
                        characterToSpawn = characterToSpawn.UpgradeTargets[0];
                    }
                    _canGenerate = true;
                    _agents = new List<Agent>();
                }
            }
        }


        public override void EarlyStart()
        {
            var finalDestinationTag = this.Mission.Scene.FindEntityWithTag("sp_prison_guard");
            var startPositionTag= this.Mission.Scene.FindEntityWithTag("spawnpoint_player_outside");
            if (finalDestinationTag != null && startPositionTag != null) {
                finalDestination = finalDestinationTag.GetFrame();
                startFrame = startPositionTag.GetFrame();
                overallDistance = finalDestination.origin.Distance(startFrame.origin);
            } else {
                _canGenerate = false;
            }
            this.Mission.Scene.SetClothSimulationState(true);
        }

        public override void OnMissionTick(float dt)
        {
            if (!didSetCloth) {
                this.Mission.Scene.SetClothSimulationState(true);
                didSetCloth = true;
            }
            if (!_canGenerate) return;
            timer += dt;

            if(timer > overallDistance/2) {

                foreach (Agent a in _agents) {
                    a.FadeOut(false, true);
                }
                _agents.Clear();
                _agents.Add(SpawnCavalry(this.characterToSpawn, startFrame));
                _agents.Add(SpawnCavalry(this.characterToSpawn, startFrame));
                _agents.Add(SpawnCavalry(this.characterToSpawn, startFrame));
                _agents.Add(SpawnCavalry(this.characterToSpawn, startFrame));
                _agents.Add(SpawnCavalry(this.characterToSpawn, startFrame));
                _agents.Add(SpawnCavalry(this.characterToSpawn, startFrame));
                didGenerate = true;
                timer = 0;
                // each time, they will spawn less frequent
                overallDistance *= 1.7f;
            }

            if (didGenerate)
            {
                List<Agent> removeList = new List<Agent>(); 
                foreach(Agent a in _agents)
                {
                    if(a.Position.AsVec2.Distance(finalDestination.origin.AsVec2) < 10)
                    {
                        a.FadeOut(false, true);
                        removeList.Add(a);  
                    }
                   
                    if (!didGiveOrder)
                    {
                        if (MBRandom.RandomFloat < 0.4f) {
                            a.MakeVoice(SkinVoiceManager.VoiceType.Move, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                        }
                        didGiveOrder = true;
                    }
                }

                if((int)timer % 5 == 0) {
                    didGiveOrder = true;
                }

                foreach(Agent agent in removeList) {
                    _agents.Remove(agent);
                }
            }
        }

        private Agent SpawnCavalry(
          CharacterObject character,
          MatrixFrame spawnPointFrame)
        {
            Team team = Team.Invalid;
            AgentData ad = new AgentData(character);
            AgentBuildData bd = new AgentBuildData(ad);
            MatrixFrame mf = new MatrixFrame(spawnPointFrame.rotation, spawnPointFrame.origin);
            mf.Strafe(MBRandom.RandomInt(-4, 4));
            mf.origin.z = this.Mission.Scene.GetGroundHeightAtPosition(spawnPointFrame.origin);
            (uint color1, uint color2) settlementColors = (Settlement.CurrentSettlement.MapFaction.Color, Settlement.CurrentSettlement.MapFaction.Color2);
            AgentBuildData agentBuildData1 = bd.Team(team).InitialPosition(in mf.origin);
            Vec2 vec2 = mf.rotation.f.AsVec2;
            vec2 = vec2.Normalized();
            ref Vec2 local = ref vec2;
            Equipment eq = new Equipment(characterToSpawn.Equipment);
            if (!characterToSpawn.HasMount())
            {
              
                eq[EquipmentIndex.Horse] = new EquipmentElement(horsie);
                eq[EquipmentIndex.HorseHarness] = new EquipmentElement(horsieHarness);
            }
            
            ItemObject bannerSpear = Game.Current.ObjectManager.GetObject<ItemObject>("western_spear_3_t3");
            if (bannerSpear != null) {
                if (MBRandom.RandomFloat < 0.5f) {
                    eq[EquipmentIndex.Weapon1] = new EquipmentElement(bannerSpear);
                }
            }
            
            bd.Equipment(eq);
            AgentBuildData agentBuildData2 = agentBuildData1.InitialDirection(in local).ClothingColor1(settlementColors.color1).ClothingColor2(settlementColors.color2).NoHorses(false).Banner(PlayerEncounter.EncounterSettlement.OwnerClan?.Banner);
            Agent agent = this.Mission.SpawnAgent(agentBuildData2);
            WorldPosition wp = new WorldPosition(this.Mission.Scene, finalDestination.origin);
            agent.SetScriptedPosition(ref wp, true, Agent.AIScriptedFrameFlags.DoNotRun);
            agent.WieldInitialWeapons(Agent.WeaponWieldActionType.WithAnimation);           
            return agent;
        }

    }
}
