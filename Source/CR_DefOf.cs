using RimWorld;
using Verse;

namespace CommRecruiter
{
    [DefOf]
    public static class CR_DefOf
    {
        public static LetterDef CR_HireeStayLetter;
        public static QuestScriptDef CR_HireContract;
        public static HistoryEventDef CR_HireeDied;
        public static HistoryEventDef CR_HireeRetained;
        public static HistoryEventDef CR_HireeEnslaved;
        public static HistoryEventDef CR_HireeForcedStay;

        static CR_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(CR_DefOf));
    }
}
