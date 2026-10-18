using NoMansGround.Editor;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NoMansGround.Tests
{
    internal static class TaskTestSetup
    {
        public static void Assign(SoldierView soldier, SoldierTask task)
        {
            typeof(SoldierData).GetProperty(nameof(SoldierData.Task)).SetValue(soldier.Data, task);
        }

        public static void AssignWorkers(SoldierRoster roster, SoldierTask task)
        {
            foreach (SoldierView soldier in roster.Soldiers)
                Assign(soldier, soldier.Data.Profession == SoldierProfession.Cook ? SoldierTask.Cook : task);
        }

        public static void AssignCook(SoldierRoster roster)
        {
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier.Data.Profession == SoldierProfession.Cook)
                    Assign(soldier, SoldierTask.Cook);
        }
    }

    public sealed class SoldierTaskTests
    {
        private Scene scene;
        private MapPrototype map;
        private MapTilemapView view;
        private SoldierRoster roster;
        private TrenchWorkOrders orders;
        private KitchenSystem kitchen;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene = EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out MapPrototype prototype)) map = prototype;
                if (root.TryGetComponent(out SoldierRoster soldiers)) roster = soldiers;
            }
            view = map.GetComponentInChildren<MapTilemapView>();
            orders = roster.GetComponent<TrenchWorkOrders>();
            map.Regenerate();
            roster.Spawn(map.Data);
            kitchen = roster.GetComponent<KitchenSystem>();
            kitchen.Rebuild();
            for (int y = 9; y <= 13; y++)
                for (int x = 45; x <= 69; x++)
                {
                    map.Data[x, y] = TerrainKind.Dirt;
                    map.Data.SetTree(new Vector2Int(x, y), false);
                    map.Data.SetWalkable(new Vector2Int(x, y), true);
                }
            view.Render(map.Data);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        private void FinishTaskChange(SoldierView soldier, SoldierTask task)
        {
            soldier.RequestTask(task);
            for (int step = 0; step < 60 && soldier.RequestedTask.HasValue; step++)
            {
                foreach (SoldierView worker in roster.Soldiers)
                    worker.AdvanceMovement(1000f);
                orders.Tick(0.05f);
            }
            Assert.AreEqual(task, soldier.Data.Task, soldier.TaskStatus);
            Assert.IsTrue(soldier.TaskReady, soldier.TaskStatus);
            Assert.IsFalse(soldier.RequestedTask.HasValue);
        }

        [Test]
        public void NewRosterHasOnlyHaulersAndNoToolsInPersonalInventory()
        {
            Assert.AreEqual(9, roster.Soldiers.Count);
            foreach (SoldierView soldier in roster.Soldiers)
            {
                Assert.AreEqual(SoldierTask.Hauler, soldier.Data.Task);
                Assert.IsTrue(soldier.TaskReady);
                Assert.AreEqual(1, soldier.Data.Inventory.TotalCount);
                Assert.AreEqual(1, soldier.Data.Inventory.Count(ItemType.FullCanteen));
            }
            Assert.IsTrue(kitchen.HasFacilities);
            Assert.IsNull(kitchen.Cook);
        }

        [Test]
        public void AnySoldierCanBecomeCookAndChangingCookPreservesKitchenStock()
        {
            SoldierView first = roster.Soldiers[0];
            Assert.AreEqual(SoldierProfession.Engineer, first.Data.Profession);
            FinishTaskChange(first, SoldierTask.Cook);
            for (int step = 0; step < 60 && kitchen.Stock.KitchenProvisions == 0; step++)
            {
                kitchen.Tick(0.1f);
                first.AdvanceMovement(1000f);
            }
            Assert.AreSame(first, kitchen.Cook);
            Assert.AreEqual(10, kitchen.Stock.KitchenProvisions);
            first.RequestTask(SoldierTask.Miner);
            kitchen.Tick(60f);
            Assert.AreEqual(1, kitchen.Stock.ReadyRations);
            Assert.IsFalse(first.HasWorkAssignment);
            FinishTaskChange(first, SoldierTask.Miner);
            kitchen.Tick(0.1f);
            Assert.IsNull(kitchen.Cook);
            Assert.AreEqual(1, kitchen.Stock.ReadyRations);
            FinishTaskChange(roster.Soldiers[1], SoldierTask.Cook);
            kitchen.Tick(0.1f);
            Assert.AreSame(roster.Soldiers[1], kitchen.Cook);
            Assert.AreEqual(1, kitchen.Stock.ReadyRations);
        }

        [TestCase(SoldierTask.Woodcutter, TrenchJobKind.CutTree)]
        [TestCase(SoldierTask.Miner, TrenchJobKind.MineRock)]
        [TestCase(SoldierTask.Builder, TrenchJobKind.Ladder)]
        [TestCase(SoldierTask.Hauler, TrenchJobKind.CollectItem)]
        public void AssignedJobMatchesSelectedTask(SoldierTask task, TrenchJobKind kind)
        {
            FinishTaskChange(roster.Soldiers[0], task);
            var cell = new Vector2Int(50, 10);
            if (kind == TrenchJobKind.CutTree)
            {
                map.Data.SetTree(cell, true);
                Assert.IsTrue(orders.DesignateTree(cell));
            }
            else if (kind == TrenchJobKind.MineRock)
            {
                map.Data[cell.x, cell.y] = TerrainKind.Rock;
                Assert.IsTrue(orders.DesignateMine(cell));
            }
            else if (kind == TrenchJobKind.Ladder)
            {
                view.CompleteTrench(map.Data, cell);
                Assert.IsTrue(orders.DesignateLadder(cell));
            }
            else
            {
                map.Data.AddGroundItem(cell, ItemType.Wood);
                Assert.IsTrue(orders.DesignateCollection(cell));
            }
            view.Render(map.Data);
            orders.Tick(0.1f);
            Assert.IsNotNull(orders.Jobs[0].Worker);
            Assert.AreEqual(task, orders.Jobs[0].Worker.Data.Task);
            Assert.AreEqual(kind, orders.Jobs[0].Kind);
        }

        [Test]
        public void TaskChangePhysicallyReturnsOldToolAndTakesNewToolWithoutTouchingCanteen()
        {
            SoldierView soldier = roster.Soldiers[0];
            soldier.RequestTask(SoldierTask.Woodcutter);
            Assert.Zero(soldier.Data.Inventory.Count(ItemType.Axe));
            Assert.AreEqual(2, map.Data.StorageStockpile.Count(ItemType.Axe));
            orders.Tick(0.01f);
            Assert.IsTrue(soldier.IsMoving);
            Assert.IsTrue(soldier.IsChangingTaskEquipment);
            Assert.IsFalse(soldier.CanTakeEngineeringWork);
            Assert.AreEqual(2, map.Data.StorageStockpile.Count(ItemType.Axe));
            FinishTaskChange(soldier, SoldierTask.Woodcutter);
            Assert.IsTrue(map.Data.IsStorageCell(soldier.Cell));
            Assert.AreEqual(1, soldier.Data.Inventory.Count(ItemType.Axe));
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.Axe));
            GridPath away = GridPathfinder.FindNearest(map.Data, new[] { soldier.Cell }, new Vector2Int(50, 10));
            Assert.IsNotNull(away);
            soldier.BeginMove(view, away.Cells);
            soldier.AdvanceMovement(1000f);
            soldier.RequestTask(SoldierTask.Miner);
            orders.Tick(0.01f);
            Assert.IsTrue(soldier.IsMoving);
            Assert.AreEqual(1, soldier.Data.Inventory.Count(ItemType.Axe));
            Assert.Zero(soldier.Data.Inventory.Count(ItemType.Pickaxe));
            FinishTaskChange(soldier, SoldierTask.Miner);
            Assert.Zero(soldier.Data.Inventory.Count(ItemType.Axe));
            Assert.AreEqual(2, map.Data.StorageStockpile.Count(ItemType.Axe));
            Assert.AreEqual(1, soldier.Data.Inventory.Count(ItemType.Pickaxe));
            FinishTaskChange(soldier, SoldierTask.Hauler);
            Assert.Zero(soldier.Data.Inventory.Count(ItemType.Pickaxe));
            Assert.AreEqual(2, map.Data.StorageStockpile.Count(ItemType.Pickaxe));
            Assert.AreEqual(1, soldier.Data.Inventory.Count(ItemType.FullCanteen));
        }

        [Test]
        public void MissingToolWaitsAndLatestRequestedTaskWinsAtDepot()
        {
            SoldierView soldier = roster.Soldiers[0];
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.Axe, 2));
            soldier.RequestTask(SoldierTask.Woodcutter);
            orders.Tick(0.01f);
            soldier.AdvanceMovement(1000f);
            orders.Tick(0.01f);
            Assert.IsFalse(soldier.TaskReady);
            Assert.AreEqual(SoldierTask.Woodcutter, soldier.RequestedTask);
            StringAssert.Contains("Accetta", soldier.TaskStatus);
            FinishTaskChange(soldier, SoldierTask.Miner);
            Assert.AreEqual(1, soldier.Data.Inventory.Count(ItemType.Pickaxe));
            Assert.Zero(soldier.Data.Inventory.Count(ItemType.Axe));
        }

        [Test]
        public void ExtraBuilderWaitsUntilAnotherBuilderReturnsHammer()
        {
            FinishTaskChange(roster.Soldiers[0], SoldierTask.Builder);
            FinishTaskChange(roster.Soldiers[1], SoldierTask.Builder);
            SoldierView waiting = roster.Soldiers[2];
            waiting.RequestTask(SoldierTask.Builder);
            orders.Tick(0.01f);
            waiting.AdvanceMovement(1000f);
            orders.Tick(0.01f);
            Assert.IsFalse(waiting.TaskReady);
            Assert.Zero(map.Data.StorageStockpile.Count(ItemType.ConstructionHammer));
            FinishTaskChange(roster.Soldiers[0], SoldierTask.Hauler);
            FinishTaskChange(waiting, SoldierTask.Builder);
            Assert.Zero(roster.Soldiers[0].Data.Inventory.Count(ItemType.ConstructionHammer));
            Assert.AreEqual(1, waiting.Data.Inventory.Count(ItemType.ConstructionHammer));
            Assert.AreEqual(1, roster.Soldiers[1].Data.Inventory.Count(ItemType.ConstructionHammer));
        }

        [Test]
        public void OnlyDiggerTakesDigJobsAndTaskChangeWaitsForActiveWork()
        {
            var cell = new Vector2Int(50, 10);
            Assert.IsTrue(orders.Designate(cell));
            orders.Tick(0.1f);
            Assert.IsNull(orders.Jobs[0].Worker);
            SoldierView digger = roster.Soldiers[0];
            FinishTaskChange(digger, SoldierTask.Digger);
            digger.AdvanceMovement(1000f);
            orders.Tick(0.01f);
            Assert.AreSame(digger, orders.Jobs[0].Worker);
            digger.RequestTask(SoldierTask.Miner);
            Assert.AreEqual(SoldierTask.Digger, digger.Data.Task);
            Assert.AreEqual(1, digger.Data.Inventory.Count(ItemType.Shovel));
            orders.Tick(10f);
            Assert.IsTrue(map.Data.IsTrench(cell));
            Assert.Zero(orders.Jobs.Count);
            FinishTaskChange(digger, SoldierTask.Miner);
            Assert.Zero(digger.Data.Inventory.Count(ItemType.Shovel));
            Assert.AreEqual(1, digger.Data.Inventory.Count(ItemType.Pickaxe));
        }

        [TestCase(1280, 720)]
        [TestCase(600, 1000)]
        public void TaskSelectorShowsAllTasksAndEditsOnlyTheSelectedSoldier(int width, int height)
        {
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                typeof(SoldierDetailsPanel).GetMethod("Awake", flags).Invoke(panel, null);
                typeof(SoldierDetailsPanel).GetMethod("OnEnable", flags).Invoke(panel, null);
                panel.Select(roster.Soldiers[0]);
                var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
                Dropdown selector = canvas.GetComponentInChildren<Dropdown>();
                Assert.IsNotNull(selector);
                Assert.AreEqual(6, selector.options.Count);
                Assert.AreEqual("Trasportatore", selector.captionText.text);
                selector.value = (int)SoldierTask.Miner;
                Assert.AreEqual(SoldierTask.Miner, roster.Soldiers[0].RequestedTask);
                Assert.IsFalse(roster.Soldiers[1].RequestedTask.HasValue);
                panel.Select(roster.Soldiers[1]);
                Assert.AreEqual((int)SoldierTask.Hauler, selector.value);
                Assert.IsFalse(roster.Soldiers[1].RequestedTask.HasValue);
                FinishTaskChange(roster.Soldiers[0], SoldierTask.Miner);
                panel.Select(roster.Soldiers[0]);
                Assert.AreEqual("Minatore", selector.captionText.text);
                typeof(Dropdown).GetMethod("Start", flags).Invoke(selector, null);
                selector.alphaFadeSpeed = 0f;
                selector.Show();
                Canvas.ForceUpdateCanvases();
                Transform list = selector.transform.Find("Dropdown List");
                Assert.IsNotNull(list);
                Assert.AreEqual(6, list.GetComponentsInChildren<Toggle>().Length);
                Camera camera = Camera.main;
                var target = new RenderTexture(width, height, 24);
                var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                RenderTexture oldTarget = camera.targetTexture;
                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.targetTexture = target;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                    Canvas.ForceUpdateCanvases();
                    var corners = new Vector3[4];
                    selector.GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 point = camera.WorldToScreenPoint(corner);
                        Assert.That(point.x, Is.InRange(-1f, width + 1f));
                        Assert.That(point.y, Is.InRange(-1f, height + 1f));
                    }
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        $"NoMansGround-Tasks-{width}x{height}.png"), pixels.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = oldTarget;
                    Object.DestroyImmediate(target);
                    Object.DestroyImmediate(pixels);
                    Object.DestroyImmediate(list.gameObject);
                    Transform blocker = canvas.transform.Find("Blocker");
                    if (blocker != null) Object.DestroyImmediate(blocker.gameObject);
                }
            }
            finally
            {
                typeof(SoldierDetailsPanel).GetMethod("OnDisable", flags).Invoke(panel, null);
            }
        }

        [Test]
        public void DutyStaysSeparateAndPausedTickDoesNotMoveOrTransferTools()
        {
            SoldierView soldier = roster.Soldiers[0];
            soldier.RequestDuty(SoldierDuty.Guard);
            soldier.RequestTask(SoldierTask.Miner);
            Vector3 position = soldier.transform.position;
            orders.Tick(0f);
            Assert.AreEqual(position, soldier.transform.position);
            Assert.IsFalse(soldier.IsMoving);
            Assert.AreEqual(2, map.Data.StorageStockpile.Count(ItemType.Pickaxe));
            FinishTaskChange(soldier, SoldierTask.Miner);
            Assert.AreEqual(SoldierDuty.Guard, soldier.Duty);
            Assert.IsFalse(soldier.CanTakeEngineeringWork);
            Assert.IsTrue(soldier.CanTakeGuardMove);
            soldier.RequestDuty(SoldierDuty.Work);
            Assert.IsTrue(soldier.CanTakeEngineeringWork);
        }
    }
}