using System;
using System.Collections.Generic;

namespace Uvc.Core
{
    public struct TickResult
    {
        public bool EveningStarted;
        public bool DayOver;
        public bool PartyOpened;
        public bool MaimeskulExpired;
        public bool BragarPassed;
        public bool BragarSearchStarted;
        public bool BragarLeft;
    }

    /// <summary>
    /// Rules of the Friday scenario. Pure logic: no Unity types, no UI.
    /// Every action is checked first; a blocked action never spends time or items.
    /// </summary>
    public static partial class DayRules
    {
        public const float DeliveryDeadline = 16 * 60;
        public const float PartyQuestOpensAt = 17 * 60;
        public const float TalkCooldown = 60;
        public const float TeaCooldown = 60;
        public const float MaimeskulDuration = 24 * 60;
        public const float MaimeskulReactionCooldown = 60;

        public static TickResult Tick(DayState state, float realSeconds)
        {
            TickResult result = default;
            if (state.HasFlag(Flags.DayFinished))
            {
                return result;
            }

            state.clock.Tick(realSeconds);

            if (state.clock.minutes >= PartyQuestOpensAt && state.GetQuest(QuestIds.Party) == QuestStatus.Unavailable)
            {
                state.SetQuest(QuestIds.Party, QuestStatus.Available);
                result.PartyOpened = true;
            }

            if (state.clock.IsEvening && !state.HasFlag(Flags.EveningPrompted))
            {
                state.SetFlag(Flags.EveningPrompted);
                result.EveningStarted = true;
            }

            if (state.maimeskul.active && state.clock.Absolute >= state.maimeskul.expiresAt)
            {
                EndMaimeskul(state, false);
                result.MaimeskulExpired = true;
            }

            TickVisitors(state, ref result);
            result.DayOver = state.clock.IsOver;
            return result;
        }

