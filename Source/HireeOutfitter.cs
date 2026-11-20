using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CommRecruiter
{
    /// <summary>Strips a freshly generated hiree and dresses it in a cloth T-shirt and pants. Returns the issued apparel IDs.</summary>
    public static class HireeOutfitter
    {
        private static readonly string[] Issued = { "Apparel_BasicShirt", "Apparel_Pants" };

        public static List<int> Outfit(Pawn pawn)
        {
            pawn.apparel?.DestroyAll();
            pawn.equipment?.DestroyAllEquipment();
            pawn.inventory?.DestroyAll();

            var ids = new List<int>();
            if (pawn.apparel == null) return ids;
            foreach (string defName in Issued)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null || !ApparelUtility.HasPartsToWear(pawn, def) || !def.apparel.PawnCanWear(pawn)) continue;
                var apparel = (Apparel)ThingMaker.MakeThing(def, ThingDefOf.Cloth);
                apparel.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
                apparel.HitPoints = apparel.MaxHitPoints;
                pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                ids.Add(apparel.thingIDNumber);
            }
            return ids;
        }

        /// <summary>True when the pawn carries or wears anything besides its issued clothes.</summary>
        public static bool HasExtraGear(Pawn pawn, List<int> issued)
        {
            if (pawn.equipment != null && pawn.equipment.AllEquipmentListForReading.Count > 0) return true;
            if (pawn.inventory != null && pawn.inventory.innerContainer.Count > 0) return true;
            if (pawn.apparel != null)
                foreach (Apparel a in pawn.apparel.WornApparel)
                    if (!issued.Contains(a.thingIDNumber)) return true;
            return false;
        }
    }
}
