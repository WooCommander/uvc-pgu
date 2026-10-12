using System.Collections.Generic;

namespace Uvc.Core
{
    /// <summary>Phases of Bragar's forgotten-bag event.</summary>
    public enum BragarPhase
    {
        /// <summary>Has not passed through yet.</summary>
        Waiting,
        /// <summary>The bag lies in the far room (or is already in the hero's bag).</summary>
        BagLeft,
        /// <summary>Bragar is back and searches the far room: no games while he is there.</summary>
        Searching,
        Returned,
        FoundHimself
    }

    /// <summary>
    /// Visitors who interfere with the plan:
    /// Bragar forgets his bag and later blocks the game room while looking for it;
    /// Prodanov with his students offers chess to anyone passing by.
    /// </summary>
    public static partial class DayRules
    {
        public const float BragarDropAt = 11 * 60 + 30;
        public const float BragarReturnAt = 13 * 60 + 30;
        public const float BragarSearchDuration = 90;
        public const float ProdanovFrom = 10 * 60;
        public const float ProdanovUntil = 16 * 60;
        public const float ProdanovCooldown = 60;
        public const float ChessMinutes = 30;
        public const float DeclineMinutes = 3;

        private static readonly ChessPuzzle[] Puzzles =
        {
            new("Детский мат: белый ферзь на h5, слон на c4. Чёрный король на e8 зажат своими фигурами, пешка f7 без защиты.",
                "Фxf7#", "Сxf7+", "Фxe5+"),
            new("Белая ладья на e1. Чёрный король на g8 за пешками f7, g7, h7, задняя горизонталь пуста.",
                "Лe8#", "Лe7", "Крf2"),
            new("Белый король на f6, ферзь на g1. Чёрный король загнан в угол h8.",
                "Фg7#", "Фg8+", "Фh2+")
        };

        public static bool IsBragarSearching(DayState state) => state.bragarPhase == BragarPhase.Searching;

        public static bool IsBagOnFloor(DayState state) => state.bragarPhase is BragarPhase.BagLeft or BragarPhase.Searching &&
                                                          !state.HasFlag(Flags.BragarBagTaken);

        public static bool IsProdanovHolding(DayState state) =>
            state.clock.minutes >= ProdanovFrom && state.clock.minutes < ProdanovUntil;

        /// <summary>
        /// Prodanov calls out to a hero passing by. At most once per game hour,
        /// and never while the hero already plays or right after a game.
        /// </summary>
        public static bool TryProdanovDistraction(DayState state)
        {
            if (state.HasFlag(Flags.DayFinished) || !IsProdanovHolding(state) || state.IsOnCooldown("prodanov") || state.IsOnCooldown("chess"))
            {
                return false;
            }

            state.StartCooldown("prodanov", ProdanovCooldown);
            return true;
        }

        public static string ProdanovInvitation =>
            "Коллега, на минутку! Партию в шахматы? Задачка на один ход — девушки как раз спорят, решите ли вы её.";

        private static ChessPuzzle CurrentPuzzle(DayState state) => Puzzles[(state.seed + state.chessPlayed) % Puzzles.Length];

        private static void TickVisitors(DayState state, ref TickResult result)
        {
            float now = state.clock.minutes;
            if (state.bragarPhase == BragarPhase.Waiting && now >= BragarDropAt)
            {
                state.bragarPhase = BragarPhase.BagLeft;
                result.BragarPassed = true;
            }

            if (state.bragarPhase == BragarPhase.BagLeft && now >= BragarReturnAt)
            {
                state.bragarPhase = BragarPhase.Searching;
                state.bragarSearchEndsAt = state.clock.Absolute + BragarSearchDuration;
                result.BragarSearchStarted = true;
            }

            if (state.bragarPhase == BragarPhase.Searching && state.clock.Absolute >= state.bragarSearchEndsAt)
            {
                bool heroHadIt = state.inventory.TryRemove(ItemIds.BragarBag);
                state.bragarPhase = BragarPhase.FoundHimself;
                state.stats.ChangeBossReputation(heroHadIt ? -5 : -2);
                state.stats.ChangeRelationship(NpcIds.Bragar, heroHadIt ? -10 : 0);
                state.bragarMessage = heroHadIt
                    ? "Брагарь сам вытащил свою сумку из вашей. Неловко вышло. Репутация у руководства −5."
                    : "Брагарь нашёл сумку сам и ушёл, ворча. Репутация у руководства −2.";
                result.BragarLeft = true;
            }
        }

        private static ActionCheck CheckVisitors(DayState state, string actionId)
        {
            switch (actionId)
            {
                case ActionIds.BagTake:
                    return IsBagOnFloor(state) ? ActionCheck.Ready : ActionCheck.Hidden;

                case ActionIds.BragarReturnBag:
                    if (!IsBragarSearching(state))
                    {
                        return ActionCheck.Hidden;
                    }

                    return state.inventory.Has(ItemIds.BragarBag)
                        ? ActionCheck.Ready
                        : ActionCheck.Blocked("Сумки у вас нет — она где-то в этой аудитории");

                case ActionIds.ProdanovChess:
                    if (!IsProdanovHolding(state))
                    {
                        return ActionCheck.Hidden;
                    }

                    return state.IsOnCooldown("chess") ? ActionCheck.Blocked("Проданов расставляет фигуры заново") : ActionCheck.Ready;

                case ActionIds.ProdanovDecline:
                    return ActionCheck.Ready;

                default:
                    return ActionCheck.Hidden;
            }
        }

