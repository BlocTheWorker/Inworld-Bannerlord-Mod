using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Inworld.Helper
{
    internal class InCampaignHelper
    {
        public struct WarPeaceState
        {
            public IFaction kingdom;
            public IFaction kingdom2;
            public bool IsWar;
            public CampaignTime StartTime;

            public static bool operator ==(WarPeaceState c1, WarPeaceState c2)
            {
                return ((c1.kingdom == c2.kingdom && c1.kingdom2 == c2.kingdom2) || (c1.kingdom == c2.kingdom2 && c1.kingdom == c2.kingdom2));
            }

            public static bool operator !=(WarPeaceState c1, WarPeaceState c2)
            {
                return !((c1.kingdom == c2.kingdom && c1.kingdom2 == c2.kingdom2) || (c1.kingdom == c2.kingdom2 && c1.kingdom == c2.kingdom2));
            }

            public override bool Equals(object obj)
            {
                WarPeaceState c1 = this;
                WarPeaceState c2 = (WarPeaceState)obj;
                return ((c1.kingdom == c2.kingdom && c1.kingdom2 == c2.kingdom2) || (c1.kingdom == c2.kingdom2 && c1.kingdom == c2.kingdom2));
            }
        }

        public static async Task<bool> MakeCallToUpdateCommonInformation(string type, string data)
        {
            string payload = "{ \"type\": \"" + type + "\", \"information\": " + data + " }";

            using (var client = new HttpClient())
            {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("http://127.0.0.1:3000/updateCommonKnowledge", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return true;
                } else
                {
                    return false;
                }
            }
        }


        public static async Task<bool> CheckVersion(string id)
        {
            string payload = "{ \"id\": \"" + id + "\" }";

            using (var client = new HttpClient())
            {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("http://127.0.0.1:3000/createSave", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    JObject obj = JObject.Parse(result);
                    return bool.Parse(obj["isMatch"].ToString());
                }
                else
                {
                    return false;
                }
            }
        }

        public static List<MobileParty> GetMobilePartiesAroundPosition(Vec2 pos, float range)
        {
            List<MobileParty> list = new List<MobileParty>();
            var data = MobileParty.StartFindingLocatablesAroundPosition(pos, range);
            for (MobileParty mobileParty = MobileParty.FindNextLocatable(ref data); mobileParty != null; mobileParty = MobileParty.FindNextLocatable(ref data)) {
                if(!list.Contains(mobileParty))
                    list.Add(mobileParty);
            }
            return list;
        }

        public static List<Settlement> GetSettlementsAroundPosition(Vec2 pos, float range)
        {
            List<Settlement> list = new List<Settlement>();
            var data = Settlement.StartFindingLocatablesAroundPosition(pos, range);
            for (Settlement settlement = Settlement.FindNextLocatable(ref data); settlement != null; settlement = Settlement.FindNextLocatable(ref data))
            {
                if (!list.Contains(settlement))
                    list.Add(settlement);
            }
            return list;
        }

        public static string TrimSentence(string sentence, int maxLength)
        {
            if (sentence.Length <= maxLength)
            {
                return sentence;
            }
            int lastPeriodIndex = sentence.LastIndexOf('.', maxLength);
            if (lastPeriodIndex == -1)
            {
                lastPeriodIndex = maxLength;
            }
            return sentence.Substring(0, lastPeriodIndex + 1);
        }
    }
}
