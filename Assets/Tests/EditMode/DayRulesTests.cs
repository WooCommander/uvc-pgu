using NUnit.Framework;
using UnityEngine;

namespace Uvc.Core.Tests
{
    public sealed class ClockTests
    {
        [Test]
        public void TwoRealSecondsMakeOneGameMinute()
        {
            GameClock clock = new();
            clock.Tick(2f);
            Assert.AreEqual(GameClock.DayStart + 1f, clock.minutes, 0.0001f);
        }

        [Test]
        public void ClockStopsAtDayEnd()
        {
            GameClock clock = new() { minutes = GameClock.DayEnd - 1f };
            clock.Tick(600f);
            Assert.AreEqual(GameClock.DayEnd, clock.minutes);
            Assert.IsTrue(clock.IsOver);
        }

        [Test]
        public void ZeroDeltaDoesNotMoveTime()
        {
            DayState state = DayState.CreateNew(1);
            float before = state.clock.minutes;
            DayRules.Tick(state, 0f);
            Assert.AreEqual(before, state.clock.minutes);
        }

        [Test]
        public void EveningPromptFiresOnce()
        {
            DayState state = DayState.CreateNew(1);
            state.clock.minutes = GameClock.WorkEnd - 0.1f;
            Assert.IsTrue(DayRules.Tick(state, 1f).EveningStarted);
            Assert.IsFalse(DayRules.Tick(state, 1f).EveningStarted);
        }

        [Test]
        public void DayOverIsReportedAtTwentyHundred()
        {
            DayState state = DayState.CreateNew(1);
            state.clock.minutes = GameClock.DayEnd - 0.1f;
            Assert.IsTrue(DayRules.Tick(state, 1f).DayOver);
        }
    }

    public sealed class InventoryTests
    {
        [Test]
        public void FullInventoryRejectsWithoutLosingItems()
        {
            Inventory inventory = new();
            Assert.IsTrue(inventory.TryAdd(ItemIds.Paper, 5 * (Inventory.SlotCount - 1)));
            Assert.IsTrue(inventory.TryAdd(ItemIds.Cartridge, 3));
            Assert.AreEqual(Inventory.SlotCount, inventory.slots.Count);

            Assert.IsFalse(inventory.TryAdd(ItemIds.Opener));
            Assert.IsFalse(inventory.TryAdd(ItemIds.Cartridge));
            Assert.AreEqual(3, inventory.Count(ItemIds.Cartridge));
            Assert.AreEqual(35, inventory.Count(ItemIds.Paper));
        }

        [Test]
        public void QuestItemsIgnoreSlotLimit()
        {
            Inventory inventory = new();
            for (int index = 0; index < Inventory.SlotCount; index++)
            {
                inventory.slots.Add(new ItemStack { itemId = ItemIds.Paper, count = 5 });
            }

            Assert.IsTrue(inventory.TryAdd(ItemIds.ProjectorAdapter));
            Assert.IsTrue(inventory.Has(ItemIds.ProjectorAdapter));
        }

        [Test]
        public void StacksRespectLimit()
        {
            Inventory inventory = new();
            Assert.IsTrue(inventory.TryAdd(ItemIds.Paper, 7));
            Assert.AreEqual(2, inventory.slots.Count);
            Assert.IsTrue(inventory.TryRemove(ItemIds.Paper, 6));
            Assert.AreEqual(1, inventory.Count(ItemIds.Paper));
            Assert.IsFalse(inventory.TryRemove(ItemIds.Paper, 2));
            Assert.AreEqual(1, inventory.Count(ItemIds.Paper));
        }
    }

    public sealed class EveningChainTests
    {
        private static DayState NewDay(string printerFault)
        {
            DayState state = DayState.CreateNew(42);
            state.printerFault = printerFault;
            return state;
        }

        private static void Run(DayState state, string actionId, string choice = null)
        {
            ActionOutcome outcome = DayRules.Execute(state, actionId, choice);
            Assert.IsTrue(outcome.Success, $"{actionId}: {outcome.Message}");
        }

        private static void FixPrinter(DayState state)
        {
            if (state.printerFault == Faults.Jam && !state.inventory.Has(ItemIds.Screwdriver))
            {
                Run(state, ActionIds.ShelfScrewdriver);
            }

            if (state.printerFault != Faults.Cartridge)
            {
                Run(state, ActionIds.PrinterRepair, state.printerFault);
            }

            Run(state, ActionIds.PrinterRepair, Faults.Cartridge);
            Assert.IsTrue(state.HasFlag(Flags.PrinterFixed));
        }

