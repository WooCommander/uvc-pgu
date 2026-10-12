using System;
using System.Collections.Generic;

namespace Uvc.Core
{
    public sealed class ItemDefinition
    {
        public ItemDefinition(string id, string name, int stackLimit, bool questItem = false, bool tool = false)
        {
            Id = id;
            Name = name;
            StackLimit = stackLimit;
            QuestItem = questItem;
            Tool = tool;
        }

        public string Id { get; }
        public string Name { get; }
        public int StackLimit { get; }
        public bool QuestItem { get; }
        public bool Tool { get; }
    }

    public static class ItemCatalog
    {
        private static readonly Dictionary<string, ItemDefinition> Items = new();

        static ItemCatalog()
        {
            Register(new ItemDefinition(ItemIds.Cartridge, "Картридж", 3));
            Register(new ItemDefinition(ItemIds.Paper, "Бумага", 5));
            Register(new ItemDefinition(ItemIds.Screwdriver, "Отвёртка", 1, tool: true));
            Register(new ItemDefinition(ItemIds.Opener, "Открывашка", 1, tool: true));
            Register(new ItemDefinition(ItemIds.Capelin, "Мойва", 3));
            Register(new ItemDefinition(ItemIds.Beer, "Пиво", 6));
            Register(new ItemDefinition(ItemIds.Lemonade, "Лимонад", 6));
            Register(new ItemDefinition(ItemIds.ProjectorAdapter, "Переходник проектора", 1, questItem: true));
            Register(new ItemDefinition(ItemIds.OldJournal, "Старый журнал", 1, questItem: true));
            Register(new ItemDefinition(ItemIds.LostPass, "Забытый пропуск", 1, questItem: true));
            Register(new ItemDefinition(ItemIds.RitualMug, "Старая кружка", 1, questItem: true));
            Register(new ItemDefinition(ItemIds.ArchiveSheet, "Архивная ведомость", 1, questItem: true));
            Register(new ItemDefinition(ItemIds.BragarBag, "Сумка Брагаря", 1, questItem: true));
        }

        public static IEnumerable<ItemDefinition> All => Items.Values;

        public static ItemDefinition Get(string itemId)
        {
            return Items.TryGetValue(itemId, out ItemDefinition item)
                ? item
                : throw new ArgumentException($"Unknown item id '{itemId}'", nameof(itemId));
        }

        public static string NameOf(string itemId) => Get(itemId).Name;

        private static void Register(ItemDefinition item) => Items.Add(item.Id, item);
    }

    [Serializable]
    public sealed class ItemStack
    {
        public string itemId;
        public int count;
    }

    /// <summary>Eight ordinary slots plus a separate, unlimited storage for quest items.</summary>
    [Serializable]
    public sealed class Inventory
    {
        public const int SlotCount = 8;

        public List<ItemStack> slots = new();
        public List<ItemStack> questItems = new();

        public IEnumerable<ItemStack> AllStacks
        {
            get
            {
                foreach (ItemStack stack in slots)
                {
                    yield return stack;
                }

                foreach (ItemStack stack in questItems)
                {
                    yield return stack;
                }
            }
        }

        public int Count(string itemId)
        {
            int total = 0;
            foreach (ItemStack stack in ListFor(itemId))
            {
                if (stack.itemId == itemId)
                {
                    total += stack.count;
                }
            }

            return total;
        }

        public bool Has(string itemId, int count = 1) => Count(itemId) >= count;

        public bool CanAdd(string itemId, int count = 1)
        {
            ItemDefinition item = ItemCatalog.Get(itemId);
            if (item.QuestItem)
            {
                return true;
            }

            int free = 0;
            foreach (ItemStack stack in slots)
            {
                if (stack.itemId == itemId)
                {
                    free += item.StackLimit - stack.count;
                }
            }

            free += (SlotCount - slots.Count) * item.StackLimit;
            return free >= count;
        }

        /// <summary>Adds all items or nothing: a full inventory never loses an item.</summary>
        public bool TryAdd(string itemId, int count = 1)
        {
            if (count <= 0 || !CanAdd(itemId, count))
            {
                return false;
            }

            ItemDefinition item = ItemCatalog.Get(itemId);
            List<ItemStack> list = ListFor(itemId);
            int remaining = count;
            foreach (ItemStack stack in list)
            {
                if (stack.itemId != itemId || remaining == 0)
                {
                    continue;
                }

                int room = item.QuestItem ? remaining : Math.Min(remaining, item.StackLimit - stack.count);
                stack.count += room;
                remaining -= room;
            }

            while (remaining > 0)
            {
                int amount = item.QuestItem ? remaining : Math.Min(remaining, item.StackLimit);
                list.Add(new ItemStack { itemId = itemId, count = amount });
                remaining -= amount;
            }

            return true;
        }

        /// <summary>Removes all requested items or nothing.</summary>
        public bool TryRemove(string itemId, int count = 1)
        {
            if (count <= 0 || !Has(itemId, count))
            {
                return false;
            }

            List<ItemStack> list = ListFor(itemId);
            int remaining = count;
            for (int index = list.Count - 1; index >= 0 && remaining > 0; index--)
            {
                ItemStack stack = list[index];
                if (stack.itemId != itemId)
                {
                    continue;
                }

                int taken = Math.Min(remaining, stack.count);
                stack.count -= taken;
                remaining -= taken;
                if (stack.count == 0)
                {
                    list.RemoveAt(index);
                }
            }

            return true;
        }

        private List<ItemStack> ListFor(string itemId) => ItemCatalog.Get(itemId).QuestItem ? questItems : slots;
    }
}
