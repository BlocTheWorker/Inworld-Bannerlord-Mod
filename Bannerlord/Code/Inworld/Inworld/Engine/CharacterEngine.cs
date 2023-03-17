using SandBox.Objects.AnimationPoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Engine.MeshBuilder;

namespace Inworld.Engine
{
    internal class CharacterEngine
    {
        public enum EmotionBehavior
        {
            NEUTRAL,
            DISGUST,
            CONTEMPT,
            BELLIGERENCE,
            DOMINEERING,
            CRITICISM,
            ANGER,
            TENSION,
            TENSEHUMOR,
            DEFENSIVENESS,
            WHINING,
            SADNESS,
            STONEWALLING,
            INTEREST,
            VALIDATION,
            AFFECTION,
            HUMOR,
            SURPRISE,
            JOY
        }

        public static string MapTalkEmotion(EmotionBehavior emotion)
        {
            switch (emotion)
            {
                case EmotionBehavior.NEUTRAL:
                    return "normal";
                case EmotionBehavior.DISGUST:
                case EmotionBehavior.CONTEMPT:
                case EmotionBehavior.BELLIGERENCE:
                case EmotionBehavior.DOMINEERING:
                case EmotionBehavior.CRITICISM:
                    return "angry";
                case EmotionBehavior.ANGER:
                case EmotionBehavior.TENSION:
                case EmotionBehavior.TENSEHUMOR:
                case EmotionBehavior.WHINING:
                    return "mean";
                case EmotionBehavior.SADNESS:
                case EmotionBehavior.STONEWALLING:
                    return "sad";
                case EmotionBehavior.INTEREST:
                case EmotionBehavior.VALIDATION:
                case EmotionBehavior.AFFECTION:
                case EmotionBehavior.HUMOR:
                case EmotionBehavior.SURPRISE:
                case EmotionBehavior.JOY:
                    return "happy";
                case EmotionBehavior.DEFENSIVENESS:
                    return "engaged";
                default:
                    return "normal";
            }
        }

        public static string MapIdleEmotion(EmotionBehavior emotion)
        {
            Dictionary<EmotionBehavior, string[]> mappings = new Dictionary<EmotionBehavior, string[]>
            {
                { EmotionBehavior.NEUTRAL, new[] { "neutral" } },
                { EmotionBehavior.DISGUST, new[] { "annoyed" } },
                { EmotionBehavior.CONTEMPT, new[] { "confused_annoyed", "confused_normal" } },
                { EmotionBehavior.BELLIGERENCE, new[] { "bored", "bored2" } },
                { EmotionBehavior.DOMINEERING, new[] { "grave", "stern" } },
                { EmotionBehavior.CRITICISM, new[] { "very_stern", "undecided_closed", "mocking_teasing" } },
                { EmotionBehavior.ANGER, new[] { "annoyed", "astonished" } },
                { EmotionBehavior.TENSION, new[] { "beaten", "nervous" } },
                { EmotionBehavior.TENSEHUMOR, new[] { "shocked", "confused_normal" } },
                { EmotionBehavior.DEFENSIVENESS, new[] { "thinking", "happy" } },
                { EmotionBehavior.WHINING, new[] { "calm_friendly", "focused_happy" } },
                { EmotionBehavior.SADNESS, new[] { "nervous", "nonchalant" } },
                { EmotionBehavior.STONEWALLING, new[] { "mocking_teasing", "mocking_revenge" } },
                { EmotionBehavior.INTEREST, new[] { "mocking_aristocratic", "focused_happy" } },
                { EmotionBehavior.VALIDATION, new[] { "huge_smile", "happy" } },
                { EmotionBehavior.AFFECTION, new[] { "calm_friendly", "focused_happy" } },
                { EmotionBehavior.HUMOR, new[] { "delighted", "mocking_teasing", "nonchalant" } },
                { EmotionBehavior.SURPRISE, new[] { "shocked", "astonished", "mocking_revenge" } },
                { EmotionBehavior.JOY, new[] { "mocking_aristocratic", "huge_smile", "happy" } },
            };
            if (!mappings.ContainsKey(emotion)) return "neutral";
            string[] possibleEmotions = mappings[emotion];
            return possibleEmotions.GetRandomElement();
        }