        private static void FixProjector(DayState state)
        {
            Run(state, ActionIds.JohnAdapter);
            Run(state, ActionIds.ProjectorSetup, state.projectorInput);
            Assert.IsTrue(state.HasFlag(Flags.ProjectorFixed));
        }

        [Test]
        public void ChainCompletesWithPrinterFirst()
        {
            DayState state = NewDay(Faults.Paper);
            Run(state, ActionIds.BossTakeTask);
            Run(state, ActionIds.StockSort, "0,1,2,0,2");
            FixPrinter(state);
            FixProjector(state);
            Run(state, ActionIds.BossDeliver);
            Assert.AreEqual(QuestStatus.RewardClaimed, state.GetQuest(QuestIds.Evening));
            Assert.AreEqual(QuestStatus.Available, state.GetQuest(QuestIds.Party));
        }

        [Test]
        public void ChainCompletesWithProjectorFirstAndVigoRoute()
        {
            DayState state = NewDay(Faults.Jam);
            Run(state, ActionIds.BossTakeTask);
            FixProjector(state);
            Run(state, ActionIds.VigoDiagnose);
            FixPrinter(state);
            Run(state, ActionIds.BossDeliver);
            Assert.AreEqual(QuestStatus.RewardClaimed, state.GetQuest(QuestIds.Evening));
        }

        [Test]
        public void MissingItemExplainsAndSpendsNothing()
        {
            DayState state = NewDay(Faults.Cartridge);
            Run(state, ActionIds.BossTakeTask);
            float before = state.clock.minutes;
            int energy = state.stats.energy;

            ActionOutcome outcome = DayRules.Execute(state, ActionIds.PrinterRepair, Faults.Cartridge);

            Assert.IsFalse(outcome.Success);
            StringAssert.Contains("Картридж", outcome.Message);
            Assert.AreEqual(before, state.clock.minutes);
            Assert.AreEqual(energy, state.stats.energy);
        }

        [Test]
        public void BlockedProjectorShowsReason()
        {
            DayState state = NewDay(Faults.Cartridge);
            ActionCheck check = DayRules.Check(state, ActionIds.ProjectorSetup);
            Assert.AreEqual(ActionAvailability.Blocked, check.Availability);
            StringAssert.Contains("переходник", check.Reason);
        }

        [Test]
        public void WrongPrinterGuessCostsTimeButNoItem()
        {
            DayState state = NewDay(Faults.Cartridge);
            Run(state, ActionIds.BossTakeTask);
            Run(state, ActionIds.VigoDiagnose);
            float before = state.clock.minutes;

            DayRules.Execute(state, ActionIds.PrinterRepair, Faults.Paper);

            Assert.AreEqual(before + 10f, state.clock.minutes, 0.001f);
            Assert.IsTrue(state.inventory.Has(ItemIds.Cartridge));
        }

        [Test]
        public void ActionSpendsTimeExactlyOnce()
        {
            DayState state = NewDay(Faults.Cartridge);
            float before = state.clock.minutes;
            Run(state, ActionIds.TerminalPlay, "0:0,1:1,2:0");
            Assert.AreEqual(before + 30f, state.clock.minutes, 0.001f);
        }

        [Test]
        public void DeliveryRewardIsPaidOnce()
        {
            DayState state = NewDay(Faults.Cartridge);
            Run(state, ActionIds.BossTakeTask);
            Run(state, ActionIds.VigoDiagnose);
            FixPrinter(state);
            FixProjector(state);
            Run(state, ActionIds.BossDeliver);
            int reputation = state.stats.bossReputation;

            ActionOutcome second = DayRules.Execute(state, ActionIds.BossDeliver);

            Assert.IsFalse(second.Success);
            Assert.AreEqual(reputation, state.stats.bossReputation);
        }

        [Test]
        public void RewardIsNotRepeatedAfterSaveAndLoad()
        {
            DayState state = NewDay(Faults.Cartridge);
            Run(state, ActionIds.BossTakeTask);
            Run(state, ActionIds.VigoDiagnose);
            FixPrinter(state);
            FixProjector(state);
            Run(state, ActionIds.BossDeliver);

            DayState loaded = JsonUtility.FromJson<DayState>(JsonUtility.ToJson(state));

            Assert.AreEqual(ActionAvailability.Hidden, DayRules.Check(loaded, ActionIds.BossDeliver).Availability);
            Assert.AreEqual(state.stats.bossReputation, loaded.stats.bossReputation);
            Assert.AreEqual(state.inventory.Count(ItemIds.Paper), loaded.inventory.Count(ItemIds.Paper));
        }

