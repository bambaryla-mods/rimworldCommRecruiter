using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CommRecruiter
{
    /// <summary>Kind tiers (per-faction quartiles of distinct combat power), goodwill cap and what a goodwill level unlocks.</summary>
    public static class GoodwillTiers
    {
        public const int TierCount = 4;

        private static bool wd2Looked;
        private static MethodInfo wd2CapMethod;
        private static bool wd2Failed;

        public static int GoodwillCap()
        {
            if (!wd2Looked)
            {
                wd2Looked = true;
                Type t = GenTypes.GetTypeInAnyAssembly("TSA_WorldDomination.GoodwillCapUtility");
                wd2CapMethod = t?.GetMethod("MaxGoodwillCap", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (wd2CapMethod == null && ModsConfig.IsActive("TSA.WorldDominationExperimental"))
                    Log.Warning("[Comm Recruiter] World Domination 2 is active but its goodwill cap could not be read. Using 100.");
            }
            if (wd2CapMethod == null || wd2Failed) return Tuning.VanillaGoodwillCap;
            try
            {
                return Math.Max(Tuning.VanillaGoodwillCap, Convert.ToInt32(wd2CapMethod.Invoke(null, null)));
            }
            catch (Exception e)
            {
                wd2Failed = true;
                Log.Warning("[Comm Recruiter] Reading World Domination 2's goodwill cap failed, using 100: " + e.Message);
                return Tuning.VanillaGoodwillCap;
            }
        }

        public static int Tier4Threshold() =>
            Math.Max(Tuning.Tier3Goodwill, Mathf.CeilToInt(Tuning.Tier4CapFraction * GoodwillCap()));

        /// <summary>Goodwill needed to unlock a tier (tier 1 needs only a non-hostile relation).</summary>
        public static int ThresholdFor(int tier)
        {
            switch (tier)
            {
                case 1: return int.MinValue;
                case 2: return Tuning.Tier2Goodwill;
                case 3: return Tuning.Tier3Goodwill;
                default: return Tier4Threshold();
            }
        }

        public static int UnlockedTier(int goodwill)
        {
            if (goodwill >= Tier4Threshold()) return 4;
            if (goodwill >= Tuning.Tier3Goodwill) return 3;
            if (goodwill >= Tuning.Tier2Goodwill) return 2;
            return 1;
        }

        public static int UnlockedTier(Faction f) => UnlockedTier(f.PlayerGoodwill);

        public static int RosterCap(int goodwill)
        {
            float t = Mathf.Clamp01(goodwill / (float)Tier4Threshold());
            return Mathf.RoundToInt(Mathf.Lerp(Tuning.MinRosterTotal, Tuning.MaxRosterTotal, t));
        }

        /// <summary>Tier of every eligible kind of a faction: rank split of the distinct combat powers.</summary>
        public static Dictionary<PawnKindDef, int> KindTiers(List<PawnKindDef> eligible)
        {
            List<float> distinct = eligible.Select(k => k.combatPower).Distinct().OrderBy(cp => cp).ToList();
            int n = distinct.Count;
            var result = new Dictionary<PawnKindDef, int>();
            foreach (PawnKindDef k in eligible)
                result[k] = 4 * distinct.IndexOf(k.combatPower) / n + 1;
            return result;
        }
    }
}
