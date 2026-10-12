using System;
using System.Collections.Generic;

namespace Uvc.Core
{
    /// <summary>act_stock: sort five things from the storeroom into three categories.</summary>
    public static class StockSorting
    {
        public static readonly string[] Categories = { "Расходники", "Инструменты", "Кабели" };

        public static readonly (string Name, int Category)[] Items =
        {
            ("Картридж", 0),
            ("Мультиметр", 1),
            ("Патч-корд", 2),
            ("Пачка бумаги", 0),
            ("Удлинитель", 2)
        };

        public const int RequiredCorrect = 4;

        public static string Encode(IReadOnlyList<int> answers) => string.Join(",", answers);

        public static int CountCorrect(string encodedAnswers)
        {
            string[] parts = (encodedAnswers ?? string.Empty).Split(',');
            int correct = 0;
            for (int index = 0; index < Items.Length && index < parts.Length; index++)
            {
                if (int.TryParse(parts[index], out int category) && category == Items[index].Category)
                {
                    correct++;
                }
            }

            return correct;
        }
    }

    public enum KafedraUnit
    {
        Infantry,
        Ranged,
        Armor
    }

    /// <summary>
    /// "КафедраCraft": three unit types, two lanes, three decisions.
    /// The outcome depends only on the composition the player chooses.
    /// </summary>
    public sealed class KafedraMatch
    {
        public const int Decisions = 3;
        public const int MainLanePower = 3;
        public const int SideLanePower = 2;

        public KafedraMatch(int mainLane, KafedraUnit mainEnemy, KafedraUnit sideEnemy)
        {
            MainLane = mainLane;
            MainEnemy = mainEnemy;
            SideEnemy = sideEnemy;
        }

        public int MainLane { get; }
        public KafedraUnit MainEnemy { get; }
        public KafedraUnit SideEnemy { get; }

        public static readonly string[] LaneNames = { "Левый фланг", "Правый фланг" };

        public static string UnitName(KafedraUnit unit) => unit switch
        {
            KafedraUnit.Infantry => "Аспиранты",
            KafedraUnit.Ranged => "Лаборанты",
            KafedraUnit.Armor => "Методисты",
            _ => unit.ToString()
        };

        public static KafedraMatch Create(int seed, int matchIndex)
        {
            Random random = new(unchecked(seed * 31 + matchIndex * 7919));
            return new KafedraMatch(random.Next(2), (KafedraUnit)random.Next(3), (KafedraUnit)random.Next(3));
        }

        public KafedraUnit EnemyIn(int lane) => lane == MainLane ? MainEnemy : SideEnemy;

        public int RequiredPower(int lane) => lane == MainLane ? MainLanePower : SideLanePower;

        /// <summary>Counter = 2, mirror = 1, countered = 0.</summary>
        public static int Power(KafedraUnit mine, KafedraUnit enemy)
        {
            if (mine == enemy)
            {
                return 1;
            }

            return (int)mine == ((int)enemy + 2) % 3 ? 2 : 0;
        }

        public static string Encode(IReadOnlyList<(KafedraUnit Unit, int Lane)> decisions)
        {
            List<string> parts = new();
            foreach ((KafedraUnit unit, int lane) in decisions)
            {
                parts.Add($"{(int)unit}:{lane}");
            }

            return string.Join(",", parts);
        }

        public bool TryResolve(string encodedDecisions, out int[] lanePower, out bool won)
        {
            lanePower = new int[2];
            won = false;
            string[] parts = (encodedDecisions ?? string.Empty).Split(',');
            if (parts.Length != Decisions)
            {
                return false;
            }

            foreach (string part in parts)
            {
                string[] pair = part.Split(':');
                if (pair.Length != 2 || !int.TryParse(pair[0], out int unit) || !int.TryParse(pair[1], out int lane) ||
                    unit < 0 || unit > 2 || lane < 0 || lane > 1)
                {
                    return false;
                }

                lanePower[lane] += Power((KafedraUnit)unit, EnemyIn(lane));
            }

            won = lanePower[0] >= RequiredPower(0) && lanePower[1] >= RequiredPower(1);
            return true;
        }
    }
}