        public static ActionCheck Check(DayState state, string actionId)
        {
            if (state.HasFlag(Flags.DayFinished))
            {
                return ActionCheck.Hidden;
            }

            if (ActionIds.IsTalk(actionId, out string talkNpc))
            {
                return talkNpc != NpcIds.Maimeskul || state.maimeskul.active ? ActionCheck.Ready : ActionCheck.Hidden;
            }

            if (ActionIds.IsInvite(actionId, out string inviteNpc))
            {
                if (!state.IsQuestAccepted(QuestIds.Party) || state.HasFlag(Flags.Invited(inviteNpc)) ||
                    Array.IndexOf(NpcIds.Friends, inviteNpc) < 0)
                {
                    return ActionCheck.Hidden;
                }

                return state.stats.GetRelationship(inviteNpc) < -20
                    ? ActionCheck.Blocked("Сейчас не в настроении: отношения испорчены")
                    : ActionCheck.Ready;
            }

            Inventory inventory = state.inventory;
            switch (actionId)
            {
                case ActionIds.BossTakeTask:
                    return state.GetQuest(QuestIds.Evening) == QuestStatus.Available ? ActionCheck.Ready : ActionCheck.Hidden;

                case ActionIds.BossDeliver:
                    if (!state.IsQuestAccepted(QuestIds.Evening))
                    {
                        return ActionCheck.Hidden;
                    }

                    if (!state.HasFlag(Flags.PrinterFixed))
                    {
                        return ActionCheck.Blocked("Принтер ещё не печатает");
                    }

                    return state.HasFlag(Flags.ProjectorFixed) ? ActionCheck.Ready : ActionCheck.Blocked("Проектор ещё не настроен");

                case ActionIds.SeregaParty:
                    if (state.GetQuest(QuestIds.Party) != QuestStatus.Available)
                    {
                        return ActionCheck.Hidden;
                    }

                    return inventory.CanAdd(ItemIds.Capelin) ? ActionCheck.Ready : ActionCheck.Blocked("Нет места в инвентаре для мойвы");

                case ActionIds.AndryukhaSecret:
                    return state.GetQuest(QuestIds.Maimeskul) == QuestStatus.Available ? ActionCheck.Ready : ActionCheck.Hidden;

                case ActionIds.AndryukhaRitual:
                    if (!state.IsQuestAccepted(QuestIds.Maimeskul) || state.maimeskul.active)
                    {
                        return ActionCheck.Hidden;
                    }

                    return MissingRitualItems(state) is { } missing
                        ? ActionCheck.Blocked("Не хватает: " + missing)
                        : ActionCheck.Ready;

                case ActionIds.AndryukhaLastErrand:
                    return state.maimeskul.active && state.GetQuest(QuestIds.LastErrand) == QuestStatus.Available
                        ? ActionCheck.Ready
                        : ActionCheck.Hidden;

                case ActionIds.ShelfArchive:
                    return state.IsQuestAccepted(QuestIds.LastErrand) && !inventory.Has(ItemIds.ArchiveSheet)
                        ? ActionCheck.Ready
                        : ActionCheck.Hidden;

                case ActionIds.CabinetReturnSheet:
                    if (!state.IsQuestAccepted(QuestIds.LastErrand))
                    {
                        return ActionCheck.Hidden;
                    }

                    return inventory.Has(ItemIds.ArchiveSheet) ? ActionCheck.Ready : ActionCheck.Blocked("Нужна архивная ведомость со склада");

                case ActionIds.JohnAdapter:
                    return state.IsQuestAccepted(QuestIds.Evening) && !state.HasFlag(Flags.AdapterReceived)
                        ? ActionCheck.Ready
                        : ActionCheck.Hidden;

                case ActionIds.VigoDiagnose:
                    if (!state.IsQuestAccepted(QuestIds.Evening) || state.HasFlag(Flags.PrinterFixed) || state.HasFlag(Flags.VigoDiagnosed))
                    {
                        return ActionCheck.Hidden;
                    }

                    return inventory.CanAdd(ItemIds.Cartridge) ? ActionCheck.Ready : ActionCheck.Blocked("Нет места в инвентаре");

                case ActionIds.StockSort:
                    if (state.HasFlag(Flags.StockSorted))
                    {
                        return ActionCheck.Hidden;
                    }

                    return inventory.CanAdd(ItemIds.Cartridge) && inventory.CanAdd(ItemIds.Paper) && FreeSlots(inventory) >= 2
                        ? ActionCheck.Ready
                        : ActionCheck.Blocked("Освободите два места в инвентаре");

                case ActionIds.ShelfScrewdriver:
                    return TakeOnce(inventory, ItemIds.Screwdriver);

                case ActionIds.ShelfOpener:
                    return TakeOnce(inventory, ItemIds.Opener);

                case ActionIds.PrinterInspect:
                case ActionIds.PrinterRepair:
                    return state.HasFlag(Flags.PrinterFixed) ? ActionCheck.Hidden : ActionCheck.Ready;

                case ActionIds.ProjectorSetup:
                    if (state.HasFlag(Flags.ProjectorFixed))
                    {
                        return ActionCheck.Hidden;
                    }

                    return inventory.Has(ItemIds.ProjectorAdapter) ? ActionCheck.Ready : ActionCheck.Blocked("Нужен переходник — он у Джона");

                case ActionIds.KettleTea:
                    return ActionCheck.Ready;

                case ActionIds.FridgeBeer:
                case ActionIds.FridgeLemonade:
                    if (!state.IsQuestAccepted(QuestIds.Party) || HasDrinks(inventory))
                    {
                        return ActionCheck.Hidden;
                    }

                    return inventory.CanAdd(actionId == ActionIds.FridgeBeer ? ItemIds.Beer : ItemIds.Lemonade)
                        ? ActionCheck.Ready
                        : ActionCheck.Blocked("Нет места в инвентаре");

                case ActionIds.TableParty:
                    if (!state.IsQuestAccepted(QuestIds.Party) || state.HasFlag(Flags.PartyDone))
                    {
                        return ActionCheck.Hidden;
                    }

                    return state.clock.IsEvening ? ActionCheck.Ready : ActionCheck.Blocked("Посиделки — после 18:00");

                case ActionIds.TerminalPlay:
                    return IsBragarSearching(state)
                        ? ActionCheck.Blocked("Брагарь ищет сумку — при нём не поиграешь")
                        : ActionCheck.Ready;

                case ActionIds.BenchPass:
                    return RitualPickup(state, ItemIds.LostPass);

                case ActionIds.CabinetJournal:
                    return RitualPickup(state, ItemIds.OldJournal);

                case ActionIds.MugsRitual:
                    return RitualPickup(state, ItemIds.RitualMug);

                default:
                    return CheckVisitors(state, actionId);
            }
        }

        public static ActionOutcome Execute(DayState state, string actionId, string choice = null)
        {
            ActionCheck check = Check(state, actionId);
            if (!check.IsReady)
            {
                return ActionOutcome.Fail(check.Reason ?? "Сейчас это недоступно");
            }

            ActionOutcome outcome = Perform(state, actionId, choice);
            state.clock.Spend(outcome.MinutesSpent);
            return outcome;
        }

        public static string ConfirmText(DayState state, string actionId) => actionId switch
        {
            ActionIds.AndryukhaRitual =>
                "Временное событие: Маймескул вернётся на этаж на 24 часа игрового времени. Он будет ходить по коридорам и устраивать взбучки. Завершить событие можно досрочно поручением Андрюхи. Провести ритуал?",
            ActionIds.TableParty =>
                $"Начать посиделки сейчас? Приглашено друзей: {state.InvitedCount}. Сцена завершит день и откроет итоги.",
            _ => "Подтвердить действие?"
        };

