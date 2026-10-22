using System;
using System.Collections.Generic;
using NoMansGround.Map;
using NUnit.Framework;

namespace NoMansGround.Tests
{
    public sealed class ArmyCatalogTests
    {
        [TestCase(ArmyType.German, "Impero Tedesco", "#4D5D53")]
        [TestCase(ArmyType.AustroHungarian, "Esercito Austro-Ungarico", "#75838F")]
        [TestCase(ArmyType.French, "Esercito Francese", "#7CA2B8")]
        [TestCase(ArmyType.UnitedKingdom, "Esercito del Regno Unito", "#625D43")]
        [TestCase(ArmyType.ImperialRussian, "Esercito Imperiale Russo", "#525838")]
        [TestCase(ArmyType.RoyalItalian, "Regio Esercito Italiano", "#5F6C37")]
        public void EachArmyHasItsOwnIdentityAndCustomContentLists(ArmyType type, string name, string colorHex)
        {
            ArmyDefinition army = ArmyCatalog.Get(type);
            Assert.AreEqual(type, army.Type);
            Assert.AreEqual(name, army.DisplayName);
            Assert.AreEqual(colorHex, army.ColorHex);
            Assert.IsNotNull(army.CustomItems);
            Assert.IsNotNull(army.CustomResearchIds);
            Assert.IsEmpty(army.CustomItems);
            Assert.IsEmpty(army.CustomResearchIds);
        }

        [Test]
        public void CatalogContainsExactlyTheSixArmiesWithoutDuplicates()
        {
            Assert.AreEqual(6, ArmyCatalog.All.Count);
            Assert.AreEqual(Enum.GetValues(typeof(ArmyType)).Length, ArmyCatalog.All.Count);
            var types = new HashSet<ArmyType>();
            var names = new HashSet<string>();
            foreach (ArmyDefinition army in ArmyCatalog.All)
            {
                Assert.IsTrue(types.Add(army.Type));
                Assert.IsTrue(names.Add(army.DisplayName));
                Assert.AreSame(army, ArmyCatalog.Get(army.Type));
            }
        }

        [TestCase(ArmyType.German, null)]
        [TestCase(ArmyType.AustroHungarian, null)]
        [TestCase(ArmyType.French, null)]
        [TestCase(ArmyType.UnitedKingdom, null)]
        [TestCase(ArmyType.ImperialRussian, null)]
        [TestCase(ArmyType.RoyalItalian, "Carcano Mod. 91")]
        public void DefaultWeaponIsAssignedOnlyToTheRoyalItalianArmy(ArmyType type, string weaponName)
        {
            Assert.AreEqual(weaponName, ArmyCatalog.Get(type).DefaultWeaponName);
        }

        [TestCase(-1)]
        [TestCase(6)]
        [TestCase(int.MaxValue)]
        public void UnknownArmyIsRejected(int type)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ArmyCatalog.Get((ArmyType)type));
        }

        [Test]
        public void CatalogAndCustomContentCannotBeModifiedThroughTheirPublicCollections()
        {
            var armies = (IList<ArmyDefinition>)ArmyCatalog.All;
            Assert.Throws<NotSupportedException>(() => armies.Clear());
            foreach (ArmyDefinition army in ArmyCatalog.All)
            {
                var items = (IList<ItemType>)army.CustomItems;
                var research = (IList<string>)army.CustomResearchIds;
                Assert.Throws<NotSupportedException>(() => items.Add(ItemType.Shovel));
                Assert.Throws<NotSupportedException>(() => research.Add("test-research"));
            }
        }
    }
}