        public static string GetBackgroundData(Hero hero)
        {
            string backstory = hero.EncyclopediaText?.ToString();
            if (backstory == "" || backstory == null)
            {
                backstory = Hero.SetHeroEncyclopediaTextAndLinks(hero).ToString();
            }
            backstory += GetBackgroundSpice(hero);
            string basicInformation = "{NAME} is {GENDER}. Belongs to {CULTURE} culture. {NAME}s age is {AGE}. {NAME} is member of {CLAN} clan. {NAME} cannot join anyone or give anything not even recruits and doesn't want to join any quest. {NAME} cannot know location of parties that are not part of their kingdom.";
            basicInformation = basicInformation.Replace("{NAME}", hero.Name.ToString());
            basicInformation = basicInformation.Replace("{GENDER}", hero.IsFemale ? "female" : "male");
            basicInformation = basicInformation .Replace("{CULTURE}", hero.Culture.ToString());
            basicInformation = basicInformation.Replace("{AGE}", ((int)hero.Age).ToString());
            basicInformation = basicInformation.Replace("{CLAN}", hero.Clan.Name.ToString());
            backstory += basicInformation;
            string somebackstory = Corpus.coreDescriptionHelper.GetRandomElement();
            somebackstory = somebackstory.Replace("{character}", hero.Name.ToString());
            backstory += somebackstory;
            backstory = backstory.Substring(0, Math.Min(backstory.Length, 1999));
            return backstory;
        }


        public static string GetMotivationForHero(Hero hero)
        {
            if (hero.IsFactionLeader)
            {
                return "{Character} seeks to expand their territory and gain more power and influence.";
            } else
            {
                if (hero.IsFemale)
                {
                    return "{Character} wants to keep her {CLAN} clan members safe.".Replace("{CLAN}", hero.Clan.Name.ToString());
                } else {

                    if(hero.Age < 20)
                    {
                        return "{Character} wants to be as famous as it's grandfathers.";
                    } else
                    {
                        if (hero.CanLeadParty())
                        {
                            return "{Character} seeks to be known commander.";
                        } else
                        {
                            return "{Character} wants to help {CLAN} clan as much as he can.".Replace("{CLAN}", hero.Clan.Name.ToString());
                        }
                    }
                }
            }
        }

        public static string GetWhatIWasDoing(Agent characterAgent)
        {
            var currentAnim = characterAgent.GetCurrentAction(0);

            if (currentAnim.Name.Contains("writer"))
            {
                return "{Character} was taking notes and reading, before this conversation with {Player}";
            }
            else if (currentAnim.Name.Contains("sit"))
            {
               return "{Character} was sitting and thinking, before this conversation with {Player}";
            }
            else if (currentAnim.Name.Contains("conversation") && !characterAgent.IsUsingGameObject)
            {
                return "{Character} was having conversation with someone else, before this conversation with {Player}";
            }
            else
            {
                if (characterAgent.IsUsingGameObject)
                {
                    if (characterAgent.CurrentlyUsedGameObject.GetType() == typeof(AnimationPoint))
                    {
                        AnimationPoint usedAnim = characterAgent.CurrentlyUsedGameObject as AnimationPoint;
                        if (usedAnim.RightHandItem.Contains("notebook") || usedAnim.LeftHandItem.Contains("notebook"))
                        {
                            return "{Character} was taking notes and reading, before this conversation with {Player}";
                        }
                        else if (usedAnim.LoopStartAction.Contains("repair"))
                        {
                            return "{Character} was repairing something before this conversation with {Player}";
                        }
                        else if (usedAnim.PairLoopStartAction.Contains("argue"))
                        {
                            return "{Character} was arguing with friend before this conversation with {Player}";
                        }
                        else if (usedAnim.PairLoopStartAction.Contains("gossip"))
                        {
                            return "{Character} was gossiping with friend before this conversation with {Player}";
                        }
                        else if (usedAnim.PairLoopStartAction.Contains("talk_to"))
                        {
                            return "{Character} was talking with someone before this conversation with {Player}";
                        } else
                        {
                            return "{Character} was just catching breath and standing before this conversation with {Player}";
                        }
                    }
                }
                else
                {
                    return "{Character} was standing and thinking before this conversation with {Player}";
                }
            }

            return "{Character} wasn't doing anything important before conversation with {Player}, was not tending the crops.";
        }