        public static string MinigameIntro(DayState state, string actionId)
        {
            switch (actionId)
            {
                case ActionIds.PrinterRepair:
                    string intro = "Симптомы: " + Symptoms(state.printerFault);
                    if (state.HasFlag(Flags.VigoDiagnosed) || state.printerFailures >= 2)
                    {
                        intro += $"\nПодсказка: похоже, что {FaultName(state.printerFault)}.";
                    }

                    return intro;
                case ActionIds.ProjectorSetup:
                    string text = "Проектор нашёл переходник, но экран синий. Какой вход выбрать?";
                    if (state.projectorFailures >= 2)
                    {
                        text += $"\nПодсказка: на кабеле наклейка «{ProjectorInputs.Label(state.projectorInput)}».";
                    }

                    return text;
                case ActionIds.StockSort:
                    string sort = $"Завхоз: «Разложи по полкам — тогда выдам картридж». Нужно не меньше {StockSorting.RequiredCorrect} из {StockSorting.Items.Length}.";
                    if (state.stockFailures >= 2)
                    {
                        sort += "\nПодсказка: всё, что кончается, — расходники; всё, что тянется, — кабели.";
                    }

                    return sort;
                case ActionIds.TerminalPlay:
                    KafedraMatch match = CurrentMatch(state);
                    return "КафедраCraft. Разведка доносит:\n" +
                           $"{KafedraMatch.LaneNames[0]}: {KafedraMatch.UnitName(match.EnemyIn(0))} (нужно силы: {match.RequiredPower(0)})\n" +
                           $"{KafedraMatch.LaneNames[1]}: {KafedraMatch.UnitName(match.EnemyIn(1))} (нужно силы: {match.RequiredPower(1)})\n" +
                           "Аспиранты бьют лаборантов, лаборанты — методистов, методисты — аспирантов. Контра = 2, зеркало = 1.";
                default:
                    return VisitorIntro(state, actionId);
            }
        }

        public static List<ChoiceOption> Choices(DayState state, string actionId)
        {
            List<ChoiceOption> options = new();
            switch (actionId)
            {
                case ActionIds.PrinterRepair:
                    options.Add(new ChoiceOption(Faults.Paper, "Заправить бумагу"));
                    options.Add(new ChoiceOption(Faults.Jam, "Устранить замятие"));
                    options.Add(new ChoiceOption(Faults.Cartridge, "Заменить картридж"));
                    break;
                case ActionIds.ProjectorSetup:
                    foreach (string input in ProjectorInputs.All)
                    {
                        options.Add(new ChoiceOption(input, ProjectorInputs.Label(input)));
                    }

                    break;
                default:
                    AddVisitorChoices(state, actionId, options);
                    break;
            }

            return options;
        }

        public static KafedraMatch CurrentMatch(DayState state) => KafedraMatch.Create(state.seed, state.matchesPlayed);

        public static List<string> Objectives(DayState state)
        {
            List<string> objectives = new();
            Inventory inventory = state.inventory;

            if (state.maimeskul.active)
            {
                objectives.Add($"Маймескул на этаже ещё {GameClock.FormatDuration(state.maimeskul.Remaining(state.clock.Absolute))} игрового времени");
            }

            switch (state.GetQuest(QuestIds.Evening))
            {
                case QuestStatus.Available:
                    objectives.Add("Зайти к руководителю за заданием");
                    break;
                case QuestStatus.Accepted:
                    bool printerFixed = state.HasFlag(Flags.PrinterFixed);
                    bool projectorFixed = state.HasFlag(Flags.ProjectorFixed);
                    if (printerFixed && projectorFixed)
                    {
                        objectives.Add("Сдать работу руководителю" + (state.clock.minutes < DeliveryDeadline ? " до 16:00" : " (уже поздно)"));
                        break;
                    }

                    if (!printerFixed)
                    {
                        objectives.Add(PrinterObjective(state));
                    }

                    if (!projectorFixed)
                    {
                        objectives.Add(inventory.Has(ItemIds.ProjectorAdapter)
                            ? "Настроить проектор в аудитории К1"
                            : "Взять переходник проектора у Джона (ПК 7)");
                    }

                    break;
            }

            switch (state.GetQuest(QuestIds.Party))
            {
                case QuestStatus.Available:
                    objectives.Add("Поговорить с Серёгой о вечере");
                    break;
                case QuestStatus.Accepted when !state.HasFlag(Flags.PartyDone):
                    if (state.InvitedCount < 2)
                    {
                        objectives.Add($"Пригласить друзей на вечер ({state.InvitedCount}/2)");
                    }

                    List<string> missing = new();
                    if (!inventory.Has(ItemIds.Capelin)) missing.Add("мойва");
                    if (!HasDrinks(inventory)) missing.Add("напитки");
                    if (!inventory.Has(ItemIds.Opener)) missing.Add("открывашка");
                    objectives.Add(missing.Count > 0
                        ? "Собрать к столу: " + string.Join(", ", missing)
                        : "После 18:00 начать посиделки в комнате отдыха");
                    break;
            }

            if (state.IsQuestAccepted(QuestIds.Maimeskul))
            {
                objectives.Add(MissingRitualItems(state) is { } missing
                    ? "Секрет Андрюхи: найти " + missing
                    : "Отнести находки Андрюхе");
            }

            if (state.IsQuestAccepted(QuestIds.LastErrand))
            {
                objectives.Add(state.inventory.Has(ItemIds.ArchiveSheet)
                    ? "Вернуть ведомость в кабинет руководителя"
                    : "Найти архивную ведомость на складе");
            }

            AddVisitorObjectives(state, objectives);

            if (objectives.Count == 0)
            {
                objectives.Add(state.clock.IsEvening ? "Можно идти домой" : "Свободное время: чай, разговоры, КафедраCraft");
            }

            return objectives;
        }

