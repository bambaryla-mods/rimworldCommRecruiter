using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CommRecruiter
{
    public class Dialog_HirePawns : Window
    {
        private const float FactionListWidth = 280f;
        private const float FactionRowHeight = 58f;
        private const float RowHeight = 30f;
        private const float FooterHeight = 70f;

        private readonly Map map;
        private Faction selected;
        private readonly Dictionary<PawnKindDef, int> counts = new Dictionary<PawnKindDef, int>();
        private Vector2 factionScroll;
        private Vector2 rosterScroll;

        public override Vector2 InitialSize => new Vector2(1100f, 680f);

        // A themed look draws its own frame: the vanilla background, shadow, margin and close X are off while it is in
        // use, and come back on the next frame if it is dropped.
        private bool themedFlags;
        private bool vanillaCloseX, vanillaBackground, vanillaShadow;

        protected override float Margin => themedFlags ? 0f : base.Margin;

        public Dialog_HirePawns(Map map)
        {
            this.map = map;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            ApplyLook();
        }

        private void ApplyLook()
        {
            bool themed = HireLook.Current.Themed;
            if (themed == themedFlags) return;
            if (themed)
            {
                vanillaCloseX = doCloseX;
                vanillaBackground = doWindowBackground;
                vanillaShadow = drawShadow;
                doCloseX = doWindowBackground = drawShadow = false;
            }
            else
            {
                doCloseX = vanillaCloseX;
                doWindowBackground = vanillaBackground;
                drawShadow = vanillaShadow;
            }
            themedFlags = themed;
        }

        public override void DoWindowContents(Rect inRect)
        {
            ApplyLook();
            HireLook look = HireLook.Current;
            try
            {
                Contents(look.BeginWindow(this, inRect, "CR_WindowTitle".Translate()));
            }
            catch (System.Exception e)
            {
                HireLook.Blame(e);
                throw;
            }
            finally
            {
                try
                {
                    look.EndWindow(this, inRect);
                }
                catch (System.Exception e)
                {
                    HireLook.Blame(e);
                }
            }
        }

        // Everything below the title.
        private void Contents(Rect inRect)
        {
            List<Faction> factions = HireUtility.HireFactions();
            if (selected != null && !factions.Contains(selected))
            {
                selected = null;
                counts.Clear();
            }
            if (selected == null && factions.Count > 0) selected = factions[0];

            Rect body = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - FooterHeight);
            DrawFactionList(new Rect(body.x, body.y, FactionListWidth, body.height), factions);
            Rect right = new Rect(body.x + FactionListWidth + 12f, body.y, body.width - FactionListWidth - 12f, body.height);

            HireRoster roster = null;
            List<PawnKindDef> eligible = null;
            Dictionary<PawnKindDef, int> tiers = null;
            int unlocked = 0;
            if (selected != null)
            {
                roster = GameComponent_CommRecruiter.Instance.RosterFor(selected);
                eligible = HireRoster.EligibleKinds(selected);
                tiers = GoodwillTiers.KindTiers(eligible);
                unlocked = GoodwillTiers.UnlockedTier(selected);
                ClampCounts(roster, tiers, unlocked);
                DrawRoster(right, roster, eligible, tiers, unlocked);
            }

            DrawFooter(new Rect(inRect.x, inRect.yMax - FooterHeight + 8f, inRect.width, FooterHeight - 8f), roster);
        }

        private void ClampCounts(HireRoster roster, Dictionary<PawnKindDef, int> tiers, int unlocked)
        {
            foreach (PawnKindDef k in counts.Keys.ToList())
            {
                int max = tiers.TryGetValue(k, out int t) && t <= unlocked ? roster.StockOf(k) : 0;
                if (counts[k] > max) counts[k] = max;
                if (counts[k] <= 0) counts.Remove(k);
            }
        }

        private void DrawFactionList(Rect rect, List<Faction> factions)
        {
            HireLook look = HireLook.Current;
            look.Section(rect);
            Rect outRect = rect.ContractedBy(4f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, factions.Count * FactionRowHeight);
            Widgets.BeginScrollView(outRect, ref factionScroll, view);
            float y = 0f;
            foreach (Faction f in factions)
            {
                Rect row = new Rect(0f, y, view.width, FactionRowHeight - 2f);
                look.Row(row, f == selected, hoverable: true, alternate: false);

                Rect icon = new Rect(row.x + 4f, row.y + 4f, 28f, 28f);
                GUI.color = f.Color;
                GUI.DrawTexture(icon, f.def.FactionIcon);
                GUI.color = Color.white;

                Widgets.Label(new Rect(icon.xMax + 6f, row.y + 2f, row.width - icon.width - 12f, 24f), f.Name.Truncate(row.width - 44f));
                GUI.color = look.TextDim;
                Text.Font = GameFont.Tiny;
                string arrival = f.def.techLevel >= TechLevel.Spacer ? "CR_ArrivalPods".Translate() : "CR_ArrivalFoot".Translate();
                Widgets.Label(new Rect(icon.xMax + 6f, row.y + 24f, row.width - icon.width - 12f, 32f),
                    "CR_FactionLine".Translate(f.PlayerGoodwill.ToStringWithSign().Named("GOODWILL"),
                        f.def.techLevel.ToStringHuman().CapitalizeFirst().Named("TECH"), arrival.Named("ARRIVAL")));
                Text.Font = GameFont.Small;
                GUI.color = Color.white;

                if (Widgets.ButtonInvisible(row) && f != selected)
                {
                    selected = f;
                    counts.Clear();
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
                y += FactionRowHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawRoster(Rect rect, HireRoster roster, List<PawnKindDef> eligible, Dictionary<PawnKindDef, int> tiers, int unlocked)
        {
            HireLook look = HireLook.Current;
            float y = rect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 32f), selected.Name);
            Text.Font = GameFont.Small;
            y += 32f;

            string tierLine = "CR_TierLine".Translate(unlocked, GoodwillTiers.TierCount);
            if (unlocked < GoodwillTiers.TierCount)
                tierLine += "  " + "CR_NextTier".Translate(GoodwillTiers.ThresholdFor(unlocked + 1));
            int restockIn = roster.RestockTick - Find.TickManager.TicksGame;
            tierLine += "  " + "CR_Restocks".Translate(Mathf.Max(0, restockIn).ToStringTicksToPeriod());
            GUI.color = look.TextDim;
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), tierLine);
            GUI.color = Color.white;
            y += 28f;

            // Column layout
            float wLabel = rect.width - 560f;
            float xCp = rect.x + wLabel, xPrice = xCp + 70f, xAvail = xPrice + 70f, xAdj = xAvail + 80f, xSub = xAdj + 260f;
            GUI.color = look.TextDim;
            Widgets.Label(new Rect(rect.x, y, wLabel, 24f), "CR_ColKind".Translate());
            Widgets.Label(new Rect(xCp, y, 70f, 24f), "CR_ColCp".Translate());
            Widgets.Label(new Rect(xPrice, y, 70f, 24f), "CR_ColPrice".Translate());
            Widgets.Label(new Rect(xAvail, y, 80f, 24f), "CR_ColAvailable".Translate());
            Widgets.Label(new Rect(xSub, y, 80f, 24f), "CR_ColSubtotal".Translate());
            GUI.color = Color.white;
            y += 26f;
            look.Divider(rect.x, y - 2f, rect.width);

            List<PawnKindDef> hireable = eligible.Where(k => tiers[k] <= unlocked && roster.StockOf(k) > 0)
                .OrderBy(k => k.combatPower).ToList();
            List<PawnKindDef> locked = eligible.Where(k => tiers[k] > unlocked).OrderBy(k => k.combatPower).ToList();

            Rect outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, (hireable.Count + locked.Count) * RowHeight + 8f);
            Widgets.BeginScrollView(outRect, ref rosterScroll, view);
            float ry = 0f;
            int i = 0;
            foreach (PawnKindDef k in hireable)
            {
                Rect row = new Rect(0f, ry, view.width, RowHeight);
                look.Row(row, selected: CountOf(k) > 0, hoverable: false, alternate: i++ % 2 == 1);
                DrawKindCells(row, k, wLabel, xCp - rect.x, xPrice - rect.x, xAvail - rect.x);
                int stock = roster.StockOf(k);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(xAvail - rect.x, ry, 80f, RowHeight), stock.ToString());
                DrawAdjuster(new Rect(xAdj - rect.x, ry + 2f, 250f, RowHeight - 4f), k, stock);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(xSub - rect.x, ry, 80f, RowHeight), (CountOf(k) * HireUtility.PriceOf(k)).ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                ry += RowHeight;
            }
            foreach (PawnKindDef k in locked)
            {
                Rect row = new Rect(0f, ry, view.width, RowHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                DrawKindCells(row, k, wLabel, xCp - rect.x, xPrice - rect.x, xAvail - rect.x);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(xAvail - rect.x, ry, 340f, RowHeight), "CR_RequiresGoodwill".Translate(GoodwillTiers.ThresholdFor(tiers[k])));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                ry += RowHeight;
            }
            Widgets.EndScrollView();
        }

        private static void DrawKindCells(Rect row, PawnKindDef k, float wLabel, float xCp, float xPrice, float xAvail)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect label = new Rect(row.x + 4f, row.y, wLabel - 8f, row.height);
            Widgets.Label(label, k.LabelCap.Truncate(label.width));
            TooltipHandler.TipRegion(label, k.LabelCap);
            Widgets.Label(new Rect(xCp, row.y, 70f, row.height), Mathf.RoundToInt(k.combatPower).ToString());
            Widgets.Label(new Rect(xPrice, row.y, 70f, row.height), HireUtility.PriceOf(k).ToString());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private int CountOf(PawnKindDef k) => counts.TryGetValue(k, out int n) ? n : 0;

        private void DrawAdjuster(Rect rect, PawnKindDef k, int stock)
        {
            float bw = 42f;
            int mult = GenUI.CurrentAdjustmentMultiplier();
            int cur = CountOf(k);
            HireLook look = HireLook.Current;
            Rect r = new Rect(rect.x, rect.y, bw, rect.height);
            if (look.Button(r, "<<")) Set(k, 0, cur);
            r.x += bw + 2f;
            if (look.Button(r, "-" + (mult > 1 ? mult.ToString() : ""))) Set(k, cur - mult, cur, stock);
            r.x += bw + 2f;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(r.x, r.y, 50f, r.height), cur.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            r.x += 52f;
            if (look.Button(r, "+" + (mult > 1 ? mult.ToString() : ""))) Set(k, cur + mult, cur, stock);
            r.x += bw + 2f;
            if (look.Button(r, ">>")) Set(k, stock, cur, stock);
        }

        private void Set(PawnKindDef k, int wanted, int cur, int stock = int.MaxValue)
        {
            int next = Mathf.Clamp(wanted, 0, stock);
            if (next == cur)
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return;
            }
            if (next == 0) counts.Remove(k);
            else counts[k] = next;
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        private void DrawFooter(Rect rect, HireRoster roster)
        {
            HireLook look = HireLook.Current;
            look.Divider(rect.x, rect.y, rect.width);
            int pawns = counts.Values.Sum();
            int price = counts.Sum(kv => kv.Value * HireUtility.PriceOf(kv.Key));
            int silver = HireUtility.BeaconSilver(map);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y + 8f, 500f, 30f),
                "CR_Totals".Translate(pawns.Named("PAWNS"), price.Named("PRICE"), silver.Named("SILVER")));
            Text.Anchor = TextAnchor.UpperLeft;

            Rect cancel = new Rect(rect.xMax - 160f, rect.y + 8f, 160f, 40f);
            Rect hire = new Rect(cancel.x - 170f, cancel.y, 160f, 40f);
            if (look.Button(cancel, "CancelButton".Translate())) Close();

            bool canHire = selected != null && pawns > 0 && silver >= price;
            if (look.Button(hire, "CR_HireButton".Translate(), canHire, HireTone.Confirm) && canHire)
            {
                TradeUtility.LaunchSilver(map, price);
                GameComponent_CommRecruiter.Instance.PlaceOrder(selected, map, counts);
                Messages.Message("CR_HiredMessage".Translate(pawns.Named("PAWNS"), selected.Named("FACTION")),
                    MessageTypeDefOf.PositiveEvent, historical: false);
                Close();
            }
            if (!canHire && Mouse.IsOver(hire))
            {
                string tip = pawns == 0 ? "CR_HireTipNothing".Translate() : "CR_HireTipSilver".Translate(price - silver);
                TooltipHandler.TipRegion(hire, tip);
            }
        }
    }
}
