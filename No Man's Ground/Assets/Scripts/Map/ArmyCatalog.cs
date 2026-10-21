using System;
using System.Collections.Generic;

namespace NoMansGround.Map
{
    public enum ArmyType
    {
        German = 0,
        AustroHungarian = 1,
        French = 2,
        UnitedKingdom = 3,
        ImperialRussian = 4,
        RoyalItalian = 5
    }

    public sealed class ArmyDefinition
    {
        public ArmyType Type { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ItemType> CustomItems { get; }
        public IReadOnlyList<string> CustomResearchIds { get; }

        internal ArmyDefinition(ArmyType type, string displayName, ItemType[] customItems,
            string[] customResearchIds)
        {
            Type = type;
            DisplayName = displayName;
            CustomItems = Array.AsReadOnly((ItemType[])customItems.Clone());
            CustomResearchIds = Array.AsReadOnly((string[])customResearchIds.Clone());
        }
    }

    public static class ArmyCatalog
    {
        public static IReadOnlyList<ArmyDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new ArmyDefinition(ArmyType.German, "Esercito Tedesco",
                Array.Empty<ItemType>(), Array.Empty<string>()),
            new ArmyDefinition(ArmyType.AustroHungarian, "Esercito Austro Ungarico",
                Array.Empty<ItemType>(), Array.Empty<string>()),
            new ArmyDefinition(ArmyType.French, "Esercito Francese",
                Array.Empty<ItemType>(), Array.Empty<string>()),
            new ArmyDefinition(ArmyType.UnitedKingdom, "Esercito del Regno Unito",
                Array.Empty<ItemType>(), Array.Empty<string>()),
            new ArmyDefinition(ArmyType.ImperialRussian, "Esercito Imperiale Russo",
                Array.Empty<ItemType>(), Array.Empty<string>()),
            new ArmyDefinition(ArmyType.RoyalItalian, "Regio Esercito Italiano",
                Array.Empty<ItemType>(), Array.Empty<string>())
        });

        public static ArmyDefinition Get(ArmyType type)
        {
            foreach (ArmyDefinition army in All)
                if (army.Type == type)
                    return army;
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}