        /// <summary>Maimeskul scolds someone nearby. One reaction per target per 60 game minutes.</summary>
        public static bool TryMaimeskulReaction(DayState state, string targetId, out string message)
        {
            message = null;
            MaimeskulState maimeskul = state.maimeskul;
            float now = state.clock.Absolute;
            if (!maimeskul.active || DayState.FindCooldown(maimeskul.reactionCooldowns, targetId) > now)
            {
                return false;
            }

            DayState.SetCooldown(maimeskul.reactionCooldowns, targetId, now + MaimeskulReactionCooldown);
            if (targetId == NpcIds.Player)
            {
                state.stats.ChangeMood(-3);
                message = "Маймескул: «Почему без дела по коридору?!» Настроение −3.";
            }
            else
            {
                message = $"Маймескул устроил взбучку: {NpcIds.DisplayName(targetId)} в панике убегает.";
            }

            return true;
        }

        public static void FinishDay(DayState state)
        {
            if (state.IsQuestAccepted(QuestIds.Party) && !state.HasFlag(Flags.PartyDone))
            {
                state.partyOutcome = "skipped";
            }

            state.SetFlag(Flags.DayFinished);
        }

        private static ActionOutcome Perform(DayState state, string actionId, string choice)
        {
            PlayerStats stats = state.stats;
            Inventory inventory = state.inventory;
            float work = stats.WorkDurationMultiplier;

            if (ActionIds.IsTalk(actionId, out string talkNpc))
            {
                return Talk(state, talkNpc);
            }

            if (ActionIds.IsInvite(actionId, out string inviteNpc))
            {
                state.SetFlag(Flags.Invited(inviteNpc));
                stats.ChangeRelationship(inviteNpc, 2);
                return Say(inviteNpc, InviteReply(inviteNpc), 5);
            }

            switch (actionId)
            {
                case ActionIds.BossTakeTask:
                    state.SetQuest(QuestIds.Evening, QuestStatus.Accepted);
                    return Say(NpcIds.Boss,
                        "К 16:00 нужны распечатанные материалы и рабочий проектор в К1. Принтер опять капризничает — разберись. Переходник от проектора, говорят, утащили в дальнюю аудиторию.",
                        5, autosave: true);

                case ActionIds.BossDeliver:
                {
                    bool onTime = state.clock.minutes < DeliveryDeadline;
                    // Completion and reward are one state transition: a reload cannot pay twice.
                    state.SetQuest(QuestIds.Evening, QuestStatus.RewardClaimed);
                    stats.ChangeBossReputation(onTime ? 10 : 0);
                    stats.ChangeMood(onTime ? 5 : 0);
                    if (onTime)
                    {
                        state.SetFlag(Flags.DeliveredOnTime);
                    }

                    if (state.GetQuest(QuestIds.Party) == QuestStatus.Unavailable)
                    {
                        state.SetQuest(QuestIds.Party, QuestStatus.Available);
                    }

                    return Say(NpcIds.Boss,
                        onTime
                            ? "Успел! Материалы на месте, проектор светит. Репутация у руководства +10. Кстати, Серёга что-то затевает на вечер."
                            : "Поздновато, но сделано. Без премии, зато без выговора. Серёга, кажется, ищет тебя.",
                        5, autosave: true);
                }

                case ActionIds.SeregaParty:
                    state.SetQuest(QuestIds.Party, QuestStatus.Accepted);
                    inventory.TryAdd(ItemIds.Capelin);
                    stats.ChangeRelationship(NpcIds.Serega, 3);
                    return Say(NpcIds.Serega,
                        "Пятница же! После шести — в комнате отдыха. Мойву я принёс, держи. Позови Андрюху и Джона, возьми напитки из холодильника и открывашку со склада.",
                        5, autosave: true);

                case ActionIds.AndryukhaSecret:
                    state.SetQuest(QuestIds.Maimeskul, QuestStatus.Accepted);
                    return Say(NpcIds.Andryukha,
                        "Только между нами. Есть старый ритуал: журнал из шкафа в кабинете, забытый пропуск с лавки в коридоре и старая кружка из комнаты отдыха. Принесёшь — вернём Маймескула. На сутки. Будет весело.",
                        5);

                case ActionIds.AndryukhaRitual:
                {
                    inventory.TryRemove(ItemIds.OldJournal);
                    inventory.TryRemove(ItemIds.LostPass);
                    inventory.TryRemove(ItemIds.RitualMug);
                    state.SetQuest(QuestIds.Maimeskul, QuestStatus.RewardClaimed);
                    state.SetQuest(QuestIds.LastErrand, QuestStatus.Available);
                    state.SetFlag(Flags.MaimeskulUsedThisWeek);
                    MaimeskulState maimeskul = state.maimeskul;
                    maimeskul.active = true;
                    maimeskul.endedEarly = false;
                    maimeskul.startedAt = state.clock.Absolute;
                    maimeskul.expiresAt = state.clock.Absolute + MaimeskulDuration;
                    maimeskul.reactionCooldowns.Clear();
                    stats.ChangeRelationship(NpcIds.Andryukha, 10);
                    ActionOutcome outcome = Say(NpcIds.Andryukha,
                        "Свет мигнул, в коридоре запахло старыми ведомостями… Он вернулся! Если станет совсем невмоготу — подойди, есть «последнее поручение».",
                        10, autosave: true);
                    outcome.MaimeskulStarted = true;
                    return outcome;
                }

                case ActionIds.AndryukhaLastErrand:
                    state.SetQuest(QuestIds.LastErrand, QuestStatus.Accepted);
                    return Say(NpcIds.Andryukha,
                        "Он не уйдёт, пока не увидит архивную ведомость на месте. Она где-то на складе. Отнеси её в кабинет руководителя.",
                        5);

                case ActionIds.ShelfArchive:
                    inventory.TryAdd(ItemIds.ArchiveSheet);
                    return Done("Архивная ведомость нашлась под коробкой с мышками.", 5);

                case ActionIds.CabinetReturnSheet:
                {
                    inventory.TryRemove(ItemIds.ArchiveSheet);
                    state.SetQuest(QuestIds.LastErrand, QuestStatus.RewardClaimed);
                    EndMaimeskul(state, true);
                    stats.ChangeMood(10);
                    ActionOutcome outcome = Say(NpcIds.Maimeskul,
                        "Ведомость на месте. Порядок восстановлен. Ухожу… но я буду следить.", 5, autosave: true);
                    outcome.MaimeskulEnded = true;
                    return outcome;
                }

                case ActionIds.JohnAdapter:
                    inventory.TryAdd(ItemIds.ProjectorAdapter);
                    state.SetFlag(Flags.AdapterReceived);
                    stats.ChangeRelationship(NpcIds.John, 3);
                    return Say(NpcIds.John,
                        "Переходник? Держи, мы им монитор для турнира подключали. Заходи потом на партию в КафедраCraft — без обязательств.",
                        5);

                case ActionIds.VigoDiagnose:
                    state.SetFlag(Flags.VigoDiagnosed);
                    inventory.TryAdd(ItemIds.Cartridge);
                    stats.ChangeEnergy(-5);
                    stats.ChangeRelationship(NpcIds.Vigo, 8);
                    return Say(NpcIds.Vigo,
                        $"Вместе прогнали диагностику. Судя по логам, {FaultName(state.printerFault)}. Держи запасной картридж из моей заначки — пригодится.",
                        30 * work);

                case ActionIds.StockSort:
                {
                    int correct = StockSorting.CountCorrect(choice);
                    if (correct < StockSorting.RequiredCorrect)
                    {
                        state.stockFailures++;
                        return new ActionOutcome
                        {
                            Speaker = NpcIds.DisplayName(NpcIds.Storekeeper),
                            SpeakerId = NpcIds.Storekeeper,
                            Message = $"Завхоз: «{correct} из {StockSorting.Items.Length}? Переделывай». Ушло 5 минут.",
                            MinutesSpent = 5
                        };
                    }

                    state.SetFlag(Flags.StockSorted);
                    inventory.TryAdd(ItemIds.Cartridge);
                    inventory.TryAdd(ItemIds.Paper);
                    stats.ChangeMess(-5);
                    stats.ChangeEnergy(-4);
                    stats.ChangeRelationship(NpcIds.Storekeeper, 5);
                    return Say(NpcIds.Storekeeper, "Вот это порядок. Держи картридж и пачку бумаги. Бардак −5.", 15 * work, autosave: true);
                }

                case ActionIds.ShelfScrewdriver:
                    inventory.TryAdd(ItemIds.Screwdriver);
                    return Done("Отвёртка взята. Инструмент не расходуется.", 2);

                case ActionIds.ShelfOpener:
                    inventory.TryAdd(ItemIds.Opener);
                    return Done("Открывашка взята.", 2);

                case ActionIds.PrinterInspect:
                    state.SetFlag(Flags.PrinterInspected);
                    return Done("Принтер: " + Symptoms(state.printerFault), 3);

                case ActionIds.PrinterRepair:
                    return RepairPrinter(state, choice, work);

                case ActionIds.ProjectorSetup:
                    if (choice != state.projectorInput)
                    {
                        state.projectorFailures++;
                        return new ActionOutcome { Message = "Синий экран и надпись «Нет сигнала». Ушло 5 минут.", MinutesSpent = 5 };
                    }

                    inventory.TryRemove(ItemIds.ProjectorAdapter);
                    state.SetFlag(Flags.ProjectorFixed);
                    stats.ChangeEnergy(-5);
                    stats.ChangeRelationship(NpcIds.Lecturer, 5);
                    return Done("Проектор показывает рабочий стол. Аудитория К1 готова.", 15 * work, autosave: true);

                case ActionIds.KettleTea:
                {
                    bool fresh = !state.IsOnCooldown("tea");
                    state.StartCooldown("tea", TeaCooldown);
                    stats.ChangeEnergy(fresh ? 10 : 5);
                    stats.ChangeMood(fresh ? 5 : 2);
                    return Done(fresh
                        ? "Горячий чай: энергия +10, настроение +5."
                        : "Уже третья кружка… Энергия +5, настроение +2.", 10);
                }

                case ActionIds.FridgeBeer:
                    inventory.TryAdd(ItemIds.Beer);
                    return Done("Пиво из холодильника отложено к вечеру.", 2);

                case ActionIds.FridgeLemonade:
                    inventory.TryAdd(ItemIds.Lemonade);
                    return Done("Лимонад отложен к вечеру. Безалкогольный вариант ничем не хуже.", 2);

                case ActionIds.TableParty:
                    return HoldParty(state);

                case ActionIds.TerminalPlay:
                {
                    KafedraMatch match = CurrentMatch(state);
                    if (!match.TryResolve(choice, out int[] power, out bool won))
                    {
                        return ActionOutcome.Fail("Матч не начался: нужно ровно три решения.");
                    }

                    state.matchesPlayed++;
                    if (won)
                    {
                        state.matchesWon++;
                        stats.ChangeMood(10);
                        stats.ChangeRelationship(NpcIds.John, 3);
                    }
                    else
                    {
                        stats.ChangeMood(5);
                    }

                    string score = $"Сила: слева {power[0]}/{match.RequiredPower(0)}, справа {power[1]}/{match.RequiredPower(1)}.";
                    return Done(won
                        ? $"Победа! {score} Настроение +10. Ушло 30 минут."
                        : $"Поражение, но было весело. {score} Настроение +5. Ушло 30 минут.", 30);
                }

                case ActionIds.BenchPass:
                    inventory.TryAdd(ItemIds.LostPass);
                    return Done("Под лавкой — пропуск 2004 года. Фото не узнать.", 2);

                case ActionIds.CabinetJournal:
                    inventory.TryAdd(ItemIds.OldJournal);
                    return Done("Старый журнал с пометками красной ручкой. Кажется, он смотрит на вас.", 2);

                case ActionIds.MugsRitual:
                    inventory.TryAdd(ItemIds.RitualMug);
                    return Done("Старая кружка с надписью «Лучшему ревизору».", 2);

                default:
                    return PerformVisitors(state, actionId, choice);
            }
        }

