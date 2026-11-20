using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace CommRecruiter
{
    public static class HireUtility
    {
        public static int BeaconSilver(Map map) =>
            map == null ? 0 : TradeUtility.AllLaunchableThingsForTrade(map).Where(t => t.def == ThingDefOf.Silver).Sum(t => t.stackCount);

        public static int PriceOf(PawnKindDef k) => Mathf.CeilToInt(k.combatPower);

        public static bool IsHireFaction(Faction f) =>
            f != null && !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction
            && !f.HostileTo(Faction.OfPlayer);

        public static List<Faction> HireFactions() =>
            Find.FactionManager.AllFactionsVisibleInViewOrder
                .Where(f => IsHireFaction(f) && HireRoster.EligibleKinds(f).Count > 0)
                .ToList();

        public static void AffectGoodwill(Faction f, int change, HistoryEventDef reason, GlobalTargetInfo? target = null)
        {
            if (f == null || change == 0 || f.IsPlayer || f.defeated) return;
            f.TryAffectGoodwillWith(Faction.OfPlayer, change, canSendMessage: true, canSendHostilityLetter: true, reason, target);
        }
    }
}
