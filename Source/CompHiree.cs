using RimWorld;
using Verse;

namespace CommRecruiter
{
    /// <summary>Shows the contract in the inspect pane. Stores nothing; the record lives in the game component.</summary>
    public class CompHiree : ThingComp
    {
        public override string CompInspectStringExtra()
        {
            HireeRecord r = GameComponent_CommRecruiter.Instance?.RecordFor(parent as Pawn);
            if (r == null) return null;
            string s = "CR_InspectHired".Translate(r.homeFaction.Named("FACTION"),
                UnityEngine.Mathf.Max(0, r.TicksLeft).ToStringTicksToPeriod().Named("TIME"));
            if (r.retained) s += "\n" + "CR_InspectRetained".Translate();
            else s += "\n" + "CR_InspectHappy".Translate((r.happyTicks / (float)Tuning.TicksPerDay).ToString("0.0").Named("DAYS"));
            return s;
        }
    }
}
