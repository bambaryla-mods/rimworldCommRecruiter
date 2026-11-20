using System.Collections.Generic;
using Verse;

namespace CommRecruiter
{
    public class ChoiceLetter_HireeStay : ChoiceLetter
    {
        public Pawn pawn;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }
                HireeRecord r = GameComponent_CommRecruiter.Instance?.RecordFor(pawn);
                bool valid = r != null && !r.retained && pawn != null && !pawn.Dead;

                var yes = new DiaOption("CR_StayYes".Translate(valid ? GameComponent_CommRecruiter.RetainCost(r) : 0))
                {
                    action = () =>
                    {
                        GameComponent_CommRecruiter.Instance.Retain(r);
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true,
                };
                var no = new DiaOption("CR_StayNo".Translate())
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true,
                };
                if (!valid)
                {
                    yes.Disable(null);
                    no.Disable(null);
                }
                yield return yes;
                yield return no;
                if (!valid) yield return Option_Close;
                else yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
        }
    }
}
