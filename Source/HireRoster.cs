using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CommRecruiter
{
    public class HireRoster : IExposable
    {
        public Faction faction;
        public Dictionary<PawnKindDef, int> stock = new Dictionary<PawnKindDef, int>();
        public int generatedTick = -1;
        public int generatedTier;

        public int RestockTick => generatedTick + Tuning.RosterRefreshDays * Tuning.TicksPerDay;

        private static readonly Dictionary<FactionDef, List<PawnKindDef>> eligibleCache = new Dictionary<FactionDef, List<PawnKindDef>>();

        /// <summary>Eligible kinds depend only on defs, so they are cached per faction def.</summary>
        public static List<PawnKindDef> EligibleKinds(Faction f)
        {
            if (!eligibleCache.TryGetValue(f.def, out var list))
                eligibleCache[f.def] = list = ComputeEligibleKinds(f.def);
            return list;
        }

        private static List<PawnKindDef> ComputeEligibleKinds(FactionDef fd)
        {
            var leaders = fd.fixedLeaderKinds;
            var result = new List<PawnKindDef>();
            if (fd.pawnGroupMakers == null) return result;
            foreach (PawnGroupMaker gm in fd.pawnGroupMakers)
            {
                foreach (var list in new[] { gm.options, gm.traders, gm.carriers, gm.guards })
                {
                    if (list == null) continue;
                    foreach (PawnGenOption opt in list)
                    {
                        PawnKindDef k = opt.kind;
                        if (k == null || result.Contains(k)) continue;
                        if (k.RaceProps == null || !k.RaceProps.Humanlike) continue;
                        if (k.combatPower <= 0f || k.factionLeader) continue;
                        if (leaders != null && leaders.Contains(k)) continue;
                        if (k.titleRequired != null || k.minTitleRequired != null || !k.titleSelectOne.NullOrEmpty()) continue;
                        result.Add(k);
                    }
                }
            }
            return result;
        }

        public bool NeedsRegeneration(int unlockedTier) =>
            generatedTick < 0 || Find.TickManager.TicksGame > RestockTick || unlockedTier > generatedTier;

        public void Generate()
        {
            stock.Clear();
            generatedTick = Find.TickManager.TicksGame;
            int goodwill = faction.PlayerGoodwill;
            generatedTier = GoodwillTiers.UnlockedTier(goodwill);

            List<PawnKindDef> eligible = EligibleKinds(faction);
            if (eligible.Count == 0) return;
            Dictionary<PawnKindDef, int> tiers = GoodwillTiers.KindTiers(eligible);
            float cpMin = eligible.Min(k => k.combatPower);

            List<PawnKindDef> unlocked = eligible.Where(k => tiers[k] <= generatedTier).ToList();
            foreach (PawnKindDef k in unlocked)
            {
                int max = Mathf.Max(1, Mathf.RoundToInt(Tuning.MaxPerKind * cpMin / k.combatPower));
                int n = Rand.RangeInclusive(0, max);
                if (n > 0) stock[k] = n;
            }

            int cap = GoodwillTiers.RosterCap(goodwill);
            while (stock.Values.Sum() > cap)
            {
                PawnKindDef k = stock.Keys.RandomElementByWeight(x => x.combatPower);
                if (--stock[k] <= 0) stock.Remove(k);
            }

            if (stock.Count == 0)
                stock[unlocked.MinBy(k => k.combatPower)] = 1;
        }

        public int StockOf(PawnKindDef k) => stock.TryGetValue(k, out int n) ? n : 0;

        public void Take(PawnKindDef k, int n)
        {
            int left = StockOf(k) - n;
            if (left > 0) stock[k] = left;
            else stock.Remove(k);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Collections.Look(ref stock, "stock", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref generatedTick, "generatedTick", -1);
            Scribe_Values.Look(ref generatedTier, "generatedTier");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                stock ??= new Dictionary<PawnKindDef, int>();
                stock.RemoveAll(kv => kv.Key == null);
            }
        }
    }
}