        private static ActionOutcome RepairPrinter(DayState state, string choice, float work)
        {
            Inventory inventory = state.inventory;
            if (choice != state.printerFault)
            {
                state.printerFailures++;
                return new ActionOutcome { Message = "Не помогло: принтер всё так же недоволен. Ушло 10 минут.", MinutesSpent = 10 };
            }

            string requiredItem = state.printerFault switch
            {
                Faults.Paper => ItemIds.Paper,
                Faults.Jam => ItemIds.Screwdriver,
                _ => ItemIds.Cartridge
            };

            if (!inventory.Has(requiredItem))
            {
                string hint = requiredItem == ItemIds.Screwdriver ? "она на складе" : "спросите завхоза или Vigo";
                return ActionOutcome.Fail($"Верно, но нужен предмет: {ItemCatalog.NameOf(requiredItem)} — {hint}.");
            }

            if (!ItemCatalog.Get(requiredItem).Tool)
            {
                inventory.TryRemove(requiredItem);
            }

            state.stats.ChangeEnergy(-8);
            if (state.printerFault != Faults.Cartridge)
            {
                state.printerFault = Faults.Cartridge;
                state.printerFailures = 0;
                return Done("Принтер ожил, но тестовый лист вышел белым: картридж пуст.", 20 * work);
            }

            state.SetFlag(Flags.PrinterFixed);
            state.stats.ChangeBossReputation(5);
            return Done("Тестовая страница напечатана, материалы готовы! Репутация у руководства +5.", 20 * work, autosave: true);
        }

