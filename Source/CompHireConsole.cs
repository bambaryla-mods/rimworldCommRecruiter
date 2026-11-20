using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CommRecruiter
{
    [StaticConstructorOnStartup]
    public class CompHireConsole : ThingComp
    {
        private static readonly Texture2D Icon =
            ContentFinder<Texture2D>.Get("UI/Commands/CR_HirePawns", reportFailure: false) ?? TexCommand.SquadAttack;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer || !parent.Spawned) yield break;
            var console = parent as Building_CommsConsole;
            var cmd = new Command_Action
            {
                defaultLabel = "CR_HirePawns".Translate(),
                defaultDesc = "CR_HirePawnsDesc".Translate(),
                icon = Icon,
                action = () => Find.WindowStack.Add(new Dialog_HirePawns(parent.Map)),
            };
            if (console != null && !console.CanUseCommsNow)
            {
                cmd.Disable(parent.Map.gameConditionManager.ElectricityDisabled(parent.Map)
                    ? "CannotUseSolarFlare".Translate()
                    : "CannotUseNoPower".Translate());
            }
            else if (HireUtility.HireFactions().Count == 0)
            {
                cmd.Disable("CR_NoFaction".Translate());
            }
            yield return cmd;
        }
    }
}
