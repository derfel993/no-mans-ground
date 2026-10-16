using System;
using System.Collections.Generic;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEngine;

namespace NoMansGround.Tests
{
    public sealed class SoldierDataTests
    {
        [Test]
        public void GeneratedSoldiersHaveUniqueItalianIdentitiesAndDeterministicCharacteristics()
        {
            UnityEngine.Random.State state = UnityEngine.Random.state;
            IReadOnlyList<SoldierData> first = SoldierGenerator.Generate(64, 1917);
            IReadOnlyList<SoldierData> second = SoldierGenerator.Generate(64, 1917);
            var names = new HashSet<string>();
            for (int i = 0; i < first.Count; i++)
            {
                Assert.IsTrue(names.Add(first[i].FullName));
                Assert.AreEqual(i, first[i].Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(first[i].FirstName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(first[i].LastName));
                Assert.AreEqual(first[i].FullName, second[i].FullName);
                Assert.AreEqual(100f, first[i].Needs.Hunger);
                Assert.AreEqual(100f, first[i].Needs.Thirst);
                Assert.AreEqual(100f, first[i].Needs.PhysicalNeeds);
                Assert.AreEqual(first[i].Needs.Hunger, second[i].Needs.Hunger);
                Assert.AreEqual(first[i].Needs.Thirst, second[i].Needs.Thirst);
                Assert.AreEqual(first[i].Needs.PhysicalNeeds, second[i].Needs.PhysicalNeeds);
                Assert.AreEqual(first[i].Needs.ThermalState, second[i].Needs.ThermalState);
                Assert.AreEqual(first[i].Needs.Stress, second[i].Needs.Stress);
                Assert.AreEqual(first[i].Needs.Fear, second[i].Needs.Fear);
                Assert.AreEqual(first[i].Needs.Fatigue, second[i].Needs.Fatigue);
                foreach (SoldierSkill skill in Enum.GetValues(typeof(SoldierSkill)))
                {
                    Assert.That(first[i].GetSkill(skill).Level, Is.InRange(1, 10));
                    Assert.AreEqual(first[i].GetSkill(skill).Level, second[i].GetSkill(skill).Level);
                    Assert.Zero(first[i].GetSkill(skill).Experience);
                }
            }
            Assert.AreEqual(state, UnityEngine.Random.state);
            Assert.AreNotEqual(first[0].FullName, SoldierGenerator.Generate(64, 1918)[0].FullName);
        }

        [TestCase(0)]
        [TestCase(65)]
        public void InvalidSoldierCountIsRejected(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SoldierGenerator.Generate(count, 1));
        }

        [Test]
        public void NeedsAcceptBoundaryValuesAndRejectInvalidStates()
        {
            var cold = new SoldierNeeds(0, 100, 0, -100, 100, 0, 100);
            var hot = new SoldierNeeds(100, 0, 100, 100, 0, 100, 0);
            Assert.AreEqual(-100f, cold.ThermalState);
            Assert.AreEqual(100f, hot.ThermalState);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(-1, 0, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(0, 101, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(0, 0, float.NaN, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(0, 0, 0, -101, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(0, 0, 0, 0, float.PositiveInfinity, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(0, 0, 0, 0, 0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(0, 0, 0, 0, 0, 0, 101));
        }

        [Test]
        public void ExperienceCrossesExactThresholdsAndPreservesRemainder()
        {
            var skill = new SkillProgress();
            int notifications = 0;
            skill.Changed += () => notifications++;
            skill.AddExperience(99);
            Assert.AreEqual(1, skill.Level);
            Assert.AreEqual(99, skill.Experience);
            skill.AddExperience(1);
            Assert.AreEqual(2, skill.Level);
            Assert.Zero(skill.Experience);
            Assert.AreEqual(200, skill.ExperienceToNextLevel);
            skill.AddExperience(550);
            Assert.AreEqual(4, skill.Level);
            Assert.AreEqual(50, skill.Experience);
            Assert.AreEqual(20, skill.Value);
            Assert.AreEqual(3, notifications);
            skill.AddExperience(0);
            Assert.AreEqual(3, notifications);
        }

        [Test]
        public void ExperienceCapsAtLevelTwentyWithoutIntegerOverflow()
        {
            var skill = new SkillProgress(19);
            skill.AddExperience(1900);
            Assert.IsTrue(skill.IsMaximumLevel);
            Assert.AreEqual(100, skill.Value);
            Assert.Zero(skill.Experience);
            Assert.Zero(skill.ExperienceToNextLevel);
            skill.AddExperience(int.MaxValue);
            Assert.AreEqual(20, skill.Level);
            var novice = new SkillProgress();
            novice.AddExperience(int.MaxValue);
            Assert.AreEqual(20, novice.Level);
            Assert.Zero(novice.Experience);
        }

        [Test]
        public void SkillInputValidationAndIndependentProgressArePreserved()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillProgress(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillProgress(21));
            var soldiers = SoldierGenerator.Generate(2, 1917);
            SkillProgress aim = soldiers[0].GetSkill(SoldierSkill.Aim);
            int courage = soldiers[0].GetSkill(SoldierSkill.Courage).Level;
            int otherAim = soldiers[1].GetSkill(SoldierSkill.Aim).Level;
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.AddExperience(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => soldiers[0].GetSkill((SoldierSkill)99));
            aim.AddExperience(aim.ExperienceToNextLevel);
            Assert.AreEqual(courage, soldiers[0].GetSkill(SoldierSkill.Courage).Level);
            Assert.AreEqual(otherAim, soldiers[1].GetSkill(SoldierSkill.Aim).Level);
            Assert.Throws<ArgumentException>(() =>
                new SoldierData(0, "", "Rossi", soldiers[0].Needs, new[] { 1, 1, 1, 1, 1 }));
        }
    }
}