        private static ActionOutcome PerformVisitors(DayState state, string actionId, string choice)
        {
            PlayerStats stats = state.stats;
            switch (actionId)
            {
                case ActionIds.BagTake:
                    state.inventory.TryAdd(ItemIds.BragarBag);
                    state.SetFlag(Flags.BragarBagTaken);
                    return Done("Сумка Брагаря: мешок дискет и пакет, в котором что-то звякает. Лучше вернуть хозяину.", 2);

                case ActionIds.BragarReturnBag:
                    state.inventory.TryRemove(ItemIds.BragarBag);
                    state.bragarPhase = BragarPhase.Returned;
                    stats.ChangeBossReputation(8);
                    stats.ChangeRelationship(NpcIds.Bragar, 10);
                    stats.ChangeMood(5);
                    ActionOutcome returned = Say(NpcIds.Bragar,
                        "Моя! Дискеты для кафедры… и кое-что для комиссии. Вы меня очень выручили. Аудитория в вашем распоряжении. Репутация у руководства +8.",
                        5, autosave: true);
                    returned.BragarLeft = true;
                    return returned;

                case ActionIds.ProdanovChess:
                {
                    ChessPuzzle puzzle = CurrentPuzzle(state);
                    if (choice == null || System.Array.IndexOf(puzzle.Moves, choice) < 0)
                    {
                        return ActionOutcome.Fail("Ход не выбран.");
                    }

                    bool won = choice == puzzle.Moves[0];
                    state.chessPlayed++;
                    state.StartCooldown("chess", ProdanovCooldown);
                    state.StartCooldown("prodanov", ProdanovCooldown);
                    stats.ChangeMood(won ? 10 : 3);
                    stats.ChangeRelationship(NpcIds.Prodanov, won ? 5 : 2);
                    return Say(NpcIds.Prodanov,
                        won
                            ? $"{choice} — мат! Студентки аплодируют, Проданов уважительно кивает. Настроение +10. Ушло 30 минут."
                            : $"{choice}? Красиво, но нет: правильно было {puzzle.Moves[0]}. Студентки сочувственно вздыхают. Настроение +3. Ушло 30 минут.",
                        ChessMinutes);
                }

                case ActionIds.ProdanovDecline:
                    return Done("Пока вежливо отказывались, студентки успели спросить про пересдачу. Ушло 3 минуты.", DeclineMinutes);

                default:
                    return ActionOutcome.Fail("Неизвестное действие");
            }
        }

        private static string VisitorIntro(DayState state, string actionId)
        {
            return actionId == ActionIds.ProdanovChess ? "Мат в один ход.\n" + CurrentPuzzle(state).Position : string.Empty;
        }

        private static void AddVisitorChoices(DayState state, string actionId, List<ChoiceOption> options)
        {
            if (actionId != ActionIds.ProdanovChess)
            {
                return;
            }

            // Shuffle the display order deterministically so the answer is not always first.
            string[] moves = CurrentPuzzle(state).Moves;
            int shift = (state.seed + state.chessPlayed) % moves.Length;
            for (int index = 0; index < moves.Length; index++)
            {
                string move = moves[(index + shift) % moves.Length];
                options.Add(new ChoiceOption(move, move));
            }
        }

        private static void AddVisitorObjectives(DayState state, List<string> objectives)
        {
            if (IsBragarSearching(state))
            {
                objectives.Add(state.inventory.Has(ItemIds.BragarBag)
                    ? "Вернуть Брагарю сумку (ПК 7)"
                    : $"Брагарь ищет сумку в ПК 7 — пока он там, не поиграть ({GameClock.FormatDuration(state.bragarSearchEndsAt - state.clock.Absolute)})");
            }
            else if (state.bragarPhase == BragarPhase.BagLeft && state.inventory.Has(ItemIds.BragarBag))
            {
                objectives.Add("У вас сумка Брагаря — он за ней ещё вернётся");
            }
        }

        internal static void AddVisitorResultLines(DayState state, List<string> lines)
        {
            switch (state.bragarPhase)
            {
                case BragarPhase.Returned:
                    lines.Add("Брагарь: сумка возвращена, он благодарен");
                    break;
                case BragarPhase.FoundHimself:
                    lines.Add("Брагарь: нашёл сумку сам и остался недоволен");
                    break;
            }

            if (state.chessPlayed > 0)
            {
                lines.Add($"Шахматы с Продановым: партий {state.chessPlayed}");
            }
        }

        private sealed class ChessPuzzle
        {
            public ChessPuzzle(string position, params string[] moves)
            {
                Position = position;
                Moves = moves;
            }

            public string Position { get; }

            /// <summary>The first move is the correct one.</summary>
            public string[] Moves { get; }
        }
    }
}