        public static string GetVillagerTownsmanBackground()
        {
            Agent characterAgent = (Agent)Campaign.Current.ConversationManager.OneToOneConversationAgent;
            var list = NameGenerator.Current.GetNameListForCulture((CultureObject)Settlement.CurrentSettlement.Culture, characterAgent.IsFemale);
            string name = list.GetRandomElement().ToString();
            string description = GetVillagerBackground(name, characterAgent) + ". {Character} cannot join anyone, if {Player} needs recruits, should check local notable for this that can lend recruits. {Character} cannot sell anything, {Player} needs to check local market to buy supplies.";
            string[] possibleStyles = new string[] { "EXAMPLE_DIALOG_STYLE_FORMAL", "EXAMPLE_DIALOG_STYLE_DEFAULT", "EXAMPLE_DIALOG_STYLE_BLUNT", "EXAMPLE_DIALOG_STYLE_DEFAULT", "EXAMPLE_DIALOG_STYLE_DEFAULT" };
            string dialogStyle = possibleStyles.GetRandomElement();
            string pronoun = characterAgent.IsFemale ? "PRONOUN_FEMALE" : "PRONOUN_MALE";
            string gender = characterAgent.IsFemale ? "female" : "male";
            string updateVoice = "true";
            string hobbyOrInterests = Settlement.CurrentSettlement.IsTown? "townresident" : "farming";
            string characterRole = Settlement.CurrentSettlement.IsTown? "townsfolk" : "villager";
            string motivation = (Settlement.CurrentSettlement.IsTown && Mission.Current.Scene.IsAtmosphereIndoor)? (name + " just wants to enjoy the music and good food in this tavern. ") : (name + " just wants to continue working because entire life is depending on that work. Thinks there is no time for chat, needs to get back to work.");
            string stageoflife = "LIFE_STAGE_TODDLERHOOD";
            string exampleDialog = GetExampleDialogue();
            if (characterAgent.Character.Age > 45)
            {
                stageoflife = "LIFE_STAGE_LATE_ADULTHOOD";
            } else if (characterAgent.Character.Age > 35)
            {
                stageoflife = "LIFE_STAGE_MIDDLE_ADULTHOOD";
            } else if (characterAgent.Character.Age > 20)
            {
                stageoflife = "LIFE_STAGE_YOUNG_ADULTHOOD";
            }

            List<string> extrasList = new List<string>();
            string timeAndStuff = "Currently its " + CharacterEngine.GetTimeToString(CampaignTime.Now.GetHourOfDay) + " and you are in " + ((Settlement.CurrentSettlement.IsTown && Mission.Current.Scene.IsAtmosphereIndoor) ? "tavern": (Settlement.CurrentSettlement.IsTown ? "town":"village"));
            extrasList.Add(timeAndStuff);
            extrasList.Add(AppereanceData(characterAgent));
            extrasList.Add(AppereanceData(Mission.Current.MainAgent));
            if ((Settlement.CurrentSettlement.IsTown && Mission.Current.Scene.IsAtmosphereIndoor) && MBRandom.RandomFloat < 0.5f)
                extrasList.Add("{Character} feels a little drunk");

            extrasList.Add(GetWhatIWasDoing(characterAgent));

            if (MBRandom.RandomFloat < 0.3f)
                extrasList.Add("{Character} does not know any poems or tales.");

            if (Settlement.CurrentSettlement.IsTown)
                extrasList.Add("{Character} is NOT in a village, {Character} is in a town, in a city");


            if (MBRandom.RandomFloat < 0.2f)
                extrasList.Add(Corpus.CalradianTales.GetRandomElement());

            if (MBRandom.RandomFloat < 0.4f)
                extrasList.Add(Corpus.CalradianPoems.GetRandomElement());

            if (MBRandom.RandomFloat < 0.2f)
                extrasList.Add(Corpus.CalradianTales.GetRandomElement());

            if (MBRandom.RandomFloat < 0.4f)
                extrasList.Add(Corpus.CalradianSongs.GetRandomElement());

            string joinedFacts = String.Join("\",\"", extrasList);
            string formattedtemporaryFacts = $"[\"{joinedFacts}\"]";

            string payload = "{\"id\": \"{id}\", \"name\": \"{name}\", \"age\": \"{age}\",\"description\": \"{description}\", \"pronoun\": \"{pronoun}\", \"temporaryFacts\": {temporaryFacts}, \"motivation\": \"{motivation}\", \"exampleDialog\": \"{exampleDialog}\", \"exampleDialogStyle\": \"{exampleDialogStyle}\", \"lifeStage\": \"{lifeStage}\", \"hobbyOrInterests\": [ \"{hobbyOrInterests}\" ], \"characterRole\": \"{characterRole}\", \"changeGenderVoice\": {changeGenderVoice}, \"gender\": \"{gender}\" }";
            payload = payload.Replace("{id}", GetVillagerId());
            payload = payload.Replace("{name}", name);
            payload = payload.Replace("{age}", (int)(characterAgent.Character.Age) + "");
            payload = payload.Replace("{description}", description);
            payload = payload.Replace("{pronoun}", pronoun);
            payload = payload.Replace("{temporaryFacts}", formattedtemporaryFacts);
            payload = payload.Replace("{motivation}", motivation);
            payload = payload.Replace("{exampleDialog}", exampleDialog);
            payload = payload.Replace("{exampleDialogStyle}", dialogStyle);
            payload = payload.Replace("{lifeStage}", stageoflife);
            payload = payload.Replace("{hobbyOrInterests}", hobbyOrInterests);
            payload = payload.Replace("{characterRole}", characterRole);
            payload = payload.Replace("{changeGenderVoice}", updateVoice);
            payload = payload.Replace("{gender}", gender);
            return payload;
        }

