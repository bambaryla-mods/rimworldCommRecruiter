using LudeonTK;
using Verse;

namespace CommRecruiter
{
    public static class DebugActions
    {
        private static GameComponent_CommRecruiter C => GameComponent_CommRecruiter.Instance;

        [DebugAction("Comm Recruiter", "Reroll all rosters", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RerollRosters()
        {
            foreach (HireRoster r in C.Rosters) r.Generate();
        }

        [DebugAction("Comm Recruiter", "Finish arrivals now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FinishArrivals()
        {
            foreach (HireOrder o in C.Orders) o.arrivalTick = Find.TickManager.TicksGame;
        }

        [DebugAction("Comm Recruiter", "Skip contracts to day 14", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SkipTo14() => SkipTo(Tuning.StayLetterTick);

        [DebugAction("Comm Recruiter", "Skip contracts to day 14.5", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SkipTo145() => SkipTo(Tuning.GearWarningTick);

        [DebugAction("Comm Recruiter", "Skip contracts to day 15", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SkipTo15() => SkipTo(Tuning.ContractTicks);

        [DebugAction("Comm Recruiter", "Make hirees happy (8 days)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeHappy()
        {
            foreach (HireeRecord r in C.Records) r.happyTicks = 8 * Tuning.TicksPerDay;
        }

        [DebugAction("Comm Recruiter", "Log goodwill tiers", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogTiers()
        {
            Log.Message($"[Comm Recruiter] goodwill cap {GoodwillTiers.GoodwillCap()}, tier 4 at {GoodwillTiers.Tier4Threshold()}");
            foreach (RimWorld.Faction f in HireUtility.HireFactions())
            {
                var tiers = GoodwillTiers.KindTiers(HireRoster.EligibleKinds(f));
                string kinds = string.Join(", ", System.Linq.Enumerable.Select(tiers, kv => $"{kv.Key.defName}({kv.Key.combatPower})=T{kv.Value}"));
                Log.Message($"  {f.Name}: goodwill {f.PlayerGoodwill}, unlocked T{GoodwillTiers.UnlockedTier(f)}, cap {GoodwillTiers.RosterCap(f.PlayerGoodwill)}: {kinds}");
            }
        }

        private static void SkipTo(int elapsed)
        {
            foreach (HireeRecord r in C.Records) r.arrivalTick = Find.TickManager.TicksGame - elapsed;
        }
    }
}
