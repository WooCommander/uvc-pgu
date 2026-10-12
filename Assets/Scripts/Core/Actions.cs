using System.Collections.Generic;

namespace Uvc.Core
{
    public enum ActionAvailability
    {
        Hidden,
        Blocked,
        Ready
    }

    public enum ActionKind
    {
        /// <summary>Executes right after the hero reaches the object.</summary>
        Instant,
        /// <summary>Opens a mini-game panel; the choice is passed to <see cref="DayRules.Execute"/>.</summary>
        Choice,
        /// <summary>Asks for confirmation with a warning first.</summary>
        Confirm
    }

    public readonly struct ActionCheck
    {
        private ActionCheck(ActionAvailability availability, string reason)
        {
            Availability = availability;
            Reason = reason;
        }

        public ActionAvailability Availability { get; }
        public string Reason { get; }
        public bool IsReady => Availability == ActionAvailability.Ready;

        public static ActionCheck Hidden => new(ActionAvailability.Hidden, null);
        public static ActionCheck Ready => new(ActionAvailability.Ready, null);
        public static ActionCheck Blocked(string reason) => new(ActionAvailability.Blocked, reason);
    }

    public sealed class ActionOutcome
    {
        public bool Success;
        public string Speaker;
        public string SpeakerId;
        public string Message;
        public float MinutesSpent;
        public bool EndsDay;
        public bool MaimeskulStarted;
        public bool MaimeskulEnded;
        public bool BragarLeft;
        public bool Autosave;

        public static ActionOutcome Fail(string message) => new() { Message = message };
    }

    public readonly struct ChoiceOption
    {
        public ChoiceOption(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    public static class ActionIds
    {
        public const string BossTakeTask = "boss.take_task";
        public const string BossDeliver = "boss.deliver";
        public const string SeregaParty = "serega.party";
        public const string AndryukhaSecret = "andryukha.secret";
        public const string AndryukhaRitual = "andryukha.ritual";
        public const string AndryukhaLastErrand = "andryukha.last_errand";
        public const string JohnAdapter = "john.adapter";
        public const string VigoDiagnose = "vigo.diagnose";
        public const string StockSort = "storekeeper.sort";
        public const string ShelfScrewdriver = "shelf.screwdriver";
        public const string ShelfOpener = "shelf.opener";
        public const string ShelfArchive = "shelf.archive";
        public const string PrinterInspect = "printer.inspect";
        public const string PrinterRepair = "printer.repair";
        public const string ProjectorSetup = "projector.setup";
        public const string KettleTea = "kettle.tea";
        public const string FridgeBeer = "fridge.beer";
        public const string FridgeLemonade = "fridge.lemonade";
        public const string TableParty = "table.party";
        public const string TerminalPlay = "terminal.play";
        public const string BenchPass = "bench.pass";
        public const string CabinetJournal = "cabinet.journal";
        public const string MugsRitual = "mugs.ritual";
        public const string CabinetReturnSheet = "cabinet.return_sheet";
        public const string BagTake = "bag.take";
        public const string BragarReturnBag = "bragar.return_bag";
        public const string ProdanovChess = "prodanov.chess";
        public const string ProdanovDecline = "prodanov.decline";

        private const string TalkPrefix = "talk:";
        private const string InvitePrefix = "invite:";

        public static string Talk(string npcId) => TalkPrefix + npcId;
        public static string Invite(string npcId) => InvitePrefix + npcId;

        public static bool IsTalk(string actionId, out string npcId) => TryStrip(actionId, TalkPrefix, out npcId);
        public static bool IsInvite(string actionId, out string npcId) => TryStrip(actionId, InvitePrefix, out npcId);

        private static bool TryStrip(string actionId, string prefix, out string rest)
        {
            if (actionId != null && actionId.StartsWith(prefix))
            {
                rest = actionId.Substring(prefix.Length);
                return true;
            }

            rest = null;
            return false;
        }
    }

    public static class ActionCatalog
    {
        private static readonly Dictionary<string, string> Titles = new()
        {
            [ActionIds.BossTakeTask] = "Получить задание",
            [ActionIds.BossDeliver] = "Сдать работу",
            [ActionIds.SeregaParty] = "Обсудить вечер",
            [ActionIds.AndryukhaSecret] = "Секретное дело",
            [ActionIds.AndryukhaRitual] = "Провести ритуал",
            [ActionIds.AndryukhaLastErrand] = "Последнее поручение",
            [ActionIds.JohnAdapter] = "Попросить переходник",
            [ActionIds.VigoDiagnose] = "Помочь с диагностикой",
            [ActionIds.StockSort] = "Разобрать оборудование",
            [ActionIds.ShelfScrewdriver] = "Взять отвёртку",
            [ActionIds.ShelfOpener] = "Взять открывашку",
            [ActionIds.ShelfArchive] = "Найти ведомость",
            [ActionIds.PrinterInspect] = "Осмотреть",
            [ActionIds.PrinterRepair] = "Починить",
            [ActionIds.ProjectorSetup] = "Настроить",
            [ActionIds.KettleTea] = "Попить чаю",
            [ActionIds.FridgeBeer] = "Взять пиво",
            [ActionIds.FridgeLemonade] = "Взять лимонад",
            [ActionIds.TableParty] = "Начать посиделки",
            [ActionIds.TerminalPlay] = "Сыграть в КафедраCraft",
            [ActionIds.BenchPass] = "Поднять пропуск",
            [ActionIds.CabinetJournal] = "Взять старый журнал",
            [ActionIds.MugsRitual] = "Взять старую кружку",
            [ActionIds.CabinetReturnSheet] = "Вернуть ведомость",
            [ActionIds.BagTake] = "Взять сумку",
            [ActionIds.BragarReturnBag] = "Отдать сумку",
            [ActionIds.ProdanovChess] = "Сыграть в шахматы",
            [ActionIds.ProdanovDecline] = "Отказаться"
        };

        public static string Title(string actionId)
        {
            if (ActionIds.IsTalk(actionId, out _))
            {
                return "Поговорить";
            }

            if (ActionIds.IsInvite(actionId, out _))
            {
                return "Пригласить на вечер";
            }

            return Titles.TryGetValue(actionId, out string title) ? title : actionId;
        }

        public static ActionKind Kind(string actionId) => actionId switch
        {
            ActionIds.StockSort => ActionKind.Choice,
            ActionIds.PrinterRepair => ActionKind.Choice,
            ActionIds.ProjectorSetup => ActionKind.Choice,
            ActionIds.TerminalPlay => ActionKind.Choice,
            ActionIds.ProdanovChess => ActionKind.Choice,
            ActionIds.AndryukhaRitual => ActionKind.Confirm,
            ActionIds.TableParty => ActionKind.Confirm,
            _ => ActionKind.Instant
        };
    }
}