        public static string AppereanceData(Hero hero)
        {
            string clothes = hero == Hero.MainHero ? "{Player} is wearing " : "{Character} is wearing ";
            for (int i = (int)EquipmentIndex.ArmorItemBeginSlot; i < (int)EquipmentIndex.ArmorItemEndSlot; i++)
            {
                var eq = hero.CivilianEquipment[(EquipmentIndex)i];
                if (eq.Item != null)
                {
                    clothes += eq.Item.Name.ToString() + ((i == (int)EquipmentIndex.ArmorItemEndSlot) ? "" : " and ");
                }
            }
            return clothes;
        }

        public static string AppereanceData(Agent hero)
        {
            string clothes = hero.IsMainAgent ? "{Player} is wearing " : "{Character} is wearing ";
            
            for (int i = (int)EquipmentIndex.ArmorItemBeginSlot; i < (int)EquipmentIndex.ArmorItemEndSlot; i++)
            {
                var eq = hero.SpawnEquipment[(EquipmentIndex)i];
                if (eq.Item != null)
                {
                    clothes += eq.Item.Name.ToString() + ((i+1 == (int)EquipmentIndex.ArmorItemEndSlot) ? "" : " and ");
                }
            }

            if (hero.IsMainAgent)
            {
                clothes += " carrying ";
                for (int i = (int)EquipmentIndex.WeaponItemBeginSlot; i < (int)EquipmentIndex.NonWeaponItemBeginSlot; i++)
                {
                    var eq = hero.SpawnEquipment[(EquipmentIndex)i];
                    if (eq.Item != null)
                    {
                        clothes += eq.Item.Name.ToString() + ((i + 1 == (int)EquipmentIndex.NonWeaponItemBeginSlot) ? "" : ",");
                    }
                }

                if (MBRandom.RandomFloat < 0.4f)
                {
                    clothes += " {Character} feel really threatened!";
                } 
            }

            return clothes;
        }

        public static string GetTimeToString(int hour)
        {
            string time;

            if (hour == 0 || hour == 24)
            {
                time = "midnight";
            }
            else if (hour >= 1 && hour < 6)
            {
                time = "early morning";
            }
            else if (hour >= 6 && hour < 12)
            {
                time = "morning";
            }
            else if (hour == 12)
            {
                time = "noon";
            }
            else if (hour >= 13 && hour < 18)
            {
                time = "afternoon";
            }
            else if (hour >= 18 && hour < 24)
            {
                time = "night";
            }
            else
            {
                time = "some time";
            }

            return time;
        }
            

