namespace Uvc.Core
{
    public static class NpcIds
    {
        public const string Player = "player";
        public const string Andryukha = "npc_andryukha";
        public const string Serega = "npc_serega";
        public const string John = "npc_john";
        public const string Vigo = "npc_vigo";
        public const string Lecturer = "npc_lecturer";
        public const string Storekeeper = "npc_storekeeper";
        public const string Boss = "npc_boss";
        public const string Maimeskul = "npc_maimeskul";
        public const string Bogdan = "npc_bogdan";
        public const string Bragar = "npc_bragar";
        public const string Prodanov = "npc_prodanov";
        public const string StudentA = "npc_student_a";
        public const string StudentB = "npc_student_b";

        public static readonly string[] Friends = { Andryukha, John, Vigo };

        public static string DisplayName(string npcId)
        {
            return npcId switch
            {
                Andryukha => "Андрюха",
                Serega => "Серёга",
                John => "Джон",
                Vigo => "Vigo",
                Lecturer => "Преподаватель",
                Storekeeper => "Завхоз",
                Boss => "Руководитель",
                Maimeskul => "Маймескул",
                Bogdan => "Богдан",
                Bragar => "Брагарь",
                Prodanov => "Проданов",
                StudentA => "Студентка",
                StudentB => "Студентка",
                _ => npcId
            };
        }
    }

    public static class QuestIds
    {
        public const string Evening = "q_evening";
        public const string Party = "q_party";
        public const string Maimeskul = "q_maimeskul";
        public const string LastErrand = "q_last_errand";
    }

    public static class ItemIds
    {
        public const string Cartridge = "cartridge";
        public const string Paper = "paper";
        public const string Screwdriver = "screwdriver";
        public const string ProjectorAdapter = "projector_adapter";
        public const string Capelin = "capelin";
        public const string Beer = "beer";
        public const string Lemonade = "lemonade";
        public const string Opener = "opener";
        public const string OldJournal = "old_journal";
        public const string LostPass = "lost_pass";
        public const string RitualMug = "ritual_mug";
        public const string ArchiveSheet = "archive_sheet";
        public const string BragarBag = "bragar_bag";
    }

    public static class Flags
    {
        public const string PrinterFixed = "printer_fixed";
        public const string PrinterInspected = "printer_inspected";
        public const string ProjectorFixed = "projector_fixed";
        public const string AdapterReceived = "adapter_received";
        public const string StockSorted = "stock_sorted";
        public const string VigoDiagnosed = "vigo_diagnosed";
        public const string DeliveredOnTime = "delivered_on_time";
        public const string MetAndryukha = "met_andryukha";
        public const string EveningPrompted = "evening_prompted";
        public const string PartyDone = "party_done";
        public const string DayFinished = "day_finished";
        public const string MaimeskulUsedThisWeek = "maimeskul_used_week";
        public const string BragarBagTaken = "bragar_bag_taken";

        public static string Invited(string npcId) => "invited:" + npcId;
    }

    public static class Faults
    {
        public const string Paper = "paper";
        public const string Jam = "jam";
        public const string Cartridge = "cartridge";

        public static readonly string[] All = { Paper, Jam, Cartridge };
    }
}
