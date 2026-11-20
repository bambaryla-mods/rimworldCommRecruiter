namespace CommRecruiter
{
    public static class Tuning
    {
        public const int TicksPerDay = 60000;

        // Roster
        public const int MaxPerKind = 5;
        public const int MinRosterTotal = 3;
        public const int MaxRosterTotal = 10;
        public const int RosterRefreshDays = 7;

        // Goodwill tiers
        public const int Tier2Goodwill = 25;
        public const int Tier3Goodwill = 75;
        public const float Tier4CapFraction = 0.9f;
        public const int VanillaGoodwillCap = 100;

        // Delivery
        public const float MinArrivalDays = 2f;
        public const float MaxArrivalDays = 3f;

        // Contract
        public const int ContractTicks = 15 * TicksPerDay;
        public const int StayLetterTick = 14 * TicksPerDay;
        public const int GearWarningTick = ContractTicks - TicksPerDay / 2;
        public const int TickInterval = 250;
        public const float HappyMoodThreshold = 0.6f;
        public const int HappyTicksForStay = (int)(7.5f * TicksPerDay);

        // Goodwill penalties
        public const float DeathGoodwillPerCp = 0.05f;
        public const float RetainGoodwillPerCp = 0.02f;
        public const float ShortfallGoodwillPerSilver = 0.05f;
    }
}