        private static string GetProfession(string input, bool isVillager)
        {

            if (Corpus.backgroundMatchProfessions.Contains(input.ToLower()))
            {
                return input;
            }
            else
            {
                List<string> listToRead = isVillager ? Corpus.professionsVillager : Corpus.professionsTownsman;
                Random rand = new Random();
                int index = rand.Next(listToRead.Count);
                return listToRead[index];
            }
        }

        private static string GetExampleDialogue()
        {
            Settlement sett = PlayerEncounter.EncounterSettlement;
            if (Corpus.townsfolkDialogueStyle.ContainsKey(sett.Culture.ToString().ToLower()))
            {
                return Corpus.townsfolkDialogueStyle[sett.Culture.ToString().ToLower()];
            } else
            {
                return Corpus.townsfolkDialogueStyle["neutral"];
            }
        }

        private static string GetVillagerBackground(string name, Agent character)
        {
            string main = "{NAME} is {AGE} years old. Living in {VILLAGE}. {VILLAGE} is a {LOCATION_TYPE}, part of {FACTION}. {VILLAGE} is owned by {RULER} from {CLAN} clan in {FACTION}. {NAME}s gender is {GENDER}.";
            string background = "";
            if (Settlement.CurrentSettlement.IsVillage)
            {
                float age = character.BodyPropertiesValue.Age;

                if(age < 15)
                {
                    background = Corpus.villageKidBackgroundStory.GetRandomElement();
                } else if(age < 25)
                {
                    background = Corpus.villageYoungBackgroundStory.GetRandomElement();
                } else
                {

                    background = Corpus.villagersBackgroundStory.GetRandomElement();
                    background += ". {Character} works as " + GetProfession(background, true);

                }
            } 
            else
            {
                float age = character.BodyPropertiesValue.Age;

                if (age < 15)
                {
                    background = Corpus.townKidBackgroundStory.GetRandomElement();
                }
                else if (age < 25)
                {
                    background = Corpus.townYoungBackgroundStory.GetRandomElement();
                }
                else
                {
                    background = Corpus.townsfolkBackgroundStory.GetRandomElement();
                    background += ". {Character} works as " + GetProfession(background, false);
                }
            }

            main = main + background;
            Settlement sett = PlayerEncounter.EncounterSettlement;
            main = main.Replace("{NAME}", name);
            main = main.Replace("{LOCATION_TYPE}", sett.IsTown ? "big town" : "village");
            main = main.Replace("{FACTION}", sett.MapFaction.Name.ToString());
            main = main.Replace("{RULER}", sett.Owner.Name.ToString());
            main = main.Replace("{CLAN}", sett.OwnerClan.Name.ToString());
            main = main.Replace("{AGE}", ((int)character.BodyPropertiesValue.Age).ToString());
            main = main.Replace("{VILLAGE}", sett.Name.ToString());
            main = main.Replace("{GENDER}", character.IsFemale ? "female" : "male");
            main = main.Replace("{VILLAGE}", sett.Name.ToString());
            return main;
        }

        private static string GetBackgroundSpice(Hero hero)
        {
            string basicInformation;
            if (hero.Age > 25 && !hero.IsFemale && hero.CanLeadParty())
            {
                basicInformation = Corpus.battleBornBackground.GetRandomElement();
            }
            else
            {
                basicInformation = Corpus.clanMemberBackground.GetRandomElement();
            }

            basicInformation = basicInformation.Replace("{NAME}", hero.Name.ToString());
            basicInformation = basicInformation.Replace("{GENDER}", hero.IsFemale ? "female" : "male");
            basicInformation = basicInformation.Replace("{CULTURE}", hero.Culture.ToString());
            basicInformation = basicInformation.Replace("{AGE}", ((int)hero.Age).ToString());
            basicInformation = basicInformation.Replace("{CLAN}", hero.Clan.Name.ToString());
            basicInformation = basicInformation.Replace("{FACTION}", hero.MapFaction.Name.ToString());

            return basicInformation;
        }