        private static ActionOutcome HoldParty(DayState state)
        {
            Inventory inventory = state.inventory;
            PlayerStats stats = state.stats;
            int friends = state.InvitedCount;
            bool food = inventory.Has(ItemIds.Capelin);
            bool drinks = HasDrinks(inventory);
            bool opener = inventory.Has(ItemIds.Opener);
            state.SetFlag(Flags.PartyDone);
            state.SetQuest(QuestIds.Party, QuestStatus.RewardClaimed);

            int warmth = 0;
            foreach (string friend in NpcIds.Friends)
            {
                if (state.HasFlag(Flags.Invited(friend)))
                {
                    warmth += stats.GetRelationship(friend);
                    stats.ChangeRelationship(friend, 5);
                }
            }

            stats.ChangeRelationship(NpcIds.Serega, 5);
            if (state.GetQuest(QuestIds.Evening) == QuestStatus.RewardClaimed)
            {
                warmth += 10;
            }

            string guests = friends == 0 ? "Пришёл только Серёга" : $"Пришло друзей: {friends} + Серёга";
            if (friends >= 2 && food && drinks && opener)
            {
                state.partyOutcome = warmth >= 20 ? "legendary" : "great";
                stats.ChangeMood(state.partyOutcome == "legendary" ? 20 : 15);
                return new ActionOutcome
                {
                    Success = true,
                    Speaker = "Вечер",
                    Message = $"{guests}. Мойва, разговоры и байки до темноты. " +
                              (state.partyOutcome == "legendary" ? "Этот вечер ещё долго будут вспоминать!" : "Хорошо посидели."),
                    MinutesSpent = 60,
                    EndsDay = true
                };
            }

            state.partyOutcome = "modest";
            stats.ChangeMood(5);
            List<string> missing = new();
            if (!food) missing.Add("мойвы");
            if (!drinks) missing.Add("напитков");
            if (!opener) missing.Add("открывашки");
            string reason = missing.Count > 0 ? "Не хватило " + string.Join(", ", missing) + ". " : string.Empty;
            return new ActionOutcome
            {
                Success = true,
                Speaker = "Вечер",
                Message = $"{guests}. {reason}Посидели скромно, с чаем и разговорами. Тоже неплохо.",
                MinutesSpent = 30,
                EndsDay = true
            };
        }

