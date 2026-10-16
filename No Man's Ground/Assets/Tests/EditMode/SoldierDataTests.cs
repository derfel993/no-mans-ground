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
                Assert.AreEqual(first[i].Needs.HungerDepletionSeconds, second[i].Needs.HungerDepletionSeconds);
                Assert.AreEqual(first[i].Needs.ThirstDepletionSeconds, second[i].Needs.ThirstDepletionSeconds);
                Assert.AreEqual(first[i].Needs.PhysicalNeedsDepletionSeconds, second[i].Needs.PhysicalNeedsDepletionSeconds);
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
        public void GeneratedConsumptionVariesIndependentlyWithinSeventeenToTwentyThreeMinutes()
        {
            var durations = new HashSet<float>();
            double sum = 0d;
            int count = 0;
            for (int seed = 0; seed < 32; seed++)
            {
                foreach (SoldierData soldier in SoldierGenerator.Generate(64, seed))
                {
                    var state = soldier.Needs;
                    foreach (float seconds in new[] { state.HungerDepletionSeconds,
                        state.ThirstDepletionSeconds, state.PhysicalNeedsDepletionSeconds })
                    {
                        Assert.That(seconds, Is.InRange(1020f, 1380f));
                        durations.Add(seconds);
                        sum += seconds;
                        count++;
                    }
                }
            }
            Assert.Greater(durations.Count, count * 0.95f);
            Assert.AreEqual(1200d, sum / count, 5d);
        }

        [Test]
        public void NeedsDeclineAtIndependentRatesAndClampAtZeroWhileHungerRaisesStress()
        {
            var state = new SoldierNeeds(100, 100, 100, -12, 25, 30, 40, 1020, 1200, 1380);
            Assert.AreEqual(100f / 1020f, state.HungerConsumptionPerSecond);
            Assert.AreEqual(100f / 1200f, state.ThirstConsumptionPerSecond);
            Assert.AreEqual(100f / 1380f, state.PhysicalNeedsConsumptionPerSecond);
            state.AdvanceTime(510f);
            Assert.AreEqual(50f, state.Hunger, 0.00001f);
            Assert.AreEqual(57.5f, state.Thirst, 0.00001f);
            Assert.AreEqual(100f - 100f * 510f / 1380f, state.PhysicalNeeds, 0.00001f);
            state.AdvanceTime(510f);
            Assert.Zero(state.Hunger);
            Assert.Greater(state.Thirst, 0f);
            Assert.Greater(state.PhysicalNeeds, 0f);
            state.AdvanceTime(180f);
            Assert.Zero(state.Thirst);
            Assert.Greater(state.PhysicalNeeds, 0f);
            state.AdvanceTime(180f);
            Assert.Zero(state.PhysicalNeeds);
            state.AdvanceTime(float.MaxValue);
            Assert.Zero(state.Hunger);
            Assert.Zero(state.Thirst);
            Assert.Zero(state.PhysicalNeeds);
            Assert.AreEqual(-12, state.ThermalState);
            Assert.AreEqual(100, state.Stress);
            Assert.AreEqual(30, state.Fear);
            Assert.AreEqual(40, state.Fatigue);
        }

        [Test]
        public void NeedDecayIsFrameRateIndependentAndZeroTimeDoesNotNotify()
        {
            var single = new SoldierNeeds(100, 100, 100, 0, 0, 0, 0);
            var stepped = new SoldierNeeds(100, 100, 100, 0, 0, 0, 0);
            int notifications = 0;
            stepped.Changed += () => notifications++;
            stepped.AdvanceTime(0f);
            Assert.Zero(notifications);
            single.AdvanceTime(600f);
            for (int i = 0; i < 36000; i++)
                stepped.AdvanceTime(1f / 60f);
            Assert.AreEqual(50f, single.Hunger);
            Assert.AreEqual(single.Hunger, stepped.Hunger, 0.00001f);
            Assert.AreEqual(single.Thirst, stepped.Thirst, 0.00001f);
            Assert.AreEqual(single.PhysicalNeeds, stepped.PhysicalNeeds, 0.00001f);
            Assert.AreEqual(36000, notifications);
            stepped.AdvanceTime(601f);
            stepped.AdvanceTime(10f);
            Assert.AreEqual(36002, notifications);
        }

        [Test]
        public void HungerThresholdIsStrictAndStressOnlyCountsTimeBelowThirtyFive()
        {
            var needs = new SoldierNeeds(36, 100, 100, 0, 10, 0, 0);
            needs.AdvanceTime(12f);
            Assert.AreEqual(35f, needs.Hunger);
            Assert.IsFalse(needs.WaitingForRations);
            Assert.AreEqual(10f, needs.Stress);
            needs.AdvanceTime(60f);
            Assert.IsTrue(needs.WaitingForRations);
            Assert.AreEqual(15f, needs.Stress, 0.00001f);
            var crossing = new SoldierNeeds(40, 100, 100, 0, 0, 0, 0);
            crossing.AdvanceTime(90f);
            Assert.AreEqual(2.5f, crossing.Stress, 0.00001f);
            crossing.AdvanceTime(10000f);
            Assert.AreEqual(100f, crossing.Stress);
            Assert.IsTrue(crossing.WaitingForRations);
        }

        [Test]
        public void NeedTimeAndDepletionDurationsRejectInvalidInputsWithoutChangingState()
        {
            var state = new SoldierNeeds(100, 100, 100, 0, 0, 0, 0);
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => state.AdvanceTime(invalid));
            foreach (float invalid in new[] { 0f, 1019f, 1381f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(100, 100, 100, 0, 0, 0, 0,
                    hungerDepletionSeconds: invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(100, 100, 100, 0, 0, 0, 0,
                    thirstDepletionSeconds: invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierNeeds(100, 100, 100, 0, 0, 0, 0,
                    physicalNeedsDepletionSeconds: invalid));
            }
            Assert.AreEqual(100f, state.Hunger);
            Assert.AreEqual(100f, state.Thirst);
            Assert.AreEqual(100f, state.PhysicalNeeds);
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