        private static string GetPlayerStatus(IFaction conversationheroFaction)
        {
            string returnStr;
            if (Hero.MainHero.MapFaction.IsKingdomFaction)
            {
                if (Hero.MainHero.MapFaction.IsAtWarWith(conversationheroFaction))
                {
                    returnStr = "{player} is noble but their kingdoms are at war";
                } 
                else
                {
                    returnStr = "{player} is a noble of {faction}";
                    returnStr = returnStr.Replace("{faction}", Hero.MainHero.MapFaction.Name.ToString());
                }
            } else
            {
                if(Hero.MainHero.Gold > 15000)
                {
                    returnStr = "{player} is known to be a really wealthy person but {player} is not noble like {character}";
                } 
                else
                {
                    if(MobileParty.MainParty.MemberRoster.TotalHealthyCount > 100)
                    {
                        returnStr = "{player} is not noble but leads a big army with around {count} soldiers";
                        returnStr = returnStr.Replace("{count}", MobileParty.MainParty.MemberRoster.TotalHealthyCount.ToString());
                    } else
                    {
                        returnStr = "{player} is a common peasant and shouldnt be in the same room with nobles!";
                    }
                }
            }

            return returnStr;
        }


        public static string[] GetFacts(Hero hero)
        {
            List<string> facts = new List<string>();
            
            facts.Add("{character} knows that name of the {Player} is {player}");
            facts.Add("{character} knows that " + GetPlayerStatus(hero.MapFaction));

            string fact;
            if (hero.Father != null)
            {
                fact = "{character} has a father, " + hero.Father.Name;
                if (!hero.Father.IsAlive) fact += " but he is deceased.";
                else fact += " and he is still alive at age " + (int)hero.Father.Age + ".";
                facts.Add(fact);
            }

            if (hero.Mother != null)
            {
                fact = "{character} has a mother, " + hero.Mother.Name;
                if (!hero.Mother.IsAlive) fact += " but she is deceased.";
                else fact += " and she is still alive at age " + (int)hero.Mother.Age + ".";
                facts.Add(fact);
            }

            if (hero.Siblings.Count() > 0)
            {
                fact = "{character} has siblings ";
                foreach (Hero sibling in hero.Siblings)
                {
                    fact += (sibling.IsFemale ? "sister" : "brother") + " " + sibling.Name + ",";
                    if (!sibling.IsAlive) fact += " but " + sibling.Name + " is deceased, ";
                }
                facts.Add(fact);
            }

            if (hero.Children.Count() > 0)
            {
                fact = "{character} has children ";
                foreach (Hero child in hero.Children)
                {
                    fact += (child.IsFemale ? "daughter" : "son") + " " + child.Name + ",";
                    if (!child.IsAlive) fact += " but " + child.Name + " is deceased, ";
                    else fact += " at age around " + ((int)child.Age);
                }
                facts.Add(fact);
            }

            if (hero.Spouse != null)
            {
                fact = "{character} is married with ";
                if (hero.Spouse == Hero.MainHero) fact += "{player}.";
                else fact += hero.Spouse.Name + " from " + hero.Spouse.Clan.Name;
                facts.Add(fact);
            }
            else
            {
                fact = "{character} is single";
                facts.Add(fact);
            }

            fact = "{character} is from " + hero.Culture.Name + " culture";
            facts.Add(fact);

            if (hero.Clan.Kingdom != null && hero.IsFactionLeader)
            {
                facts.Add(Corpus.rulerFact.GetRandomElement());
            }
            else
            {
                facts.Add(Corpus.politicsFact.GetRandomElement());
            }

            if (hero.Clan != null)
            {
                fact = "{character} is from " + hero.Clan.Name + " Clan and " + hero.Clan.Name + "'s wealth can described as " + CampaignUIHelper.GetClanWealthStatusText(hero.Clan);
                facts.Add(fact);

                if (hero.Clan.Leader != hero)
                {
                    if (hero.Age > 25 && !hero.IsFemale && hero.CanLeadParty())
                    {
                        fact = "{character}'s role in clan is just a normal member, {character} cannot lead any warband";

                        if (hero.IsFemale)
                            facts.Add(Corpus.spouseFacts.GetRandomElement());
                    }
                    else
                    {
                        fact = "{character}'s role in clan is just a normal member but can lead warband when in need";
                        facts.Add(Corpus.battleLikeFacts.GetRandomElement());
                        facts.Add(Corpus.commanderAndPoliticsFacts.GetRandomElement());
                    }
                }
                else
                {
                    fact = "{character}'s role in clan is clan leader";
                }

                facts.Add(fact);
            }

            facts.Add(Corpus.youngFacts.GetRandomElement());
            facts.Add(Corpus.nobleHobbies.GetRandomElement());
            facts.Add(Corpus.bannerlordSpecificFacts.GetRandomElement());
            facts.Add(Corpus.thoughtsFacts.GetRandomElement());
            facts.Add(Corpus.thoughtsFacts.GetRandomElement());
            facts.Add(Corpus.thoughtsFacts.GetRandomElement());
            facts.Add("As rumor:" + Corpus.nobleRumors.GetRandomElement());
            facts.Add("As rumor:" + Corpus.nobleRumors.GetRandomElement());
            facts.Add(Corpus.innerPoliticsFacts.GetRandomElement());


            if (MBRandom.RandomFloat < 0.2f)
                facts.Add(Corpus.CalradianTales.GetRandomElement());

            if(MBRandom.RandomFloat < 0.4f)
                facts.Add(Corpus.CalradianPoems.GetRandomElement());

            for (int i = 0; i < facts.Count; i++)
            {
                fact = facts[i];
                fact = fact.Replace("{character}", hero.Name.ToString());
                fact = fact.Replace("{player}", Hero.MainHero.Name.ToString());
                Clan rival = GetRivalClan(hero.Clan);
                fact = fact.Replace("{ruler}", hero.MapFaction == null? hero.Clan.Leader.Name.ToString() : hero.MapFaction.Leader.Name.ToString());
                if(rival != null) fact = fact.Replace("{rival}", rival.Name.ToString());
                fact = fact.Replace("{culture}", Kingdom.All.GetRandomElement().Culture.Name.ToString());
                if (rival != null) fact = fact.Replace("{rival_hero}", rival.Heroes.GetRandomElement().Name.ToString());
                facts[i] = fact;
            }

            facts.Add("{Character} does not need any help.");
            facts.Add("{Character} does not consider rumored clans as it's enemy but just as a clan to be watched out.");
            facts.Add("{Character} does not want anything from {Player}.");
            facts.Add("{Character} cannot sell anything.");
            facts.Add("{Character} cannot join anyone.");
            facts.Add("{Character} cannot make deals at the moment.");
            facts.Add("{Character} cannot sell you anything.");

            return facts.ToArray();
        }

