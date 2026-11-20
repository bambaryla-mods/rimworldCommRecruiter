using RimWorld;
using Verse;

namespace CommRecruiter
{
    /// <summary>
    /// Adds the console comp to every comms console def (including subclasses) and the hiree comp to every humanlike race.
    /// Done in code rather than an XML patch so modded consoles and races without a comps node are covered.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DefInjector
    {
        static DefInjector()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass != null && typeof(Building_CommsConsole).IsAssignableFrom(def.thingClass))
                    AddComp(def, typeof(CompHireConsole));
                else if (def.race != null && def.race.Humanlike)
                    AddComp(def, typeof(CompHiree));
            }
        }

        private static void AddComp(ThingDef def, System.Type compClass)
        {
            def.comps ??= new System.Collections.Generic.List<CompProperties>();
            if (def.comps.Any(c => c.compClass == compClass)) return;
            def.comps.Add(new CompProperties { compClass = compClass });
        }
    }
}
