using NUnit.Framework;

namespace Uvc.Core.Tests
{
    public sealed class BragarTests
    {
        private static DayState AtMinute(float minute)
        {
            DayState state = DayState.CreateNew(11);
            state.clock.minutes = minute - 0.1f;
            DayRules.Tick(state, 1f);
            return state;
        }

        [Test]
        public void BagAppearsAfterBragarPassesBy()
        {
            DayState state = DayState.CreateNew(11);
            Assert.AreEqual(ActionAvailability.Hidden, DayRules.Check(state, ActionIds.BagTake).Availability);

            state.clock.minutes = DayRules.BragarDropAt - 0.1f;
            Assert.IsTrue(DayRules.Tick(state, 1f).BragarPassed);
            Assert.AreEqual(ActionAvailability.Ready, DayRules.Check(state, ActionIds.BagTake).Availability);
        }

        [Test]
        public void NoGamesWhileBragarSearches()
        {
            DayState state = AtMinute(DayRules.BragarDropAt);
            state.clock.minutes = DayRules.BragarReturnAt - 0.1f;
            Assert.IsTrue(DayRules.Tick(state, 1f).BragarSearchStarted);

            ActionCheck check = DayRules.Check(state, ActionIds.TerminalPlay);

            Assert.AreEqual(ActionAvailability.Blocked, check.Availability);
            StringAssert.Contains("Брагарь", check.Reason);
        }

        [Test]
        public void ReturningTheBagUnblocksGamesAndPaysOnce()
        {
            DayState state = AtMinute(DayRules.BragarDropAt);
            Assert.IsTrue(DayRules.Execute(state, ActionIds.BagTake).Success);
            state.clock.minutes = DayRules.BragarReturnAt - 0.1f;
            DayRules.Tick(state, 1f);
            int reputation = state.stats.bossReputation;

            ActionOutcome outcome = DayRules.Execute(state, ActionIds.BragarReturnBag);

            Assert.IsTrue(outcome.Success);
            Assert.IsTrue(outcome.BragarLeft);
            Assert.AreEqual(reputation + 8, state.stats.bossReputation);
            Assert.IsFalse(state.inventory.Has(ItemIds.BragarBag));
            Assert.AreEqual(ActionAvailability.Ready, DayRules.Check(state, ActionIds.TerminalPlay).Availability);
            Assert.AreEqual(ActionAvailability.Hidden, DayRules.Check(state, ActionIds.BragarReturnBag).Availability);
        }

        [Test]
        public void ReturnWithoutBagExplainsWhy()
        {
            DayState state = AtMinute(DayRules.BragarReturnAt);
            Assert.AreEqual(BragarPhase.Searching, state.bragarPhase);
            Assert.AreEqual(ActionAvailability.Blocked, DayRules.Check(state, ActionIds.BragarReturnBag).Availability);
        }

        [Test]
        public void SearchEndsByItselfAndTakesTheBagBack()
        {
            DayState state = AtMinute(DayRules.BragarDropAt);
            DayRules.Execute(state, ActionIds.BagTake);
            state.clock.minutes = DayRules.BragarReturnAt - 0.1f;
            DayRules.Tick(state, 1f);
            state.clock.Spend(DayRules.BragarSearchDuration);

            Assert.IsTrue(DayRules.Tick(state, 0.1f).BragarLeft);
            Assert.AreEqual(BragarPhase.FoundHimself, state.bragarPhase);
            Assert.IsFalse(state.inventory.Has(ItemIds.BragarBag));
            Assert.AreEqual(ActionAvailability.Ready, DayRules.Check(state, ActionIds.TerminalPlay).Availability);
        }
    }

    public sealed class ProdanovTests
    {
        private static DayState AtTen()
        {
            DayState state = DayState.CreateNew(5);
            state.clock.minutes = DayRules.ProdanovFrom;
            return state;
        }

        [Test]
        public void DistractsOncePerHour()
        {
            DayState state = AtTen();
            Assert.IsTrue(DayRules.TryProdanovDistraction(state));
            Assert.IsFalse(DayRules.TryProdanovDistraction(state));
            state.clock.Spend(DayRules.ProdanovCooldown + 1f);
            Assert.IsTrue(DayRules.TryProdanovDistraction(state));
        }

        [Test]
        public void NoDistractionOutsideHisHours()
        {
            DayState state = DayState.CreateNew(5);
            Assert.IsFalse(DayRules.TryProdanovDistraction(state));
        }

        [Test]
        public void DecliningStillCostsAFewMinutes()
        {
            DayState state = AtTen();
            float before = state.clock.minutes;
            Assert.IsTrue(DayRules.Execute(state, ActionIds.ProdanovDecline).Success);
            Assert.AreEqual(before + DayRules.DeclineMinutes, state.clock.minutes, 0.001f);
        }

        [Test]
        public void ChessCostsThirtyMinutesWhateverTheMove()
        {
            DayState state = AtTen();
            float before = state.clock.minutes;
            string anyMove = DayRules.Choices(state, ActionIds.ProdanovChess)[0].Id;

            Assert.IsTrue(DayRules.Execute(state, ActionIds.ProdanovChess, anyMove).Success);
            Assert.AreEqual(before + DayRules.ChessMinutes, state.clock.minutes, 0.001f);
            Assert.AreEqual(1, state.chessPlayed);
            Assert.AreEqual(ActionAvailability.Blocked, DayRules.Check(state, ActionIds.ProdanovChess).Availability);
        }

        [Test]
        public void ExactlyOneMoveWins()
        {
            int wins = 0;
            DayState probe = AtTen();
            foreach (ChoiceOption option in DayRules.Choices(probe, ActionIds.ProdanovChess))
            {
                DayState state = AtTen();
                int mood = state.stats.mood;
                DayRules.Execute(state, ActionIds.ProdanovChess, option.Id);
                if (state.stats.mood - mood == 10)
                {
                    wins++;
                }
            }

            Assert.AreEqual(1, wins);
        }

        [Test]
        public void InvalidMoveSpendsNothing()
        {
            DayState state = AtTen();
            float before = state.clock.minutes;
            Assert.IsFalse(DayRules.Execute(state, ActionIds.ProdanovChess, "e4").Success);
            Assert.AreEqual(before, state.clock.minutes);
        }
    }
}