        private static Clan GetRivalClan(Clan mainClan)
        {
            Clan rivalClan = null;

            if (mainClan.Kingdom != null)
            {
                foreach (Clan c in mainClan.Kingdom.Clans)
                {
                    if (c != mainClan)
                    {
                        if (rivalClan == null)
                        {
                            rivalClan = c;
                        }
                        else
                        {
                            if (rivalClan.Tier < c.Tier)
                            {
                                rivalClan = c;
                            }
                        }
                    }
                }
            }
          

            if(rivalClan == null)
            {
                return Clan.All.GetRandomElementWithPredicate(c => c != mainClan && c.Heroes.Count > 0);
            }

            return rivalClan;
        }

        public static string GetVillagerId()
        {
            return "unique-villager-fact";
        }

        public static string GetUniqueId(Hero hero)
        {
            string src = hero.Id.ToString() + hero.Name.ToString();
            byte[] stringbytes = Encoding.UTF8.GetBytes(src);
            byte[] hashedBytes = new System.Security.Cryptography
                .SHA1CryptoServiceProvider()
                .ComputeHash(stringbytes);
            Array.Resize(ref hashedBytes, 16);
            return new Guid(hashedBytes).ToString();
        }

        public static string GetCommonKnowledgeOnKingdom(Kingdom kingdom)
        {
            string result = "{FACTION} is ruled by {RULER}, member clans are {CLANS}";
            result = result.Replace("{FACTION}", kingdom.Name.ToString());
            result = result.Replace("{RULER}", kingdom.Leader.Name.ToString());
            string clans = "";
            foreach (Clan c in kingdom.Clans)
            {
                clans += c.Name.ToString() + ",";
            }
            result = result.Replace("{CLANS}", clans);
            return result.Substring(0, Math.Min(result.Length, 299));
        }
    }
}
