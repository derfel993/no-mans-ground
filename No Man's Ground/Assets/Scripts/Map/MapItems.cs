using System;
using System.Collections.Generic;

namespace NoMansGround.Map
{
    public enum ItemCategory
    {
        Tool,
        Weapon,
        Food,
        Resource,
        Equipment
    }

    public enum ItemType
    {
        Shovel,
        Axe,
        Pickaxe,
        Rations,
        Wood,
        BuildingStone,
        FullCanteen,
        EmptyCanteen,
        ConstructionHammer
    }

    public enum ItemAction
    {
        DigTrench,
        CutTree,
        BreakRock,
        Build
    }

    public readonly struct ItemDefinition
    {
        public ItemType Type { get; }
        public string DisplayName { get; }
        public ItemCategory Category { get; }
        public ItemAction? Action { get; }
        public int StackSize { get; }

        internal ItemDefinition(ItemType type, string displayName, ItemCategory category,
            ItemAction? action, int stackSize)
        {
            Type = type;
            DisplayName = displayName;
            Category = category;
            Action = action;
            StackSize = stackSize;
        }
    }

    public static class ItemCatalog
    {
        public static ItemDefinition Get(ItemType type)
        {
            switch (type)
            {
                case ItemType.Shovel: return new ItemDefinition(type, "Pala", ItemCategory.Tool, ItemAction.DigTrench, 1);
                case ItemType.Axe: return new ItemDefinition(type, "Accetta", ItemCategory.Tool, ItemAction.CutTree, 1);
                case ItemType.Pickaxe: return new ItemDefinition(type, "Piccone", ItemCategory.Tool, ItemAction.BreakRock, 1);
                case ItemType.Rations: return new ItemDefinition(type, "Razioni", ItemCategory.Food, null, 250);
                case ItemType.Wood: return new ItemDefinition(type, "Legna", ItemCategory.Resource, null, 250);
                case ItemType.BuildingStone:
                    return new ItemDefinition(type, "Pietra da costruzione", ItemCategory.Resource, null, 250);
                case ItemType.FullCanteen:
                    return new ItemDefinition(type, "Borraccia piena (acqua)", ItemCategory.Equipment, null, 1);
                case ItemType.EmptyCanteen:
                    return new ItemDefinition(type, "Borraccia vuota", ItemCategory.Equipment, null, 1);
                case ItemType.ConstructionHammer:
                    return new ItemDefinition(type, "Martello da Costruzione", ItemCategory.Tool, ItemAction.Build, 1);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }

    public readonly struct ItemStack
    {
        public ItemType Type { get; }
        public int Count { get; }

        public ItemStack(ItemType type, int count)
        {
            if (!Enum.IsDefined(typeof(ItemType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            Type = type;
            Count = count;
        }
    }

    public sealed class ItemStockpile
    {
        private readonly int[] counts = new int[Enum.GetValues(typeof(ItemType)).Length];
        public event Action Changed;

        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (int count in counts)
                    total = checked(total + count);
                return total;
            }
        }

        public int Count(ItemType type)
        {
            return counts[IndexOf(type)];
        }

        public IReadOnlyList<ItemStack> GetStacks(ItemType type)
        {
            int remaining = Count(type);
            int stackSize = ItemCatalog.Get(type).StackSize;
            var stacks = new List<ItemStack>();
            while (remaining > 0)
            {
                int count = Math.Min(remaining, stackSize);
                stacks.Add(new ItemStack(type, count));
                remaining -= count;
            }
            return stacks.AsReadOnly();
        }

        public void Add(ItemType type, int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            int index = IndexOf(type);
            counts[index] = checked(counts[index] + count);
            if (count > 0)
                Changed?.Invoke();
        }

        public bool TryTake(ItemType type, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            int index = IndexOf(type);
            if (counts[index] < count)
                return false;
            counts[index] -= count;
            Changed?.Invoke();
            return true;
        }

        private static int IndexOf(ItemType type)
        {
            int index = (int)type;
            if (index < 0 || index >= Enum.GetValues(typeof(ItemType)).Length)
                throw new ArgumentOutOfRangeException(nameof(type));
            return index;
        }
    }
}
