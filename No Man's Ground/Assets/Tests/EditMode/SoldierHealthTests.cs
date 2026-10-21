using System;
using System.Reflection;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMansGround.Tests
{
    public sealed class SoldierHealthTests
    {
        private GameObject mapObject;
        private GameObject cameraObject;
        private GameObject rosterObject;
        private GameObject panelObject;
        private MapPrototype map;
        private MapTilemapView view;
        private Camera camera;
        private SoldierRoster roster;
        private TrenchWorkOrders orders;
        private KitchenSystem kitchen;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            mapObject = new GameObject("Map", typeof(Grid), typeof(MapPrototype));
            map = mapObject.GetComponent<MapPrototype>();
            map.Generation.width = 40;
            map.Generation.height = 32;
            var ground = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(MapTilemapView));
            ground.transform.SetParent(mapObject.transform, false);
            view = ground.GetComponent<MapTilemapView>();
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Prototype/Dirt.asset");
            Assert.IsNotNull(tile);
            view.Configure(tile, tile, tile);
            view.ConfigureRoad(tile);
            view.ConfigureRock(tile);
            view.ConfigureGravel(tile);
            view.ConfigureTrench(tile);
            view.ConfigureLadder(tile);
            cameraObject = new GameObject("Camera", typeof(Camera), typeof(MapCameraController));
            camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(20f, 16f, -10f);
            map.Configure(view, cameraObject.GetComponent<MapCameraController>());
            map.Regenerate();
            for (int row = 0; row < map.Data.Height; row++)
                for (int column = 0; column < map.Data.Width; column++)
                {
                    var cell = new Vector2Int(column, row);
                    map.Data.SetTree(cell, false);
                    map.Data[column, row] = TerrainKind.Dirt;
                    map.Data.SetWalkable(cell, true);
                }
            view.Render(map.Data);
            rosterObject = new GameObject("Soldiers", typeof(SoldierRoster));
            roster = rosterObject.GetComponent<SoldierRoster>();
            roster.Configure(map, view, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Prototype/Soldier.png"));
            roster.ConfigureCook(true);
            roster.Spawn(map.Data);
        }

        [TearDown]
        public void TearDown()
        {
            if (panelObject != null)
            {
                Invoke(panelObject.GetComponent<SoldierDetailsPanel>(), "OnDisable");
                Object.DestroyImmediate(panelObject);
            }
            if (orders != null)
                Invoke(orders, "OnDisable");
            if (kitchen != null)
                Invoke(kitchen, "OnDisable");
            Object.DestroyImmediate(rosterObject);
            Object.DestroyImmediate(mapObject);
            Object.DestroyImmediate(cameraObject);
            Time.timeScale = 1f;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void DeathLeavesCorpseRemovesSoldierFromRosterDropsInventoryAndPreservesOtherSoldiers()
        {
            SoldierView victim = roster.Soldiers[0];
            SoldierData data = victim.Data;
            GameObject corpse = victim.gameObject;
            Sprite livingSprite = victim.GetComponent<SpriteRenderer>().sprite;
            int count = roster.Soldiers.Count;
            data.Inventory.Add(ItemType.Shovel, 1);
            data.Inventory.Add(ItemType.Wood, 2);
            int dropped = data.Inventory.TotalCount;
            int groundItems = map.Data.GroundItemCount;
            int deaths = 0;
            roster.SoldierDied += soldier => { Assert.AreSame(victim, soldier); deaths++; };
            data.TakeDamage(100f);
            Assert.AreEqual(1, deaths);
            Assert.IsTrue(corpse.activeInHierarchy);
            Assert.AreNotSame(livingSprite, victim.GetComponent<SpriteRenderer>().sprite);
            CollectionAssert.DoesNotContain(roster.Soldiers, victim);
            Assert.AreEqual(count - 1, roster.Soldiers.Count);
            Assert.AreEqual(count - 1, roster.WorkCount);
            Assert.Zero(roster.GuardCount);
            Assert.AreEqual(groundItems + dropped, map.Data.GroundItemCount);
            Assert.Zero(data.Inventory.TotalCount);
            foreach (SoldierView survivor in roster.Soldiers)
            {
                Assert.IsTrue(survivor.IsAlive);
                Assert.AreEqual(100f, survivor.Data.Health);
            }
            data.TakeDamage(100f);
            Assert.AreEqual(1, deaths);
            roster.Spawn(map.Data);
            Assert.IsTrue(corpse == null);
            Assert.AreEqual(count, roster.Soldiers.Count);
            foreach (SoldierView soldier in roster.Soldiers)
                Assert.AreEqual(100f, soldier.Data.Health);
        }

        [Test]
        public void StarvationAndDehydrationCanKillAndRemoveSoldierFromSimulation()
        {
            SoldierView victim = roster.Soldiers[0];
            SoldierData data = victim.Data;
            int count = roster.Soldiers.Count;
            data.Inventory.TryTake(ItemType.FullCanteen, 1);
            data.Needs.AdvanceTime(SoldierNeeds.MaximumDepletionSeconds);
            Assert.Zero(data.Needs.Hunger);
            Assert.Zero(data.Needs.Thirst);
            data.AdvanceTime((float)GameClock.SecondsPerDay * 3f);
            Assert.Zero(data.Health);
            Assert.IsFalse(data.IsAlive);
            Assert.AreEqual(count - 1, roster.Soldiers.Count);
            foreach (SoldierView survivor in roster.Soldiers)
                Assert.IsTrue(survivor.IsAlive);
        }

        [Test]
        public void NoDropSpacePreservesInventoryInStorageWithoutPreventingDeath()
        {
            SoldierData data = roster.Soldiers[0].Data;
            data.Inventory.Add(ItemType.Shovel, 1);
            int shovels = map.Data.StorageStockpile.Count(ItemType.Shovel);
            int canteens = map.Data.StorageStockpile.Count(ItemType.FullCanteen);
            for (int row = 0; row < map.Data.Height; row++)
                for (int column = 0; column < map.Data.Width; column++)
                    map.Data.SetWalkable(new Vector2Int(column, row), false);
            data.TakeDamage(100f);
            Assert.IsFalse(data.IsAlive);
            Assert.Zero(data.Inventory.TotalCount);
            Assert.AreEqual(shovels + 1, map.Data.StorageStockpile.Count(ItemType.Shovel));
            Assert.AreEqual(canteens + 1, map.Data.StorageStockpile.Count(ItemType.FullCanteen));
        }

        [Test]
        public void DeathImmediatelyRemovesVisionButPreservesExploration()
        {
            var fog = rosterObject.AddComponent<SoldierFogOfWar>();
            fog.Configure(map, view, roster);
            Vector2Int cell = roster.Soldiers[0].Cell;
            Assert.IsTrue(map.Data.Fog.IsVisible(cell));
            var victims = new System.Collections.Generic.List<SoldierView>(roster.Soldiers);
            foreach (SoldierView soldier in victims)
                soldier.Data.TakeDamage(100f);
            Assert.Zero(roster.Soldiers.Count);
            Assert.IsFalse(map.Data.Fog.IsVisible(cell));
            Assert.IsTrue(map.Data.Fog.IsExplored(cell));
        }

        [Test]
        public void DeathReleasesOnlyVictimsWorkAndLeavesDesignationQueued()
        {
            orders = rosterObject.AddComponent<TrenchWorkOrders>();
            orders.Configure(map, view, roster, camera);
            Invoke(orders, "OnEnable");
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Digger);
            foreach (SoldierView soldier in roster.Soldiers)
                soldier.Data.Inventory.Add(ItemType.Shovel, 1);
            Assert.IsTrue(orders.Designate(new Vector2Int(10, 12)));
            Assert.IsTrue(orders.Designate(new Vector2Int(28, 12)));
            orders.Tick(0.01f);
            Assert.AreEqual(2, orders.Jobs.Count);
            TrenchJob first = orders.Jobs[0];
            TrenchJob other = orders.Jobs[1];
            Assert.IsNotNull(first.Worker);
            Assert.IsNotNull(other.Worker);
            SoldierView survivor = other.Worker;
            first.Worker.Data.TakeDamage(100f);
            Assert.AreEqual(2, orders.Jobs.Count);
            Assert.IsNull(first.Worker);
            Assert.AreSame(survivor, other.Worker);
            orders.Tick(1f);
            Assert.IsNotNull(first.Worker);
            Assert.IsTrue(first.Worker.IsAlive);
        }

        [Test]
        public void DeathDuringCollectionReleasesDeliveryAndDropsCollectedResource()
        {
            orders = rosterObject.AddComponent<TrenchWorkOrders>();
            orders.Configure(map, view, roster, camera);
            Invoke(orders, "OnEnable");
            var cell = new Vector2Int(10, 12);
            map.Data.AddGroundItem(cell, ItemType.Wood);
            Assert.IsTrue(orders.DesignateCollection(cell));
            TrenchJob job = orders.Jobs[0];
            for (int step = 0; step < 20 && !job.CollectedItem.HasValue; step++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                orders.Tick(0.1f);
            }
            Assert.AreEqual(ItemType.Wood, job.CollectedItem);
            Assert.IsNotNull(job.Worker);
            job.Worker.Data.TakeDamage(100f);
            Assert.Zero(orders.Jobs.Count);
            int droppedWood = 0;
            for (int row = 0; row < map.Data.Height; row++)
                for (int column = 0; column < map.Data.Width; column++)
                    if (map.Data.TryGetGroundItem(new Vector2Int(column, row), out ItemType item)
                        && item == ItemType.Wood)
                        droppedWood++;
            Assert.AreEqual(1, droppedWood);
            orders.Tick(0.1f);
            Assert.Zero(orders.Jobs.Count);
        }

        [Test]
        public void CookDeathPreservesCarriedProvisionsAndAssignsLivingReplacement()
        {
            SoldierView cook = roster.Soldiers[0];
            SoldierView replacement = roster.Soldiers[1];
            TaskTestSetup.Assign(cook, SoldierTask.Cook);
            TaskTestSetup.Assign(replacement, SoldierTask.Cook);
            kitchen = rosterObject.AddComponent<KitchenSystem>();
            kitchen.Configure(map, view, roster,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Prototype/Soldier.png"));
            kitchen.Rebuild();
            Invoke(kitchen, "OnEnable");
            Assert.AreSame(cook, kitchen.Cook);
            for (int step = 0; step < 20 && kitchen.CarriedProvisions == 0; step++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                kitchen.Tick(0.1f);
            }
            Assert.Greater(kitchen.CarriedProvisions, 0);
            int provisions = kitchen.Stock.KitchenProvisions + kitchen.CarriedProvisions;
            cook.Data.TakeDamage(100f);
            Assert.AreSame(replacement, kitchen.Cook);
            Assert.IsTrue(kitchen.Cook.IsAlive);
            Assert.Zero(kitchen.CarriedProvisions);
            Assert.AreEqual(provisions, kitchen.Stock.KitchenProvisions);
        }

        [Test]
        public void HealthBarUpdatesOnDamageAndDeathClearsSelection()
        {
            panelObject = new GameObject("Health UI", typeof(SoldierDetailsPanel));
            var panel = panelObject.GetComponent<SoldierDetailsPanel>();
            panel.Configure(roster, camera, prototype: map);
            Invoke(panel, "Awake");
            Invoke(panel, "OnEnable");
            SoldierView victim = roster.Soldiers[0];
            SoldierData data = victim.Data;
            panel.Select(victim);
            var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
            Text label = FindHealthLabel(canvas);
            Assert.AreEqual("Salute: 100/100", label.text);
            data.TakeDamage(40f);
            Assert.AreEqual("Salute: 60/100", label.text);
            var fill = label.transform.parent.Find("Track/Fill").GetComponent<Image>();
            Assert.AreEqual(0.6f, fill.rectTransform.anchorMax.x, 0.0001f);
            data.TakeDamage(60f);
            Assert.IsNull(panel.Selected);
            Assert.AreEqual(string.Empty, label.text);
        }

        private static Text FindHealthLabel(Canvas canvas)
        {
            foreach (Text text in canvas.GetComponentsInChildren<Text>(true))
                if (text.text.StartsWith("Salute: "))
                    return text;
            Assert.Fail("Barra Salute non trovata.");
            return null;
        }

        private static void Invoke(Component component, string method)
        {
            component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(component, null);
        }
    }
}