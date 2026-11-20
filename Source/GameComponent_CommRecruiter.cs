using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CommRecruiter
{
    public class GameComponent_CommRecruiter : GameComponent
    {
        private List<HireRoster> rosters = new List<HireRoster>();
        private List<HireOrder> orders = new List<HireOrder>();
        private List<HireeRecord> records = new List<HireeRecord>();
        private int nextBatchId = 1;

        public static GameComponent_CommRecruiter Instance => Current.Game?.GetComponent<GameComponent_CommRecruiter>();

        public GameComponent_CommRecruiter(Game game) { }

        public List<HireOrder> Orders => orders;
        public List<HireeRecord> Records => records;
        public List<HireRoster> Rosters => rosters;

        public static int RoundHalfUp(float x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);

        public HireeRecord RecordFor(Pawn p) => p == null ? null : records.Find(r => r.pawn == p);

        public HireRoster RosterFor(Faction f, bool allowRegenerate = true)
        {
            HireRoster r = rosters.Find(x => x.faction == f);
            if (r == null)
            {
                r = new HireRoster { faction = f };
                rosters.Add(r);
            }
            if (allowRegenerate && r.NeedsRegeneration(GoodwillTiers.UnlockedTier(f))) r.Generate();
            return r;
        }

        public void PlaceOrder(Faction f, Map map, Dictionary<PawnKindDef, int> counts)
        {
            var order = new HireOrder
            {
                faction = f,
                map = map,
                counts = new Dictionary<PawnKindDef, int>(counts),
                arrivalTick = Find.TickManager.TicksGame
                    + Mathf.RoundToInt(Rand.Range(Tuning.MinArrivalDays, Tuning.MaxArrivalDays) * Tuning.TicksPerDay),
            };
            orders.Add(order);
            HireRoster roster = RosterFor(f, allowRegenerate: false);
            foreach (var kv in counts) roster.Take(kv.Key, kv.Value);
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % Tuning.TickInterval != 0) return;

            for (int i = orders.Count - 1; i >= 0; i--)
            {
                if (orders[i].arrivalTick > now) continue;
                HireOrder o = orders[i];
                orders.RemoveAt(i);
                try { Arrive(o); }
                catch (Exception e) { Log.Error("[Comm Recruiter] Arrival failed: " + e); }
            }

            try { TickContracts(); }
            catch (Exception e) { Log.Error("[Comm Recruiter] Contract tick failed: " + e); }
        }

        // ---------------------------------------------------------------- arrival

        private void Arrive(HireOrder order)
        {
            Faction f = order.faction;
            if (f == null || f.defeated || f.HostileTo(Faction.OfPlayer))
            {
                Find.LetterStack.ReceiveLetter("CR_OrderCancelledLabel".Translate(),
                    "CR_OrderCancelledHostile".Translate(f.Named("FACTION")), LetterDefOf.NegativeEvent, null, f);
                return;
            }
            Map map = order.map != null && Find.Maps.Contains(order.map) ? order.map : Find.AnyPlayerHomeMap;
            if (map == null)
            {
                Find.LetterStack.ReceiveLetter("CR_OrderCancelledLabel".Translate(),
                    "CR_OrderCancelledNoMap".Translate(f.Named("FACTION")), LetterDefOf.NegativeEvent, null, f);
                return;
            }

            int now = Find.TickManager.TicksGame;
            int batch = nextBatchId++;
            var pawns = new List<Pawn>();
            int refund = 0;
            bool logged = false;
            foreach (var kv in order.counts)
            {
                for (int i = 0; i < kv.Value; i++)
                {
                    Pawn p = null;
                    try { p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kv.Key, f)); }
                    catch (Exception e)
                    {
                        if (!logged) Log.Error($"[Comm Recruiter] Generating {kv.Key.defName} for {f} failed: {e}");
                        logged = true;
                    }
                    if (p == null)
                    {
                        refund += HireUtility.PriceOf(kv.Key);
                        continue;
                    }
                    List<int> ids = HireeOutfitter.Outfit(p);
                    p.SetFaction(Faction.OfPlayer);
                    pawns.Add(p);
                    records.Add(new HireeRecord
                    {
                        pawn = p,
                        homeFaction = f,
                        combatPower = kv.Key.combatPower,
                        arrivalTick = now,
                        batchId = batch,
                        issuedApparelIds = ids,
                    });
                }
            }

            if (pawns.Count > 0)
            {
                Quest quest = MakeContractQuest(f, pawns);
                foreach (HireeRecord r in records.Where(x => x.batchId == batch)) r.quest = quest;
            }

            IntVec3 spot;
            if (order.DropPods || !RCellFinder.TryFindRandomPawnEntryCell(out spot, map, CellFinder.EdgeRoadChance_Friendly))
            {
                spot = DropCellFinder.TradeDropSpot(map);
                if (pawns.Count > 0) DropPodUtility.DropThingsNear(spot, map, pawns.Cast<Thing>(), forbid: false);
            }
            else
            {
                foreach (Pawn p in pawns) GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(spot, map, 4), map);
            }

            if (refund > 0) DropSilver(spot, map, refund);

            if (pawns.Count == 0) return;
            long endAbs = Find.TickManager.TicksAbs + Tuning.ContractTicks;
            string end = GenDate.DateFullStringAt(endAbs, Find.WorldGrid.LongLatOf(map.Tile));
            TaggedString text = "CR_ArrivalText".Translate(f.Named("FACTION"), PawnList(pawns).Named("PAWNS"), end.Named("DATE"));
            if (refund > 0) text += "\n\n" + "CR_ArrivalRefund".Translate(refund.Named("SILVER"));
            Find.LetterStack.ReceiveLetter("CR_ArrivalLabel".Translate(), text, LetterDefOf.PositiveEvent, new LookTargets(pawns), f);
        }

        /// <summary>
        /// Hidden quest that keeps the hirees members of their home faction while the player controls them,
        /// the same way vanilla marks quest helpers (faction icon on the colonist bar and character card).
        /// </summary>
        private static Quest MakeContractQuest(Faction f, List<Pawn> pawns)
        {
            Quest quest = Quest.MakeRaw();
            quest.root = CR_DefOf.CR_HireContract;
            quest.hidden = true;
            quest.name = "CR_ContractQuestName".Translate(f.Named("FACTION"));
            quest.ExtraFaction(f, pawns, ExtraFactionType.HomeFaction, areHelpers: true);
            Find.QuestManager.Add(quest);
            quest.SetInitiallyAccepted();
            return quest;
        }

        /// <summary>Takes a hiree off the contract: drops the record and its home-faction marking.</summary>
        private void LeaveContract(HireeRecord r)
        {
            records.Remove(r);
            Quest q = r.quest;
            if (q == null) return;
            foreach (QuestPart_ExtraFaction part in q.PartsListForReading.OfType<QuestPart_ExtraFaction>())
                if (part.affectedPawns.Remove(r.pawn))
                    part.extraFaction?.faction?.Notify_MemberLeftExtraFaction(r.pawn);
            if (q.State == QuestState.Ongoing && !records.Any(x => x.quest == q))
                q.End(QuestEndOutcome.Success, sendLetter: false, playSound: false);
        }

        private static void DropSilver(IntVec3 spot, Map map, int amount)
        {
            while (amount > 0)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = Mathf.Min(amount, ThingDefOf.Silver.stackLimit);
                amount -= silver.stackCount;
                GenPlace.TryPlaceThing(silver, spot, map, ThingPlaceMode.Near);
            }
        }

        private static string PawnList(IEnumerable<Pawn> pawns) =>
            pawns.Select(p => "  - " + p.LabelShortCap + ", " + p.kindDef.LabelCap.Resolve()).ToList().ToLineList();

        // ---------------------------------------------------------------- contract

        private void TickContracts()
        {
            if (records.Count == 0) return;

            for (int i = records.Count - 1; i >= 0; i--)
            {
                HireeRecord r = records[i];
                Pawn p = r.pawn;
                if (p == null || (p.Destroyed && !p.Dead) || r.homeFaction == null)
                {
                    LeaveContract(r);
                    continue;
                }
                if (p.Dead)
                {
                    LeaveContract(r);
                    HireUtility.AffectGoodwill(r.homeFaction, -Mathf.Max(1, RoundHalfUp(r.combatPower * Tuning.DeathGoodwillPerCp)),
                        CR_DefOf.CR_HireeDied, p.Corpse);
                    continue;
                }
                if (p.IsSlaveOfColony)
                {
                    LeaveContract(r);
                    Betrayal(r);
                    continue;
                }
                if (p.needs?.mood != null && p.needs.mood.CurLevelPercentage > Tuning.HappyMoodThreshold)
                    r.happyTicks += Tuning.TickInterval;
            }

            // Hostile home faction: every hiree from it reverts and flees.
            // Defeated home faction: nobody to go back to, so its hirees simply stay as colonists.
            foreach (Faction f in records.Select(r => r.homeFaction).Distinct().ToList())
            {
                if (f.defeated)
                {
                    List<HireeRecord> stay = records.Where(r => r.homeFaction == f).ToList();
                    CloseStayLetters(stay);
                    foreach (HireeRecord r in stay) LeaveContract(r);
                }
                else if (f.HostileTo(Faction.OfPlayer))
                    Revert(records.Where(r => r.homeFaction == f).ToList(), hostile: true);
            }

            foreach (var batch in records.GroupBy(r => r.batchId).ToList())
            {
                List<HireeRecord> list = batch.ToList();
                int elapsed = list[0].Elapsed;
                if (elapsed >= Tuning.StayLetterTick)
                {
                    foreach (HireeRecord r in list.Where(x => !x.stayLetterSent))
                    {
                        r.stayLetterSent = true;
                        if (r.happyTicks > Tuning.HappyTicksForStay) SendStayLetter(r);
                    }
                }
                if (elapsed >= Tuning.GearWarningTick && list.Any(x => !x.gearWarned))
                {
                    foreach (HireeRecord r in list) r.gearWarned = true;
                    SendGearWarning(list);
                }
                if (elapsed >= Tuning.ContractTicks) Depart(list);
            }
        }

        private void Betrayal(HireeRecord slave)
        {
            Faction f = slave.homeFaction;
            if (f.HostileTo(Faction.OfPlayer)) return;
            HireUtility.AffectGoodwill(f, -(f.PlayerGoodwill + 100), CR_DefOf.CR_HireeEnslaved, slave.pawn);
            if (!f.HostileTo(Faction.OfPlayer))
                f.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Hostile, true, CR_DefOf.CR_HireeEnslaved.label);
            // The remaining hirees are reverted by the hostile-faction pass in the same tick.
        }

        private void SendStayLetter(HireeRecord r)
        {
            Pawn p = r.pawn;
            int cost = RetainCost(r);
            var letter = (ChoiceLetter_HireeStay)LetterMaker.MakeLetter(
                "CR_StayLabel".Translate(p.Named("PAWN")),
                "CR_StayText".Translate(p.Named("PAWN"), r.homeFaction.Named("FACTION"), cost.Named("COST")),
                CR_DefOf.CR_HireeStayLetter, new LookTargets(p), r.homeFaction);
            letter.pawn = p;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public static int RetainCost(HireeRecord r) => Mathf.Max(1, RoundHalfUp(r.combatPower * Tuning.RetainGoodwillPerCp));

        /// <summary>Stay letter accepted.</summary>
        public void Retain(HireeRecord r)
        {
            if (r.retained) return;
            r.retained = true;
            HireUtility.AffectGoodwill(r.homeFaction, -RetainCost(r), CR_DefOf.CR_HireeRetained, r.pawn);
        }

        private void SendGearWarning(List<HireeRecord> batch)
        {
            List<Pawn> carrying = batch.Where(r => !r.retained && HireeOutfitter.HasExtraGear(r.pawn, r.issuedApparelIds))
                .Select(r => r.pawn).ToList();
            TaggedString text = "CR_GearWarningText".Translate();
            if (carrying.Count > 0) text += "\n\n" + "CR_GearWarningList".Translate() + "\n" + PawnList(carrying);
            Find.LetterStack.ReceiveLetter("CR_GearWarningLabel".Translate(), text, LetterDefOf.NeutralEvent,
                new LookTargets(batch.Select(r => r.pawn)), batch[0].homeFaction);
        }

        private void Depart(List<HireeRecord> batch)
        {
            CloseStayLetters(batch);
            var leaving = new List<HireeRecord>();
            foreach (HireeRecord r in batch)
            {
                LeaveContract(r);
                if (r.retained) continue;
                Pawn p = r.pawn;
                if (p.IsPrisonerOfColony) GenGuest.PrisonerRelease(p);
                if (p.MapHeld != null && (p.Downed || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)))
                {
                    ForceRecruit(r);
                    continue;
                }
                leaving.Add(r);
            }
            if (leaving.Count == 0) return;
            HashSet<Pawn> fromOutposts = Revert(leaving, hostile: false);
            leaving.RemoveAll(r => fromOutposts.Contains(r.pawn));
            if (leaving.Count == 0) return;
            Find.LetterStack.ReceiveLetter("CR_LeavingLabel".Translate(),
                "CR_LeavingText".Translate(PawnList(leaving.Select(r => r.pawn)).Named("PAWNS")),
                LetterDefOf.NeutralEvent, new LookTargets(leaving.Where(r => r.pawn.Spawned).Select(r => r.pawn)), leaving[0].homeFaction);
        }

        private static void CloseStayLetters(IEnumerable<HireeRecord> batch)
        {
            var pawns = new HashSet<Pawn>(batch.Select(r => r.pawn));
            foreach (ChoiceLetter_HireeStay l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_HireeStay>().ToList())
                if (pawns.Contains(l.pawn)) Find.LetterStack.RemoveLetter(l);
        }

        private void ForceRecruit(HireeRecord r)
        {
            Pawn p = r.pawn;
            Map map = p.MapHeld;
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            int price = Mathf.CeilToInt(r.combatPower / 2f);
            int paid = Mathf.Min(price, HireUtility.BeaconSilver(map));
            if (paid > 0) TradeUtility.LaunchSilver(map, paid);
            int shortfall = price - paid;
            int goodwill = 0;
            if (shortfall > 0)
            {
                goodwill = Mathf.Max(1, RoundHalfUp(shortfall * Tuning.ShortfallGoodwillPerSilver));
                HireUtility.AffectGoodwill(r.homeFaction, -goodwill, CR_DefOf.CR_HireeForcedStay, p);
            }
            TaggedString text = "CR_ForcedText".Translate(p.Named("PAWN"), r.homeFaction.Named("FACTION"), paid.Named("SILVER"));
            if (goodwill > 0) text += "\n\n" + "CR_ForcedGoodwill".Translate(shortfall.Named("SHORT"), goodwill.Named("GOODWILL"), r.homeFaction.Named("FACTION"));
            Find.LetterStack.ReceiveLetter("CR_ForcedLabel".Translate(p.Named("PAWN")), text, LetterDefOf.NeutralEvent, p, r.homeFaction);
        }

        /// <summary>Returns hirees to their faction. Spawned ones walk (or, when hostile, jog) off the map.</summary>
        private HashSet<Pawn> Revert(List<HireeRecord> list, bool hostile)
        {
            // Hirees sitting in a WD2 outpost vanish to the world with a letter per outpost.
            var fromOutposts = OutpostExit.Extract(list.Select(r => r.pawn));
            foreach (var kv in fromOutposts)
            {
                string key = hostile ? "CR_OutpostLostHostileText" : "CR_OutpostLostText";
                Find.LetterStack.ReceiveLetter("CR_OutpostLostLabel".Translate(kv.Key.LabelCap),
                    key.Translate(kv.Key.LabelCap.Named("OUTPOST"), PawnList(kv.Value).Named("PAWNS"),
                        list[0].homeFaction.Named("FACTION")),
                    LetterDefOf.NeutralEvent, new LookTargets(kv.Key), list[0].homeFaction);
            }
            var byMap = new Dictionary<Map, List<Pawn>>();
            foreach (HireeRecord r in list)
            {
                LeaveContract(r);
                Pawn p = r.pawn;
                if (p.Dead) continue;
                if (!hostile && p.IsPrisonerOfColony) GenGuest.PrisonerRelease(p);
                if (p.Spawned)
                {
                    if (p.Faction != r.homeFaction) p.SetFaction(r.homeFaction);
                    if (p.IsPrisonerOfColony || p.Downed) continue;
                    if (!byMap.TryGetValue(p.Map, out var group)) byMap[p.Map] = group = new List<Pawn>();
                    group.Add(p);
                }
                else
                {
                    ReturnOffscreen(p, r.homeFaction);
                }
            }
            if (hostile) CloseStayLetters(list);
            foreach (var kv in byMap)
            {
                LocomotionUrgency speed = hostile ? LocomotionUrgency.Jog : LocomotionUrgency.Walk;
                LordMaker.MakeNewLord(kv.Value[0].Faction, new LordJob_ExitMapBest(speed, canDig: false, canDefendSelf: true), kv.Key, kv.Value);
            }
            return new HashSet<Pawn>(fromOutposts.Values.SelectMany(x => x));
        }

        private static void ReturnOffscreen(Pawn p, Faction home)
        {
            Caravan caravan = p.GetCaravan();
            if (caravan != null) caravan.RemovePawn(p);
            else p.holdingOwner?.Remove(p);
            if (p.Faction != home) p.SetFaction(home);
            if (!p.IsWorldPawn()) Find.WorldPawns.PassToWorld(p);
        }

        // ---------------------------------------------------------------- save

        public override void FinalizeInit()
        {
            // Saves from before the contract quest existed: give those batches their home-faction marking.
            foreach (var batch in records.Where(r => r.quest == null && r.pawn != null && !r.pawn.Dead && r.homeFaction != null)
                         .GroupBy(r => r.batchId).ToList())
            {
                List<HireeRecord> list = batch.ToList();
                Quest quest = MakeContractQuest(list[0].homeFaction, list.Select(r => r.pawn).ToList());
                foreach (HireeRecord r in list) r.quest = quest;
            }
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                foreach (HireOrder o in orders)
                    if (o.map != null && !Find.Maps.Contains(o.map)) o.map = null;

            Scribe_Collections.Look(ref rosters, "rosters", LookMode.Deep);
            Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            Scribe_Values.Look(ref nextBatchId, "nextBatchId", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                rosters ??= new List<HireRoster>();
                orders ??= new List<HireOrder>();
                records ??= new List<HireeRecord>();
                rosters.RemoveAll(r => r?.faction == null);
                orders.RemoveAll(o => o?.faction == null);
                records.RemoveAll(r => r?.pawn == null);
            }
        }
    }
}
