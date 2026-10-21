using System;
using System.Collections.Generic;
using NoMansGround.Map;
using NUnit.Framework;

namespace NoMansGround.Tests
{
    public sealed class ArmyCatalogTests
    {
        [TestCase(ArmyType.German, "Esercito Tedesco")]
        [TestCase(ArmyType.AustroHungarian, "Esercito Austro Ungarico")]
        [TestCase(ArmyType.French, "Esercito Francese")]
        [TestCase(ArmyType.UnitedKingdom, "Esercito del Regno Unito")]
        [TestCase(ArmyType.ImperialRussian, "Esercito Imperiale Russo")]
        [TestCase(ArmyType.RoyalItalian, "Regio Esercito Italiano")]
        public void EachArmyHasItsOwnIdentityAndCustomContentLists(ArmyType type, string name)
        {
            ArmyDefinition army = ArmyCatalog.Get(type);
            Assert.AreEqual(type, army.Type);
            Assert.AreEqual(name, army.DisplayName);
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