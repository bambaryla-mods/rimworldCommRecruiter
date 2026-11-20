using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CommRecruiter
{
    /// <summary>A paid order on its way. Pawns are generated on arrival.</summary>
    public class HireOrder : IExposable
    {
        public Faction faction;
        public Map map;
        public int arrivalTick;
        public Dictionary<PawnKindDef, int> counts = new Dictionary<PawnKindDef, int>();

        public bool DropPods => faction.def.techLevel >= TechLevel.Spacer;

        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref arrivalTick, "arrivalTick");
            Scribe_Collections.Look(ref counts, "counts", LookMode.Def, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                counts ??= new Dictionary<PawnKindDef, int>();
                counts.RemoveAll(kv => kv.Key == null);
            }
        }
    }
}