        [Test]
        public void LateDeliveryGivesSmallerReward()
        {
            DayState state = NewDay(Faults.Cartridge);
            Run(state, ActionIds.BossTakeTask);
            Run(state, ActionIds.VigoDiagnose);
            FixPrinter(state);
            FixProjector(state);
            state.clock.minutes = DayRules.DeliveryDeadline + 1f;
            Run(state, ActionIds.BossDeliver);
            Assert.IsFalse(state.HasFlag(Flags.DeliveredOnTime));
        }
    }

    public sealed class PartyTests
    {
        private static DayState ReadyForParty()
        {
            DayState state = DayState.CreateNew(7);
            state.SetQuest(QuestIds.Party, QuestStatus.Available);
            Assert.IsTrue(DayRules.Execute(state, ActionIds.SeregaParty).Success);
            return state;
        }

        [Test]
        public void PartyWaitsForEvening()
        {
            DayState state = ReadyForParty();
            Assert.AreEqual(ActionAvailability.Blocked, DayRules.Check(state, ActionIds.TableParty).Availability);
        }

        [Test]
        public void FullPartyWithLemonadeIsAsGoodAsWithBeer()
        {
            DayState state = ReadyForParty();
            DayRules.Execute(state, ActionIds.Invite(NpcIds.Andryukha));
            DayRules.Execute(state, ActionIds.Invite(NpcIds.John));
            DayRules.Execute(state, ActionIds.FridgeLemonade);
            DayRules.Execute(state, ActionIds.ShelfOpener);
            state.clock.minutes = GameClock.WorkEnd;

            ActionOutcome outcome = DayRules.Execute(state, ActionIds.TableParty);

            Assert.IsTrue(outcome.EndsDay);
            Assert.That(state.partyOutcome, Is.EqualTo("great").Or.EqualTo("legendary"));
        }

        [Test]
        public void MissingFoodGivesAlternativeScene()
        {
            DayState state = ReadyForParty();
            state.inventory.TryRemove(ItemIds.Capelin);
            DayRules.Execute(state, ActionIds.Invite(NpcIds.Andryukha));
            state.clock.minutes = GameClock.WorkEnd;

            ActionOutcome outcome = DayRules.Execute(state, ActionIds.TableParty);

            Assert.IsTrue(outcome.Success);
            Assert.IsTrue(outcome.EndsDay);
            Assert.AreEqual("modest", state.partyOutcome);
        }

        [Test]
        public void PartyQuestOpensAtFivePmWithoutDelivery()
        {
            DayState state = DayState.CreateNew(7);
            state.clock.minutes = DayRules.PartyQuestOpensAt - 0.1f;
            Assert.IsTrue(DayRules.Tick(state, 1f).PartyOpened);
            Assert.AreEqual(QuestStatus.Available, state.GetQuest(QuestIds.Party));
        }
    }

    public sealed class MaimeskulTests
    {
        private static DayState WithRitualReady()
        {
            DayState state = DayState.CreateNew(3);
            DayRules.Execute(state, ActionIds.Talk(NpcIds.Andryukha));
            Assert.IsTrue(DayRules.Execute(state, ActionIds.AndryukhaSecret).Success);
            DayRules.Execute(state, ActionIds.BenchPass);
            DayRules.Execute(state, ActionIds.CabinetJournal);
            DayRules.Execute(state, ActionIds.MugsRitual);
            return state;
        }

        [Test]
        public void RitualNeedsAllThreeItems()
        {
            DayState state = DayState.CreateNew(3);
            DayRules.Execute(state, ActionIds.Talk(NpcIds.Andryukha));
            DayRules.Execute(state, ActionIds.AndryukhaSecret);
            DayRules.Execute(state, ActionIds.BenchPass);
            Assert.AreEqual(ActionAvailability.Blocked, DayRules.Check(state, ActionIds.AndryukhaRitual).Availability);
        }

        [Test]
        public void RitualStartsTwentyFourGameHours()
        {
            DayState state = WithRitualReady();
            float startedAt = state.clock.Absolute;
            ActionOutcome outcome = DayRules.Execute(state, ActionIds.AndryukhaRitual);
            Assert.IsTrue(outcome.MaimeskulStarted);
            Assert.AreEqual(startedAt + DayRules.MaimeskulDuration, state.maimeskul.expiresAt, 0.001f);
        }

        [Test]
        public void RitualCannotBeRepeatedInTheSameWeek()
        {
            DayState state = WithRitualReady();
            DayRules.Execute(state, ActionIds.AndryukhaRitual);
            Assert.AreEqual(ActionAvailability.Hidden, DayRules.Check(state, ActionIds.AndryukhaRitual).Availability);
            Assert.AreEqual(ActionAvailability.Hidden, DayRules.Check(state, ActionIds.AndryukhaSecret).Availability);
        }