        private static ActionOutcome Talk(DayState state, string npcId)
        {
            bool fresh = !state.IsOnCooldown("talk:" + npcId);
            if (fresh && npcId != NpcIds.Maimeskul)
            {
                state.StartCooldown("talk:" + npcId, TalkCooldown);
                state.stats.ChangeMood(5);
                state.stats.ChangeRelationship(npcId, 3);
            }

            if (npcId == NpcIds.Andryukha && !state.HasFlag(Flags.MetAndryukha))
            {
                state.SetFlag(Flags.MetAndryukha);
                if (!state.HasFlag(Flags.MaimeskulUsedThisWeek) && state.GetQuest(QuestIds.Maimeskul) == QuestStatus.Unavailable)
                {
                    state.SetQuest(QuestIds.Maimeskul, QuestStatus.Available);
                }
            }

            string line = TalkLine(state, npcId, fresh);
            if (fresh && npcId != NpcIds.Maimeskul)
            {
                line += " (настроение +5)";
            }

            return Say(npcId, line, 5);
        }

        private static string TalkLine(DayState state, string npcId, bool fresh)
        {
            if (!fresh)
            {
                return npcId switch
                {
                    NpcIds.Boss => "Работаем, работаем.",
                    NpcIds.Maimeskul => "Не отвлекайте. Я составляю акт.",
                    _ => "Мы же только что говорили. Давай потом."
                };
            }

            return npcId switch
            {
                NpcIds.Andryukha when state.maimeskul.active => "Ну как тебе? Атмосфера, а! Главное — не попадайся ему в коридоре.",
                NpcIds.Andryukha => "Слушай, есть одна тема… Секретная. Спроси меня про «секретное дело».",
                NpcIds.Serega => "Главное — пережить пятницу. А вечером — мойва. Святое.",
                NpcIds.Bogdan when IsBragarSearching(state) => "При Брагаре даже монитор не включишь. Сидим, делаем вид, что читаем методичку.",
                NpcIds.Bogdan => "Ещё одну катку — и на пару. Честно. Ну, может, две.",
                NpcIds.Bragar => "Никто не видел мою сумку? Там… очень важные дискеты. И кое-что ещё.",
                NpcIds.Prodanov => "Шахматы — та же кафедра: кто первым сделал ход, тот и прав.",
                NpcIds.StudentA => "А вы не знаете, когда пересдача? Проданов говорит — «как партия ляжет».",
                NpcIds.StudentB => "Мы просто смотрим. Он обещал, что шахматы засчитаются как зачёт. Наверное.",
                NpcIds.John => "В дальней аудитории компы не выключаются никогда. Турнир по КафедраCraft — это серьёзно.",
                NpcIds.Vigo => "Если что-то не работает — сначала проверь, включено ли оно. Я серьёзно.",
                NpcIds.Lecturer => state.HasFlag(Flags.ProjectorFixed)
                    ? "Спасибо за проектор! Студенты даже не успели заскучать."
                    : "Без проектора у меня пара превращается в радиоспектакль.",
                NpcIds.Storekeeper => "Всё, что на складе, — под отчёт. Даже скрепки.",
                NpcIds.Boss => "Пятница — не повод расслабляться. Хотя… ладно, повод.",
                NpcIds.Maimeskul => "Почему пыль на мониторе? Где журнал инструктажа? Кто так ставит стулья?!",
                _ => "…"
            };
        }

