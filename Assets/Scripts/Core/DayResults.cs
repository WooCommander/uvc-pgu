using System.Collections.Generic;

namespace Uvc.Core
{
    public sealed class DayResult
    {
        public string Grade;
        public int Score;
        public readonly List<string> Lines = new();
    }

    public static class DayResults
    {
        public const string GradeSaved = "День спасён";
        public const string GradeMixed = "С переменным успехом";
        public const string GradeExplain = "Есть что объяснить";

        public static DayResult Compute(DayState state)
        {
            DayResult result = new();
            PlayerStats stats = state.stats;

            switch (state.GetQuest(QuestIds.Evening))
            {
                case QuestStatus.RewardClaimed when state.HasFlag(Flags.DeliveredOnTime):
                    result.Score += 3;
                    result.Lines.Add("«Успеть к вечеру»: сдано вовремя");
                    break;
                case QuestStatus.RewardClaimed:
                    result.Score += 2;
                    result.Lines.Add("«Успеть к вечеру»: сдано с опозданием");
                    break;
                case QuestStatus.Accepted:
                    int parts = (state.HasFlag(Flags.PrinterFixed) ? 1 : 0) + (state.HasFlag(Flags.ProjectorFixed) ? 1 : 0);
                    result.Lines.Add($"«Успеть к вечеру»: не сдано (готово {parts} из 2)");
                    break;
                default:
                    result.Lines.Add("«Успеть к вечеру»: задание так и не получено");
                    break;
            }

            switch (state.partyOutcome)
            {
                case "legendary":
                    result.Score += 3;
                    result.Lines.Add("Вечер: легендарные посиделки с мойвой");
                    break;
                case "great":
                    result.Score += 2;
                    result.Lines.Add("Вечер: хорошо посидели с друзьями");
                    break;
                case "modest":
                    result.Score += 1;
                    result.Lines.Add("Вечер: скромные посиделки");
                    break;
                case "skipped":
                    result.Lines.Add("Вечер: друзья ждали, но встреча не состоялась");
                    break;
                default:
                    result.Lines.Add("Вечер: обычный пятничный вечер дома");
                    break;
            }

            if (stats.bossReputation >= 60)
            {
                result.Score += 1;
            }

            if (stats.mood >= 70)
            {
                result.Score += 1;
            }

            if (state.matchesPlayed > 0)
            {
                result.Lines.Add($"КафедраCraft: побед {state.matchesWon} из {state.matchesPlayed}");
            }

            if (state.maimeskul.active)
            {
                result.Lines.Add($"Маймескул остаётся на этаже ещё {GameClock.FormatDuration(state.maimeskul.Remaining(state.clock.Absolute))} игрового времени");
            }
            else if (state.HasFlag(Flags.MaimeskulUsedThisWeek))
            {
                result.Lines.Add(state.maimeskul.endedEarly
                    ? "Маймескул: ушёл досрочно — ведомость на месте"
                    : "Маймескул: срок вышел, этаж выдохнул");
            }

            DayRules.AddVisitorResultLines(state, result.Lines);

            List<string> friends = new();
            foreach (Relationship relationship in stats.relationships)
            {
                if (relationship.value != 0)
                {
                    friends.Add($"{NpcIds.DisplayName(relationship.npcId)} {relationship.value:+#;-#}");
                }
            }

            if (friends.Count > 0)
            {
                result.Lines.Add("Отношения: " + string.Join(", ", friends));
            }

            result.Lines.Add($"Энергия {stats.energy} · настроение {stats.mood} · репутация у руководства {stats.bossReputation} · бардак {stats.mess}");
            result.Grade = result.Score >= 5 ? GradeSaved : result.Score >= 3 ? GradeMixed : GradeExplain;
            return result;
        }
    }
}