        [Test]
        public void LoadKeepsExpiryAndDoesNotRestartTimer()
        {
            DayState state = WithRitualReady();
            DayRules.Execute(state, ActionIds.AndryukhaRitual);
            float expiresAt = state.maimeskul.expiresAt;

            DayState loaded = JsonUtility.FromJson<DayState>(JsonUtility.ToJson(state));

            Assert.IsTrue(loaded.maimeskul.active);
            Assert.AreEqual(expiresAt, loaded.maimeskul.expiresAt);
        }

        [Test]
        public void EventExpiresByWorldTime()
        {
            DayState state = WithRitualReady();
            DayRules.Execute(state, ActionIds.AndryukhaRitual);
            state.clock.dayIndex = 1;
            Assert.IsTrue(DayRules.Tick(state, 0.1f).MaimeskulExpired);
            Assert.IsFalse(state.maimeskul.active);
        }

        [Test]
        public void LastErrandEndsEventEarly()
        {
            DayState state = WithRitualReady();
            DayRules.Execute(state, ActionIds.AndryukhaRitual);
            Assert.IsTrue(DayRules.Execute(state, ActionIds.AndryukhaLastErrand).Success);
            Assert.IsTrue(DayRules.Execute(state, ActionIds.ShelfArchive).Success);
            ActionOutcome outcome = DayRules.Execute(state, ActionIds.CabinetReturnSheet);
            Assert.IsTrue(outcome.MaimeskulEnded);
            Assert.IsFalse(state.maimeskul.active);
            Assert.IsTrue(state.maimeskul.endedEarly);
        }

        [Test]
        public void ReactionHasCooldownPerTarget()
        {
            DayState state = WithRitualReady();
            DayRules.Execute(state, ActionIds.AndryukhaRitual);
            Assert.IsTrue(DayRules.TryMaimeskulReaction(state, NpcIds.Serega, out _));
            Assert.IsFalse(DayRules.TryMaimeskulReaction(state, NpcIds.Serega, out _));
            Assert.IsTrue(DayRules.TryMaimeskulReaction(state, NpcIds.John, out _));
            state.clock.Spend(DayRules.MaimeskulReactionCooldown + 1f);
            Assert.IsTrue(DayRules.TryMaimeskulReaction(state, NpcIds.Serega, out _));
        }
    }

    public sealed class MinigameTests
    {
        [Test]
        public void CounterUnitsWinBothLanes()
        {
            KafedraMatch match = new(0, KafedraUnit.Armor, KafedraUnit.Infantry);
            // Armor is countered by Ranged; Infantry by Armor.
            Assert.IsTrue(match.TryResolve("1:0,2:0,2:1", out int[] power, out bool won));
            Assert.AreEqual(3, power[0]);
            Assert.AreEqual(2, power[1]);
            Assert.IsTrue(won);
        }

        [Test]
        public void MatchResultIsDeterministic()
        {
            KafedraMatch a = KafedraMatch.Create(5, 2);
            KafedraMatch b = KafedraMatch.Create(5, 2);
            Assert.AreEqual(a.MainLane, b.MainLane);
            Assert.AreEqual(a.MainEnemy, b.MainEnemy);
            Assert.AreEqual(a.SideEnemy, b.SideEnemy);
        }

        [Test]
        public void InvalidDecisionsSpendNoTime()
        {
            DayState state = DayState.CreateNew(9);
            float before = state.clock.minutes;
            Assert.IsFalse(DayRules.Execute(state, ActionIds.TerminalPlay, "0:0").Success);
            Assert.AreEqual(before, state.clock.minutes);
        }

        [Test]
        public void StockSortingNeedsFourOfFive()
        {
            Assert.AreEqual(5, StockSorting.CountCorrect("0,1,2,0,2"));
            Assert.AreEqual(3, StockSorting.CountCorrect("0,1,2,1,1"));
        }
    }

    public sealed class ResultsTests
    {
        [Test]
        public void EmptyDayNeedsExplaining()
        {
            DayState state = DayState.CreateNew(1);
            DayRules.FinishDay(state);
            Assert.AreEqual(DayResults.GradeExplain, DayResults.Compute(state).Grade);
        }

        [Test]
        public void OnTimeWorkAndGoodPartySaveTheDay()
        {
            DayState state = DayState.CreateNew(1);
            state.SetQuest(QuestIds.Evening, QuestStatus.RewardClaimed);
            state.SetFlag(Flags.DeliveredOnTime);
            state.partyOutcome = "great";
            Assert.AreEqual(DayResults.GradeSaved, DayResults.Compute(state).Grade);
        }
    }
}
