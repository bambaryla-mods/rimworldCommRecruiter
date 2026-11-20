using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CommRecruiter
{
    /// <summary>One hiree on contract.</summary>
    public class HireeRecord : IExposable
    {
        public Pawn pawn;
        public Faction homeFaction;
        /// <summary>Hidden contract quest that marks the pawn as a helper from its home faction.</summary>
        public Quest quest;
        public float combatPower;
        public int arrivalTick;
        public int batchId;
        public int happyTicks;
        public List<int> issuedApparelIds = new List<int>();
        public bool stayLetterSent;
        public bool retained;
        public bool gearWarned;

        public int Elapsed => Find.TickManager.TicksGame - arrivalTick;
        public int TicksLeft => Tuning.ContractTicks - Elapsed;
        public int Price => UnityEngine.Mathf.CeilToInt(combatPower);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref homeFaction, "homeFaction");
            Scribe_References.Look(ref quest, "quest");
            Scribe_Values.Look(ref combatPower, "combatPower");
            Scribe_Values.Look(ref arrivalTick, "arrivalTick");
            Scribe_Values.Look(ref batchId, "batchId");
            Scribe_Values.Look(ref happyTicks, "happyTicks");
            Scribe_Collections.Look(ref issuedApparelIds, "issuedApparelIds", LookMode.Value);
            Scribe_Values.Look(ref stayLetterSent, "stayLetterSent");
            Scribe_Values.Look(ref retained, "retained");
            Scribe_Values.Look(ref gearWarned, "gearWarned");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                issuedApparelIds ??= new List<int>();
        }
    }
}