        private static string InviteReply(string npcId) => npcId switch
        {
            NpcIds.Andryukha => "Мойва? Пятница? Конечно, буду. Может, ещё и историю расскажу.",
            NpcIds.John => "Добью матч — и приду. Честно.",
            NpcIds.Vigo => "Если ничего не сгорит до шести — я с вами.",
            _ => "Приду!"
        };

        private static string PrinterObjective(DayState state)
        {
            bool needsCartridge = state.printerFault == Faults.Cartridge;
            if (needsCartridge && !state.inventory.Has(ItemIds.Cartridge))
            {
                return "Достать картридж: склад (завхоз) или Vigo";
            }

            return "Починить принтер в рабочей комнате (ПК)";
        }

        private static string Symptoms(string fault) => fault switch
        {
            Faults.Paper => "лоток пуст, мигает жёлтая лампочка, принтер обиженно молчит.",
            Faults.Jam => "скрежет, из задней крышки торчит смятый лист.",
            _ => "листы выходят бледными и полосатыми."
        };

        private static string FaultName(string fault) => fault switch
        {
            Faults.Paper => "кончилась бумага",
            Faults.Jam => "замятие бумаги",
            _ => "пустой картридж"
        };

        private static string MissingRitualItems(DayState state)
        {
            List<string> missing = new();
            if (!state.inventory.Has(ItemIds.OldJournal)) missing.Add("старый журнал");
            if (!state.inventory.Has(ItemIds.LostPass)) missing.Add("забытый пропуск");
            if (!state.inventory.Has(ItemIds.RitualMug)) missing.Add("старую кружку");
            return missing.Count > 0 ? string.Join(", ", missing) : null;
        }

        private static ActionCheck RitualPickup(DayState state, string itemId)
        {
            return state.IsQuestAccepted(QuestIds.Maimeskul) && !state.inventory.Has(itemId) ? ActionCheck.Ready : ActionCheck.Hidden;
        }

        private static ActionCheck TakeOnce(Inventory inventory, string itemId)
        {
            if (inventory.Has(itemId))
            {
                return ActionCheck.Hidden;
            }

            return inventory.CanAdd(itemId) ? ActionCheck.Ready : ActionCheck.Blocked("Нет места в инвентаре");
        }

        private static bool HasDrinks(Inventory inventory) => inventory.Has(ItemIds.Beer) || inventory.Has(ItemIds.Lemonade);

        private static int FreeSlots(Inventory inventory) => Inventory.SlotCount - inventory.slots.Count;

        private static void EndMaimeskul(DayState state, bool early)
        {
            state.maimeskul.active = false;
            state.maimeskul.endedEarly = early;
            state.maimeskul.reactionCooldowns.Clear();
            if (state.GetQuest(QuestIds.LastErrand) != QuestStatus.RewardClaimed)
            {
                state.SetQuest(QuestIds.LastErrand, QuestStatus.Cancelled);
            }
        }

        private static ActionOutcome Say(string npcId, string message, float minutes, bool autosave = false)
        {
            return new ActionOutcome
            {
                Success = true,
                Speaker = NpcIds.DisplayName(npcId),
                SpeakerId = npcId,
                Message = message,
                MinutesSpent = minutes,
                Autosave = autosave
            };
        }

        private static ActionOutcome Done(string message, float minutes, bool autosave = false)
        {
            return new ActionOutcome { Success = true, Message = message, MinutesSpent = minutes, Autosave = autosave };
        }
    }
}
