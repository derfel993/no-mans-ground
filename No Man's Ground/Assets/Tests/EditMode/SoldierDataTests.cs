using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoMansGround.Tests
{
    public sealed class SoldierDataTests
    {
        [TestCase(0f, 100f, 600f, 98.75f)]
        [TestCase(100f, 0f, 600f, 92.5f)]
        [TestCase(0f, 0f, 600f, 91.25f)]
        [TestCase(100f, 100f, 600f, 100f)]
        [TestCase(10f, 100f, 240f, 99.75f)]
        [TestCase(100f, 10f, 240f, 98.5f)]
        [TestCase(10f, 10f, 240f, 98.25f)]
        [TestCase(10f, 10f, 120f, 100f)]
        public void EmptyReservesDamageHealthOnlyAfterTheyRunOut(
            float hunger, float thirst, float seconds, float expectedHealth)
        {
            var soldier = new SoldierData(0, "Mario", "Rossi",
                new SoldierNeeds(hunger, thirst, 100, 0, 0, 0, 0), new[] { 1, 1, 1, 1, 1 });
            Assert.IsTrue(soldier.Inventory.TryTake(ItemType.FullCanteen, 1));
            soldier.AdvanceTime(seconds);
            Assert.AreEqual(expectedHealth, soldier.Health, 0.0001f);
        }

        [TestCase(true, false, 95f)]
        [TestCase(false, true, 70f)]
        [TestCase(true, true, 65f)]
        public void DailyStarvationAndDehydrationRatesUseTheGameClockDay(
            bool starving, bool dehydrated, float expectedHealth)
        {
            var soldier = new SoldierData(0, "Mario", "Rossi",
                new SoldierNeeds(starving ? 0 : 100, dehydrated ? 0 : 100, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            soldier.Inventory.TryTake(ItemType.FullCanteen, 1);
            for (int step = 0; step < 24; step++)
            {
                if (!starving) soldier.Needs.EatRation();
                if (!dehydrated) soldier.Needs.DrinkWater();
                soldier.AdvanceTime((float)(GameClock.SecondsPerDay / 24d));
            }
            Assert.AreEqual(expectedHealth, soldier.Health, 0.0001f);
        }

        [Test]
        public void EatingAndDrinkingStopDamageWithoutHealingAndZeroTimeDoesNotDamage()
        {
            var soldier = new SoldierData(0, "Mario", "Rossi",
                new SoldierNeeds(0, 0, 100, 0, 0, 0, 0), new[] { 1, 1, 1, 1, 1 });
            soldier.Inventory.TryTake(ItemType.FullCanteen, 1);
            soldier.AdvanceTime(600f);
            Assert.AreEqual(91.25f, soldier.Health);
            soldier.AdvanceTime(0f);
            Assert.AreEqual(91.25f, soldier.Health);
            soldier.Needs.EatRation();
            soldier.Needs.DrinkWater();
            soldier.AdvanceTime(100f);
            Assert.AreEqual(91.25f, soldier.Health);
        }

        [Test]
        public void CanteenPreventsPrematureDehydrationAndLargeUpdatesMatchSmallUpdates()
        {
            var single = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(100, 100, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            var stepped = new SoldierData(1, "Luigi", "Verdi", new SoldierNeeds(100, 100, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            single.AdvanceTime(2400f);
            for (int step = 0; step < 2400; step++)
                stepped.AdvanceTime(1f);
            Assert.AreEqual(94.5f, single.Health, 0.0001f);
            Assert.AreEqual(single.Health, stepped.Health, 0.0001f);
            Assert.AreEqual(1, single.Inventory.Count(ItemType.EmptyCanteen));
        }

        [Test]
        public void FrameSizedDamagePreservesFractionalHealthAndDeadSoldiersStopUpdating()
        {
            var soldier = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(0, 0, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            soldier.Inventory.TryTake(ItemType.FullCanteen, 1);
            for (int frame = 0; frame < 144000; frame++)
                soldier.AdvanceTime(1f / 60f);
            Assert.AreEqual(65f, soldier.Health, 0.0001f);
            int deaths = 0;
            soldier.Died += () => deaths++;
            soldier.AdvanceTime((float)GameClock.SecondsPerDay * 2f);
            Assert.Zero(soldier.Health);
            Assert.AreEqual(1, deaths);
            float stress = soldier.Needs.Stress;
            soldier.AdvanceTime(100f);
            Assert.AreEqual(stress, soldier.Needs.Stress);
            Assert.AreEqual(1, deaths);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(10)]
        public void DamageFollowsTheSameScaledTimeAsTheClock(int simulationSpeed)
        {
            var clock = new GameClock();
            var soldier = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(0, 0, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            soldier.Inventory.TryTake(ItemType.FullCanteen, 1);
            for (int step = 0; step < 100; step++)
            {
                clock.AdvanceTime(simulationSpeed);
                soldier.AdvanceTime(simulationSpeed);
            }
            Assert.AreEqual(100d - 35d * clock.ElapsedSeconds / GameClock.SecondsPerDay,
                soldier.Health, 0.0001d);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidSoldierTimeDoesNotDamageHealth(float seconds)
        {
            SoldierData soldier = SoldierGenerator.Generate(1, 1917)[0];
            Assert.Throws<ArgumentOutOfRangeException>(() => soldier.AdvanceTime(seconds));
            Assert.AreEqual(100f, soldier.Health);
        }

        [Test]
        public void HealthStartsAtOneHundredAndLethalDamageKillsExactlyOnce()
        {
            SoldierData soldier = SoldierGenerator.Generate(1, 1917)[0];
            int healthChanges = 0;
            int deaths = 0;
            soldier.HealthChanged += () => healthChanges++;
            soldier.Died += () => deaths++;
            Assert.AreEqual(100f, soldier.Health);
            Assert.IsTrue(soldier.IsAlive);
            soldier.TakeDamage(0f);
            Assert.Zero(healthChanges);
            soldier.TakeDamage(25f);
            Assert.AreEqual(75f, soldier.Health);
            Assert.IsTrue(soldier.IsAlive);
            soldier.TakeDamage(100f);
            Assert.Zero(soldier.Health);
            Assert.IsFalse(soldier.IsAlive);
            Assert.AreEqual(2, healthChanges);
            Assert.AreEqual(1, deaths);
            soldier.TakeDamage(100f);
            Assert.AreEqual(2, healthChanges);
            Assert.AreEqual(1, deaths);
            Assert.IsFalse(soldier.TryAddToInventory(ItemType.Wood));
            Assert.IsFalse(soldier.TryDrinkWater());
            float hunger = soldier.Needs.Hunger;
            soldier.ReceiveRation();
            Assert.AreEqual(hunger, soldier.Needs.Hunger);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDamageDoesNotChangeHealth(float amount)
        {
            SoldierData soldier = SoldierGenerator.Generate(1, 1917)[0];
            Assert.Throws<ArgumentOutOfRangeException>(() => soldier.TakeDamage(amount));
            Assert.AreEqual(100f, soldier.Health);
            Assert.IsTrue(soldier.IsAlive);
        }

        [Test]
        public void DeathStopsSoldierMovementWorkSelectionAndNewOrders()
        {
            var owner = new GameObject("Health test", typeof(SpriteRenderer), typeof(SoldierView));
            var grid = new GameObject("Grid", typeof(Grid));
            var ground = new GameObject("Ground", typeof(UnityEngine.Tilemaps.Tilemap), typeof(MapTilemapView));
            ground.transform.SetParent(grid.transform, false);
            var texture = new Texture2D(32, 32);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f);
            try
            {
                SoldierData data = SoldierGenerator.Generate(1, 1917)[0];
                var soldier = owner.GetComponent<SoldierView>();
                var view = ground.GetComponent<MapTilemapView>();
                soldier.Initialize(data, Vector2Int.zero, sprite);
                soldier.SetSelected(true);
                soldier.RequestDuty(SoldierDuty.Guard);
                soldier.BeginMove(view, new[] { Vector2Int.zero, Vector2Int.right });
                Assert.IsTrue(soldier.IsMoving);
                int deaths = 0;
                soldier.Died += dead => { Assert.AreSame(soldier, dead); deaths++; };
                Vector3 position = owner.transform.position;
                data.TakeDamage(100f);
                Assert.AreEqual(1, deaths);
                Assert.IsFalse(owner.activeSelf);
                Assert.IsFalse(soldier.IsMoving);
                Assert.IsFalse(soldier.IsSelected);
                Assert.IsFalse(soldier.HasWorkAssignment);
                Assert.IsFalse(soldier.IsAvailable);
                Assert.IsFalse(soldier.CanTakeGuardMove);
                Assert.IsFalse(soldier.CanTakeEngineeringWork);
                Assert.IsFalse(soldier.CanAutoTransport);
                Assert.IsFalse(soldier.RequestDuty(SoldierDuty.Work));
                soldier.RequestTask(SoldierTask.Cook);
                Assert.IsNull(soldier.RequestedTask);
                Assert.Throws<InvalidOperationException>(() =>
                    soldier.BeginMove(view, new[] { Vector2Int.zero, Vector2Int.right }));
                soldier.AdvanceMovement(100f);
                soldier.AdvanceLook(100f);
                Assert.AreEqual(position, owner.transform.position);
                Assert.Zero(data.Needs.Fatigue);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(grid);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

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
                Assert.Zero(first[i].Needs.Stress);
                Assert.Zero(first[i].Needs.Fear);
                Assert.Zero(first[i].Needs.Fatigue);
                Assert.AreEqual(1, first[i].Inventory.Count(ItemType.FullCanteen));
                Assert.AreEqual(1, first[i].Inventory.Count(ItemType.CarcanoMod91));
                Assert.AreEqual(168, first[i].Inventory.Count(ItemType.CarcanoAmmunition));
                Assert.Zero(first[i].Inventory.Count(ItemType.EmptyCanteen));
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

        [TestCase(SoldierProfession.Soldier)]
        [TestCase(SoldierProfession.Engineer)]
        [TestCase(SoldierProfession.Cook)]
        public void EveryProfessionStartsWithFullCanteenCarcanoAndAmmunition(SoldierProfession profession)
        {
            var soldier = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(100, 100, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 }, profession);
            Assert.AreEqual(170, soldier.Inventory.TotalCount);
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.FullCanteen));
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.CarcanoMod91));
            Assert.AreEqual(168, soldier.Inventory.Count(ItemType.CarcanoAmmunition));
            Assert.AreEqual(ItemCategory.Equipment, ItemCatalog.Get(ItemType.FullCanteen).Category);
            Assert.AreEqual(ItemCategory.Weapon, ItemCatalog.Get(ItemType.CarcanoMod91).Category);
            Assert.AreEqual(3.8f, ItemCatalog.Get(ItemType.CarcanoMod91).WeightKg);
            Assert.AreEqual(1, ItemCatalog.Get(ItemType.EmptyCanteen).StackSize);
        }

        [Test]
        public void AimLevelControlsGuardHitProbabilityFromTwentyToEightyPercent()
        {
            Assert.AreEqual(0.2f, SoldierView.GetAimHitProbability(0));
            Assert.AreEqual(0.8f, SoldierView.GetAimHitProbability(SkillProgress.MaximumLevel));
            Assert.Throws<ArgumentOutOfRangeException>(() => SoldierView.GetAimHitProbability(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SoldierView.GetAimHitProbability(21));
        }

        [Test]
        public void ZeroLevelSkillNeedsOneHundredExperienceForItsFirstLevel()
        {
            var skill = new SkillProgress(0);
            Assert.Zero(skill.Value);
            Assert.AreEqual(100, skill.ExperienceToNextLevel);
            skill.AddExperience(99);
            Assert.Zero(skill.Level);
            Assert.AreEqual(99, skill.Experience);
            skill.AddExperience(1);
            Assert.AreEqual(1, skill.Level);
            Assert.Zero(skill.Experience);
        }

        [Test]
        public void GuardShotConsumesOneRoundAndAddsOneAimExperienceOnHitAndMiss()
        {
            var map = new MapData(10, 10);
            var target = new Vector2Int(5, 5);
            Assert.IsTrue(map.ToggleTargetCell(target));
            var data = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(100, 100, 100, 0, 0, 0, 0),
                new[] { 19, 1, 1, 1, 1 });
            var owner = new GameObject("Guard combat test", typeof(SpriteRenderer), typeof(SoldierView));
            var texture = new Texture2D(1, 1);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            try
            {
                SoldierView soldier = owner.GetComponent<SoldierView>();
                soldier.Initialize(data, new Vector2Int(1, 1), sprite, mapData: map);
                Assert.IsTrue(soldier.RequestDuty(SoldierDuty.Guard));
                MethodInfo fireAtTarget = typeof(SoldierView).GetMethod("TryFireAtTarget",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                object[] parameters = { target, 0.76f, false };
                LogAssert.Expect(LogType.Log, new Regex(@"Guard shot: Mario Rossi -> \(5, 5\) HIT; aim=19; chance=.*; roll=.*"));
                Assert.IsTrue((bool)fireAtTarget.Invoke(soldier, parameters));
                bool hit = (bool)parameters[2];
                Assert.IsTrue(hit);
                Assert.AreEqual(167, data.Inventory.Count(ItemType.CarcanoAmmunition));
                Assert.AreEqual(1, data.GetSkill(SoldierSkill.Aim).Experience);
                Assert.IsTrue(map.IsTargetCell(target));

                parameters[1] = 0.99f;
                LogAssert.Expect(LogType.Log, new Regex(@"Guard shot: Mario Rossi -> \(5, 5\) MISS; aim=19; chance=.*; roll=.*"));
                Assert.IsTrue((bool)fireAtTarget.Invoke(soldier, parameters));
                hit = (bool)parameters[2];
                Assert.IsFalse(hit);
                Assert.AreEqual(166, data.Inventory.Count(ItemType.CarcanoAmmunition));
                Assert.AreEqual(2, data.GetSkill(SoldierSkill.Aim).Experience);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GuardFiresAtVisibleTargetOnlyAfterLineOfSightIsClear()
        {
            var map = new MapData(10, 10);
            var target = new Vector2Int(5, 5);
            Assert.IsTrue(map.ToggleTargetCell(target));
            map[3, 3] = TerrainKind.Mountain;
            var data = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(100, 100, 100, 0, 0, 0, 0),
                new[] { 10, 1, 1, 1, 1 });
            var owner = new GameObject("Guard target visibility test", typeof(SpriteRenderer), typeof(SoldierView));
            var texture = new Texture2D(1, 1);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            try
            {
                SoldierView soldier = owner.GetComponent<SoldierView>();
                soldier.Initialize(data, new Vector2Int(1, 1), sprite, mapData: map);
                int shots = 0;
                soldier.ShotFired += (_, __) => shots++;
                soldier.AdvanceGuardCombat(1f);
                Assert.Zero(shots);

                Assert.IsTrue(soldier.RequestDuty(SoldierDuty.Guard));
                soldier.AdvanceGuardCombat(1f);
                Assert.Zero(shots);
                Assert.AreEqual(168, data.Inventory.Count(ItemType.CarcanoAmmunition));

                map[3, 3] = TerrainKind.Dirt;
                soldier.AdvanceGuardCombat(1f);
                Assert.AreEqual(1, shots);
                Assert.AreEqual(167, data.Inventory.Count(ItemType.CarcanoAmmunition));
                Assert.AreEqual(1, data.GetSkill(SoldierSkill.Aim).Experience);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void CanteenIsUsedOnlyBelowTwentyPercentAndCannotBeUsedTwice()
        {
            var soldier = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(100, 20, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            Assert.IsFalse(soldier.TryDrinkWater());
            Assert.AreEqual(20f, soldier.Needs.Thirst);
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.FullCanteen));
            soldier.Needs.AdvanceTime(0.1f);
            Assert.AreEqual(100f, soldier.Needs.Thirst);
            Assert.Zero(soldier.Inventory.Count(ItemType.FullCanteen));
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.EmptyCanteen));
            Assert.IsFalse(soldier.TryDrinkWater());
            soldier.Needs.AdvanceTime(soldier.Needs.ThirstDepletionSeconds);
            Assert.Zero(soldier.Needs.Thirst);
            Assert.IsFalse(soldier.TryDrinkWater());
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.EmptyCanteen));
            Assert.AreEqual(170, soldier.Inventory.TotalCount);
        }

        [Test]
        public void RationReplacesEmptyCanteenWithoutDuplicatingFullOnes()
        {
            var soldier = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(50, 19, 75, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            Assert.IsTrue(soldier.TryDrinkWater());
            soldier.ReceiveRation();
            Assert.AreEqual(100f, soldier.Needs.Hunger);
            Assert.AreEqual(100f, soldier.Needs.Thirst);
            Assert.AreEqual(75f, soldier.Needs.PhysicalNeeds);
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.FullCanteen));
            Assert.Zero(soldier.Inventory.Count(ItemType.EmptyCanteen));
            soldier.ReceiveRation();
            Assert.AreEqual(170, soldier.Inventory.TotalCount);
            soldier.Needs.AdvanceTime(soldier.Needs.ThirstDepletionSeconds);
            Assert.Zero(soldier.Inventory.Count(ItemType.FullCanteen));
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.EmptyCanteen));
            soldier.Needs.AdvanceTime(soldier.Needs.ThirstDepletionSeconds);
            Assert.Zero(soldier.Needs.Thirst);
            soldier.ReceiveRation();
            Assert.AreEqual(100f, soldier.Needs.Thirst);
            Assert.AreEqual(170, soldier.Inventory.TotalCount);
        }

        [Test]
        public void RefillingCanteenDoesNotRestoreThirstUntilSoldierDrinks()
        {
            var soldier = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(40, 60, 75, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 });
            Assert.IsTrue(soldier.Inventory.TryTake(ItemType.FullCanteen, 1));
            soldier.Inventory.Add(ItemType.EmptyCanteen, 1);
            soldier.ReceiveRation();
            Assert.AreEqual(100f, soldier.Needs.Hunger);
            Assert.AreEqual(60f, soldier.Needs.Thirst);
            Assert.AreEqual(1, soldier.Inventory.Count(ItemType.FullCanteen));
            Assert.Zero(soldier.Inventory.Count(ItemType.EmptyCanteen));
        }

        [TestCase(SoldierTask.Hauler, null, TrenchJobKind.CollectItem)]
        [TestCase(SoldierTask.Woodcutter, ItemType.Axe, TrenchJobKind.CutTree)]
        [TestCase(SoldierTask.Miner, ItemType.Pickaxe, TrenchJobKind.MineRock)]
        [TestCase(SoldierTask.Digger, ItemType.Shovel, TrenchJobKind.Dig)]
        [TestCase(SoldierTask.Builder, ItemType.ConstructionHammer, TrenchJobKind.BuildWall)]
        public void TasksDefineToolsAndOnlyAllowTheirOwnJobs(SoldierTask task, ItemType? tool, TrenchJobKind job)
        {
            Assert.AreEqual(tool, SoldierTasks.Tool(task));
            Assert.IsTrue(SoldierTasks.Allows(task, job));
            Assert.IsFalse(SoldierTasks.Allows(SoldierTask.Cook, job));
            Assert.IsFalse(string.IsNullOrEmpty(SoldierTasks.DisplayName(task)));
            foreach (TrenchJobKind kind in Enum.GetValues(typeof(TrenchJobKind)))
                if (kind != job && !(task == SoldierTask.Builder
                    && (kind == TrenchJobKind.BuildDoor || kind == TrenchJobKind.Ladder)))
                    Assert.IsFalse(SoldierTasks.Allows(task, kind));
        }

        [Test]
        public void EveryGeneratedSoldierStartsAsHaulerIncludingCookProfession()
        {
            foreach (SoldierData soldier in SoldierGenerator.Generate(8, 1917))
                Assert.AreEqual(SoldierTask.Hauler, soldier.Task);
            var cook = new SoldierData(0, "Mario", "Rossi", new SoldierNeeds(100, 100, 100, 0, 0, 0, 0),
                new[] { 1, 1, 1, 1, 1 }, SoldierProfession.Cook);
            Assert.AreEqual(SoldierTask.Hauler, cook.Task);
            Assert.IsNull(SoldierTasks.Tool(SoldierTask.Cook));
            Assert.IsTrue(SoldierTasks.Allows(SoldierTask.Builder, TrenchJobKind.BuildDoor));
            Assert.IsTrue(SoldierTasks.Allows(SoldierTask.Builder, TrenchJobKind.Ladder));
        }

        [Test]
        public void FatigueIncreasesOnlyOnActivityAndClampsAtOneHundred()
        {
            var state = new SoldierNeeds(100, 100, 100, 0, 0, 0, 0);
            int notifications = 0;
            state.Changed += () => notifications++;
            state.AddFatigue(0f);
            Assert.Zero(notifications);
            state.AddFatigue(1f);
            state.AddFatigue(5f);
            Assert.AreEqual(6f, state.Fatigue);
            Assert.AreEqual(2, notifications);
            Assert.Zero(state.Stress);
            Assert.Zero(state.Fear);
            state.AddFatigue(200f);
            Assert.AreEqual(100f, state.Fatigue);
            state.AddFatigue(5f);
            Assert.AreEqual(3, notifications);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.AddFatigue(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.AddFatigue(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.AddFatigue(float.PositiveInfinity));
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
        public void CarryCapacityScalesWithStrengthFromTwentyToThirtyFiveKilograms()
        {
            var needs = new SoldierNeeds(100, 100, 100, 0, 0, 0, 0);
            var soldier = new SoldierData(0, "Giovanni", "Rossi", needs, new[] { 1, 1, 1, 1, 1 });
            SkillProgress strength = soldier.GetSkill(SoldierSkill.Strength);
            Assert.AreEqual(20f, soldier.MaxCarryWeightKg);

            strength.AddExperience(strength.ExperienceToNextLevel);
            Assert.Greater(soldier.MaxCarryWeightKg, 20f);
            while (!strength.IsMaximumLevel)
                strength.AddExperience(strength.ExperienceToNextLevel);
            Assert.AreEqual(35f, soldier.MaxCarryWeightKg);
        }

        [Test]
        public void InventoryTracksItemWeightAndRejectsItemsAboveCarryCapacity()
        {
            var needs = new SoldierNeeds(100, 100, 100, 0, 0, 0, 0);
            var soldier = new SoldierData(0, "Giovanni", "Rossi", needs, new[] { 1, 1, 1, 1, 1 });
            Assert.AreEqual(1.5f, ItemCatalog.Get(ItemType.FullCanteen).WeightKg);
            Assert.AreEqual(9.5f, soldier.CarryWeightKg);
            Assert.IsTrue(soldier.CanCarry(ItemType.Wood, 10));
            Assert.IsFalse(soldier.CanCarry(ItemType.Wood, 11));
            Assert.IsTrue(soldier.TryAddToInventory(ItemType.Wood, 10));
            Assert.AreEqual(19.5f, soldier.CarryWeightKg);
            Assert.IsFalse(soldier.TryAddToInventory(ItemType.Wood, 1));
            Assert.AreEqual(10, soldier.Inventory.Count(ItemType.Wood));
        }

        [Test]
        public void SkillInputValidationAndIndependentProgressArePreserved()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillProgress(-1));
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
