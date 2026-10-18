using System.Collections.Generic;
using NoMansGround.Editor;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;

namespace NoMansGround.Tests
{
    public sealed class TrenchWorkTests
    {
        private Scene scene;
        private MapPrototype map;
        private MapTilemapView view;
        private SoldierRoster roster;
        private SoldierMovementOrders movement;
        private TrenchWorkOrders trenches;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene = EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out MapPrototype prototype))
                {
                    map = prototype;
                    view = root.GetComponentInChildren<MapTilemapView>();
                }
                if (root.TryGetComponent(out SoldierRoster soldiers))
                {
                    roster = soldiers;
                    movement = root.GetComponent<SoldierMovementOrders>();
                    trenches = root.GetComponent<TrenchWorkOrders>();
                }
            }
            Assert.IsNotNull(trenches);
            map.Regenerate();
            for (int x = 45; x < 70; x++)
            {
                var cell = new Vector2Int(x, 10);
                map.Data[x, 10] = TerrainKind.Dirt;
                map.Data.SetTree(cell, false);
                map.Data.SetWalkable(cell, true);
            }
            view.Render(map.Data);
            roster.Spawn(map.Data);
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.Shovel, 1));
                roster.Soldiers[i].Data.Inventory.Add(ItemType.Shovel, 1);
            }
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        [TestCase(ItemType.Wood)]
        [TestCase(ItemType.BuildingStone)]
        public void RoomRequiresFinishedPerimeterAndDoorAndOnlyDoorAllowsEntry(ItemType material)
        {
            var data = new MapData(10, 10);
            Assert.IsTrue(data.TryPlanBuilding(new Vector2Int(5, 5), new Vector2Int(2, 2), material, out BuildingRoom room));
            Assert.IsFalse(data.TrySetBuildingDoor(new Vector2Int(2, 2)));
            Assert.IsFalse(data.TrySetBuildingDoor(new Vector2Int(3, 3)));
            Assert.IsFalse(data.CompleteBuildingTile(new Vector2Int(3, 2)));
            Assert.IsFalse(data.IsIndoors(new Vector2Int(3, 3)));
            var door = new Vector2Int(3, 2);
            Assert.IsTrue(data.TrySetBuildingDoor(door));
            foreach (Vector2Int cell in TrenchWorkOrders.RectanglePerimeter(room.Bounds.min, room.Bounds.max - Vector2Int.one))
                if (cell != door)
                    Assert.IsTrue(data.CompleteBuildingTile(cell));
            Assert.IsFalse(room.IsComplete);
            Assert.IsFalse(data.IsIndoors(new Vector2Int(3, 3)));
            Assert.IsTrue(data.CompleteBuildingTile(door));
            Assert.IsTrue(room.IsComplete);
            Assert.IsTrue(data.IsIndoors(new Vector2Int(3, 3)));
            Assert.IsFalse(data.IsIndoors(door));
            Assert.IsTrue(data.CanStep(new Vector2Int(3, 1), door));
            Assert.IsTrue(data.CanStep(door, new Vector2Int(3, 3)));
            Assert.IsFalse(data.CanStep(new Vector2Int(2, 1), new Vector2Int(2, 2)));
            GridPath path = GridPathfinder.FindNearest(data, new[] { new Vector2Int(1, 3) }, new Vector2Int(3, 3));
            Assert.IsNotNull(path);
            CollectionAssert.Contains(path.Cells, door);
            Assert.IsFalse(data.CanStep(new Vector2Int(2, 1), door));
        }

        [TestCase(ItemType.Wood)]
        [TestCase(ItemType.BuildingStone)]
        public void RoofAppearsOnlyOnCompletionAndHoverHidesOnlyThePointedBuilding(ItemType material)
        {
            var data = new MapData(12, 10);
            Assert.IsTrue(data.TryPlanBuilding(new Vector2Int(2, 2), new Vector2Int(4, 4), material, out BuildingRoom first));
            Assert.IsTrue(data.TryPlanBuilding(new Vector2Int(6, 2), new Vector2Int(8, 4), material, out BuildingRoom second));
            var door = new Vector2Int(3, 2);
            Assert.IsTrue(data.TrySetBuildingDoor(door));
            Assert.IsTrue(data.TrySetBuildingDoor(new Vector2Int(7, 2)));
            foreach (BuildingRoom room in data.Buildings)
                foreach (Vector2Int cell in TrenchWorkOrders.RectanglePerimeter(room.Bounds.min, room.Bounds.max - Vector2Int.one))
                    if (cell != door)
                        Assert.IsTrue(data.CompleteBuildingTile(cell));
            view.Render(data);
            Tilemap roofs = view.transform.Find("Roofs").GetComponent<Tilemap>();
            Assert.IsFalse(roofs.HasTile(new Vector3Int(3, 3, 0)));
            Assert.IsTrue(roofs.HasTile(new Vector3Int(7, 3, 0)));
            Assert.IsTrue(data.CompleteBuildingTile(door));
            view.RenderBuildings(data);
            view.UpdateRoofVisibility(null, null);
            int roofCells = 0;
            foreach (TileBase tile in roofs.GetTilesBlock(roofs.cellBounds))
                if (tile != null) roofCells++;
            Assert.AreEqual(18, roofCells);
            foreach (SpriteRenderer renderer in roster.Soldiers[0].GetComponentsInChildren<SpriteRenderer>())
                Assert.Greater(roofs.GetComponent<TilemapRenderer>().sortingOrder, renderer.sortingOrder);
            Camera camera = Camera.main;
            var viewport = new RenderTexture(800, 600, 0);
            Vector3 oldPosition = camera.transform.position;
            float oldSize = camera.orthographicSize;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = viewport;
                camera.orthographicSize = 6f;
                camera.transform.position = new Vector3(6f, 4f, -10f);
                view.UpdateRoofVisibility(camera, camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(new Vector3Int(3, 3, 0))));
                Assert.Zero(roofs.GetColor(new Vector3Int(3, 3, 0)).a);
                Assert.AreEqual(1f, roofs.GetColor(new Vector3Int(7, 3, 0)).a);
                view.UpdateRoofVisibility(camera, camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(new Vector3Int(6, 2, 0))));
                Assert.AreEqual(1f, roofs.GetColor(new Vector3Int(3, 3, 0)).a);
                Assert.Zero(roofs.GetColor(new Vector3Int(7, 3, 0)).a);
                view.UpdateRoofVisibility(camera, new Vector2(-1f, -1f));
                Assert.AreEqual(1f, roofs.GetColor(new Vector3Int(7, 3, 0)).a);
                view.UpdateRoofVisibility(camera, null);
                Assert.IsTrue(data.IsIndoors(new Vector2Int(3, 3)));
                view.Render(new MapData(12, 10));
                Assert.IsFalse(roofs.HasTile(new Vector3Int(3, 3, 0)));
                Assert.IsFalse(roofs.HasTile(new Vector3Int(7, 3, 0)));
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.transform.position = oldPosition;
                camera.orthographicSize = oldSize;
                Object.DestroyImmediate(viewport);
            }
        }

        [Test]
        public void BuildingPlansRejectSmallBlockedOccupiedAndOverlappingAreas()
        {
            var data = new MapData(10, 10);
            Assert.IsFalse(data.TryPlanBuilding(Vector2Int.zero, Vector2Int.one, ItemType.Wood, out _));
            Assert.IsFalse(data.TryPlanBuilding(Vector2Int.zero, new Vector2Int(3, 3), ItemType.Shovel, out _));
            Assert.IsFalse(data.TryPlanBuilding(new Vector2Int(-1, 0), new Vector2Int(3, 3), ItemType.Wood, out _));
            data.SetTree(Vector2Int.one, true);
            Assert.IsFalse(data.TryPlanBuilding(Vector2Int.zero, new Vector2Int(3, 3), ItemType.Wood, out _));
            Assert.Zero(data.Buildings.Count);
            data.SetTree(Vector2Int.one, false);
            Assert.IsTrue(data.TryPlanBuilding(Vector2Int.zero, new Vector2Int(3, 3), ItemType.Wood, out _));
            Assert.IsFalse(data.TryPlanBuilding(Vector2Int.one, new Vector2Int(5, 5), ItemType.BuildingStone, out _));
            Assert.IsFalse(data.TryDesignateZone(MapZoneKind.Dump, Vector2Int.one, new Vector2Int(2, 2), out _));
            Assert.AreEqual(1, data.Buildings.Count);
        }

        [TestCase(ItemType.Wood)]
        [TestCase(ItemType.BuildingStone)]
        public void EngineersFetchAndConsumeOneMaterialPerTileAndFinishRoom(ItemType material)
        {
            PrepareBuildingArea();
            map.Data.StorageStockpile.Add(material, 8);
            ItemType other = material == ItemType.Wood ? ItemType.BuildingStone : ItemType.Wood;
            map.Data.StorageStockpile.Add(other, 3);
            Assert.IsTrue(trenches.DesignateBuilding(new Vector2Int(50, 10), new Vector2Int(52, 12), material));
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsFalse(trenches.Designate(new Vector2Int(51, 11)));
            Assert.IsTrue(trenches.DesignateDoor(new Vector2Int(51, 10)));
            Assert.AreEqual(8, trenches.Jobs.Count);
            for (int iteration = 0; iteration < 200 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.Buildings[0].IsComplete);
            Assert.IsTrue(map.Data.IsIndoors(new Vector2Int(51, 11)));
            Assert.Zero(map.Data.StorageStockpile.Count(material));
            Assert.AreEqual(3, map.Data.StorageStockpile.Count(other));
            foreach (SoldierView soldier in roster.Soldiers)
                Assert.Zero(soldier.Data.Inventory.Count(material));
            Tilemap buildings = view.transform.Find("Buildings").GetComponent<Tilemap>();
            Assert.AreEqual(material == ItemType.Wood ? "WoodDoor" : "StoneDoor",
                buildings.GetTile(new Vector3Int(51, 10, 0)).name);
            map.Regenerate();
            Assert.Zero(map.Data.Buildings.Count);
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsFalse(buildings.HasTile(new Vector3Int(51, 10, 0)));
        }

        [Test]
        public void ConstructionWaitsForMaterialsAndCancelledPickupKeepsResources()
        {
            PrepareBuildingArea();
            Assert.IsTrue(trenches.DesignateBuilding(new Vector2Int(50, 10), new Vector2Int(52, 12), ItemType.Wood));
            Assert.IsTrue(trenches.DesignateDoor(new Vector2Int(51, 10)));
            trenches.Tick(1f);
            foreach (TrenchJob job in trenches.Jobs)
            {
                Assert.IsNull(job.Worker);
                Assert.Zero(job.Progress);
            }
            map.Data.StorageStockpile.Add(ItemType.Wood, 1);
            trenches.Tick(1f);
            SoldierView picker = null;
            foreach (TrenchJob job in trenches.Jobs)
                if (job.PickingTool == ItemType.Wood)
                    picker = job.Worker;
            Assert.IsNotNull(picker);
            picker.AdvanceMovement(1000f);
            trenches.Tick(0.01f);
            Assert.AreEqual(1, picker.Data.Inventory.Count(ItemType.Wood));
            Assert.IsTrue(trenches.Cancel(new Vector2Int(51, 10)));
            Assert.Zero(trenches.Jobs.Count);
            Assert.Zero(map.Data.Buildings.Count);
            Assert.AreEqual(1, picker.Data.Inventory.Count(ItemType.Wood));
        }

        [TestCase(ItemType.Wood)]
        [TestCase(ItemType.BuildingStone)]
        public void EngineersBuildUsingGroundMaterialsWithoutDepositingThem(ItemType material)
        {
            PrepareBuildingArea();
            Assert.IsTrue(trenches.DesignateBuilding(new Vector2Int(50, 10), new Vector2Int(52, 12), material));
            for (int x = 45; x < 53; x++)
                map.Data.AddGroundItem(new Vector2Int(x, 10), material);
            view.RefreshGroundItems(map.Data);
            Assert.IsTrue(trenches.DesignateDoor(new Vector2Int(51, 10)));
            trenches.Tick(0.01f);
            var reserved = new HashSet<Vector2Int>();
            foreach (TrenchJob job in trenches.Jobs)
                if (job.GroundPickupCell.HasValue)
                    Assert.IsTrue(reserved.Add(job.GroundPickupCell.Value));
            Assert.Greater(reserved.Count, 0);
            Assert.AreEqual(8, map.Data.GroundItemCount);
            for (int iteration = 0; iteration < 200 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.Buildings[0].IsComplete);
            Assert.Zero(map.Data.GroundItemCount);
            Assert.Zero(map.Data.StorageStockpile.Count(material));
            foreach (SoldierView soldier in roster.Soldiers)
                Assert.Zero(soldier.Data.Inventory.Count(material));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GroundMaterialPickupHandlesCancellationAndMissingResourceWithoutDuplication(bool cancel)
        {
            PrepareBuildingArea();
            var source = new Vector2Int(45, 10);
            map.Data.AddGroundItem(source, ItemType.Wood);
            Assert.IsTrue(trenches.DesignateBuilding(new Vector2Int(50, 10), new Vector2Int(52, 12), ItemType.Wood));
            Assert.IsTrue(trenches.DesignateDoor(new Vector2Int(51, 10)));
            trenches.Tick(0.01f);
            TrenchJob pickup = null;
            int pickups = 0;
            foreach (TrenchJob job in trenches.Jobs)
                if (job.GroundPickupCell == source)
                {
                    pickup = job;
                    pickups++;
                }
            Assert.AreEqual(1, pickups);
            Assert.Zero(trenches.DesignateCollection(source, source));
            SoldierView worker = pickup.Worker;
            if (cancel)
                Assert.IsTrue(trenches.Cancel(new Vector2Int(51, 10)));
            else
            {
                Assert.IsTrue(map.Data.TryTakeGroundItem(source, out _));
                map.Data.AddGroundItem(source, ItemType.BuildingStone);
            }
            worker.AdvanceMovement(1000f);
            trenches.Tick(0.01f);
            Assert.IsTrue(map.Data.TryGetGroundItem(source, out ItemType remaining));
            Assert.AreEqual(cancel ? ItemType.Wood : ItemType.BuildingStone, remaining);
            Assert.IsFalse(worker.HasWorkAssignment);
            Assert.IsFalse(pickup.GroundPickupCell.HasValue);
            Assert.Zero(worker.Data.Inventory.Count(ItemType.Wood));
            Assert.Zero(worker.Data.Inventory.Count(ItemType.BuildingStone));
            Assert.Zero(map.Data.StorageStockpile.Count(ItemType.Wood));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CollectionRectangleIncludesInteriorSkipsToolsAndDoesNotDuplicateJobs(bool reverse)
        {
            PrepareBuildingArea();
            var from = new Vector2Int(49, 9);
            var to = new Vector2Int(53, 13);
            var first = new Vector2Int(49, 9);
            var interior = new Vector2Int(51, 11);
            var last = new Vector2Int(53, 13);
            var outside = new Vector2Int(54, 10);
            map.Data.AddGroundItem(first, ItemType.Wood);
            map.Data.AddGroundItem(interior, ItemType.BuildingStone);
            map.Data.AddGroundItem(last, ItemType.Wood);
            map.Data.AddGroundItem(new Vector2Int(50, 10), ItemType.Shovel);
            map.Data.AddGroundItem(outside, ItemType.BuildingStone);
            Assert.IsTrue(trenches.DesignateCollection(first));
            Assert.AreEqual(2, trenches.DesignateCollection(reverse ? to : from, reverse ? from : to));
            Assert.AreEqual(3, trenches.Jobs.Count);
            var targets = new List<Vector2Int>();
            foreach (TrenchJob job in trenches.Jobs)
            {
                Assert.AreEqual(TrenchJobKind.CollectItem, job.Kind);
                targets.Add(job.Cell);
            }
            CollectionAssert.AreEquivalent(new[] { first, interior, last }, targets);
            Assert.Zero(trenches.DesignateCollection(from, to));
            Assert.AreEqual(3, trenches.Jobs.Count);
            Assert.AreEqual(1, trenches.DesignateCollection(outside, outside));
            Assert.AreEqual(4, trenches.Jobs.Count);
            Assert.AreEqual(5, map.Data.GroundItemCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TreeCutRectangleIncludesInteriorAndSkipsEmptyCellsAndExistingJobs(bool reverse)
        {
            PrepareBuildingArea();
            var from = new Vector2Int(49, 9);
            var to = new Vector2Int(53, 13);
            var interior = new Vector2Int(51, 11);
            var outside = new Vector2Int(54, 10);
            foreach (Vector2Int cell in new[] { from, interior, to, outside })
                map.Data.SetTree(cell, true);
            Assert.IsTrue(trenches.DesignateTree(from));
            Assert.AreEqual(2, trenches.DesignateTree(reverse ? to : from, reverse ? from : to));
            Assert.AreEqual(3, trenches.Jobs.Count);
            var targets = new List<Vector2Int>();
            foreach (TrenchJob job in trenches.Jobs)
            {
                Assert.AreEqual(TrenchJobKind.CutTree, job.Kind);
                targets.Add(job.Cell);
            }
            CollectionAssert.AreEquivalent(new[] { from, interior, to }, targets);
            Assert.Zero(trenches.DesignateTree(from, to));
            Assert.AreEqual(1, trenches.DesignateTree(outside, outside));
            Assert.AreEqual(4, trenches.Jobs.Count);
            foreach (Vector2Int cell in targets)
                Assert.IsTrue(map.Data.HasTree(cell));
        }

        private void PrepareBuildingArea()
        {
            for (int y = 9; y <= 13; y++)
                for (int x = 49; x <= 53; x++)
                {
                    var cell = new Vector2Int(x, y);
                    map.Data[x, y] = TerrainKind.Dirt;
                    map.Data.SetTree(cell, false);
                    map.Data.SetWalkable(cell, true);
                }
            view.Render(map.Data);
        }

        [TestCase(0, 0, 7, 0)]
        [TestCase(7, 7, 0, 0)]
        [TestCase(2, 8, 5, 1)]
        [TestCase(3, 3, 3, 3)]
        public void DragRasterIncludesEndpointsWithoutDuplicates(int x0, int y0, int x1, int y1)
        {
            var from = new Vector2Int(x0, y0);
            var to = new Vector2Int(x1, y1);
            var line = TrenchWorkOrders.RasterLine(from, to);
            Assert.AreEqual(from, line[0]);
            Assert.AreEqual(to, line[line.Count - 1]);
            Assert.AreEqual(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1, line.Count);
            Assert.AreEqual(line.Count, new HashSet<Vector2Int>(line).Count);
        }

        [Test]
        public void AdjacentWorkerCompletesTrenchWithExactStrengthDurationAndExperience()
        {
            SoldierView worker = roster.Soldiers[0];
            Vector2Int cell = worker.Cell + Vector2Int.down;
            Assert.IsTrue(trenches.Designate(cell));
            Assert.IsFalse(trenches.Designate(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.AreSame(worker, job.Worker);
            Assert.IsFalse(worker.IsMoving);
            Assert.IsTrue(worker.HasWorkAssignment);
            Assert.IsFalse(worker.IsAvailable);
            SkillProgress strength = worker.Data.GetSkill(SoldierSkill.Strength);
            int before = strength.Experience;
            float duration = 6f / (1f + strength.Value / 100f);
            trenches.Tick(duration * 0.5f);
            Assert.AreEqual(0.5f, job.Progress, 0.0001f);
            Assert.AreEqual(0.5f, worker.DiggingProgress, 0.0001f);
            trenches.Tick(duration * 0.5f + 0.001f);
            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(TerrainKind.Trench, map.Data[cell.x, cell.y]);
            Assert.IsTrue(map.Data.IsWalkable(cell));
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreEqual(before + 25, strength.Experience);
            Assert.IsFalse(trenches.Designate(cell));
            Assert.AreEqual("Trench", view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
        }

        [Test]
        public void DigJobSendsUnequippedWorkerToCollectShovelAndAddsItToInventory()
        {
            ReturnShovelsToDepot();
            var cell = roster.Soldiers[0].Cell + Vector2Int.down;
            Assert.IsTrue(trenches.Designate(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.IsNotNull(job.Worker);
            Assert.AreEqual(ItemType.Shovel, job.PickingTool);
            SoldierView picker = job.Worker;
            Assert.Zero(picker.Data.Inventory.Count(ItemType.Shovel));
            Assert.AreEqual(6, map.Data.StorageStockpile.Count(ItemType.Shovel));

            picker.AdvanceMovement(1000f);
            trenches.Tick(0.01f);
            Assert.AreEqual(1, picker.Data.Inventory.Count(ItemType.Shovel));
            Assert.AreEqual(5, map.Data.StorageStockpile.Count(ItemType.Shovel));
            Assert.AreEqual(2, map.Data.StorageStockpile.Count(ItemType.Axe));
            Assert.AreEqual(2, map.Data.StorageStockpile.Count(ItemType.Pickaxe));
            Assert.IsFalse(job.PickingTool.HasValue);
            Transform depotDisplay = view.transform.Find("Depot stockpile items");
            Assert.AreEqual(4, depotDisplay.GetComponentsInChildren<SpriteRenderer>().Length);
            var labels = new HashSet<string>();
            foreach (TextMesh label in depotDisplay.GetComponentsInChildren<TextMesh>())
                labels.Add(label.text);
            CollectionAssert.Contains(labels, "Pala x5");
            CollectionAssert.Contains(labels, "Accetta x2");
            CollectionAssert.Contains(labels, "Piccone x2");

            trenches.Tick(0.5f);
            Assert.AreSame(picker, job.Worker);
            Assert.IsTrue(picker.IsMoving || picker.HasWorkAssignment);
        }

        [Test]
        public void DigJobWaitsWhenNoShovelIsCarriedOrAvailableInTheDepot()
        {
            ReturnShovelsToDepot();
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.Shovel, 6));
            Assert.IsTrue(trenches.Designate(new Vector2Int(50, 10)));
            trenches.Tick(0.01f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.AreEqual("Nessuna pala disponibile", trenches.Jobs[0].Status);
            Assert.Zero(trenches.Jobs[0].Progress);
        }

        [Test]
        public void TreeCutCollectsAnAxeAndReplacesTheTreeWithWood()
        {
            var cell = new Vector2Int(50, 10);
            map.Data.SetTree(cell, true);
            view.Render(map.Data);
            Assert.IsTrue(trenches.DesignateTree(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.IsTrue(job.PickingTool.HasValue);
            Assert.AreEqual(ItemType.Axe, job.PickingTool.Value);
            SoldierView cutter = job.Worker;
            cutter.AdvanceMovement(1000f);
            trenches.Tick(0.01f);
            Assert.AreEqual(1, cutter.Data.Inventory.Count(ItemType.Axe));
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.Axe));

            for (int i = 0; i < 20 && trenches.Jobs.Count > 0; i++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(1f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.IsFalse(map.Data.HasTree(cell));
            Assert.IsTrue(map.Data.TryGetGroundItem(cell, out ItemType drop));
            Assert.AreEqual(ItemType.Wood, drop);
            Assert.AreEqual(1, map.Data.GroundItemCount);
            Tilemap nature = view.transform.Find("Trees").GetComponent<Tilemap>();
            Assert.AreEqual("Legna", nature.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            Assert.IsTrue(map.Data.IsWalkable(cell));
        }

        [Test]
        public void TreeCutWaitsWhenNoAxeIsAvailable()
        {
            var cell = new Vector2Int(50, 10);
            map.Data.SetTree(cell, true);
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.Axe, 2));
            Assert.IsTrue(trenches.DesignateTree(cell));
            trenches.Tick(0.01f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.AreEqual("Nessuna accetta disponibile", trenches.Jobs[0].Status);
            Assert.AreEqual(TerrainKind.Dirt, map.Data[cell.x, cell.y]);
            Assert.IsTrue(map.Data.HasTree(cell));
        }

        [Test]
        public void MineJobCollectsPickaxeWorksTenSecondsAndDropsBuildingStoneOnGravel()
        {
            var cell = new Vector2Int(50, 10);
            map.Data[cell.x, cell.y] = TerrainKind.Rock;
            foreach (Vector2Int offset in new[]
                { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int adjacent = cell + offset;
                map.Data[adjacent.x, adjacent.y] = TerrainKind.Dirt;
                map.Data.SetTree(adjacent, false);
                map.Data.SetWalkable(adjacent, true);
            }
            view.Render(map.Data);
            Assert.IsTrue(trenches.DesignateMine(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.IsNotNull(job.Worker);
            Assert.AreEqual(ItemType.Pickaxe, job.PickingTool);
            SoldierView miner = job.Worker;

            for (int i = 0; i < 30 && (job.PickingTool.HasValue || miner.IsMoving); i++)
            {
                miner.AdvanceMovement(1000f);
                trenches.Tick(0.01f);
                if (job.Worker != null)
                    miner = job.Worker;
            }
            Assert.IsFalse(job.PickingTool.HasValue);
            Assert.IsNotNull(job.Worker);
            Assert.IsFalse(job.Worker.IsMoving);
            Assert.AreEqual(1, miner.Data.Inventory.Count(ItemType.Pickaxe));
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.Pickaxe));
            Assert.Less(job.Progress, 1f);

            float remainingWork = 10f * (1f - job.Progress);
            trenches.Tick(remainingWork - 0.01f);
            Assert.AreEqual(TerrainKind.Rock, map.Data[cell.x, cell.y]);
            Assert.IsFalse(map.Data.TryGetGroundItem(cell, out _));
            trenches.Tick(0.02f);
            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(TerrainKind.Gravel, map.Data[cell.x, cell.y]);
            Assert.IsTrue(map.Data.TryGetGroundItem(cell, out ItemType drop));
            Assert.AreEqual(ItemType.BuildingStone, drop);
            Assert.AreEqual("Gravel", view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            Tilemap nature = view.transform.Find("Trees").GetComponent<Tilemap>();
            Assert.AreEqual("Pietra da costruzione", nature.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
        }

        [Test]
        public void CollectJobPicksUpGroundResourceAndDeliversItToTheDepot()
        {
            var cell = new Vector2Int(50, 10);
            map.Data.AddGroundItem(cell, ItemType.Wood);
            view.Render(map.Data);
            Assert.IsTrue(trenches.DesignateCollection(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.AreEqual(TrenchJobKind.CollectItem, job.Kind);
            Assert.IsNotNull(job.Worker);
            SoldierView collector = job.Worker;

            for (int i = 0; i < 20 && !job.CollectedItem.HasValue; i++)
            {
                collector.AdvanceMovement(1000f);
                trenches.Tick(0.01f);
            }
            Assert.AreEqual(ItemType.Wood, job.CollectedItem);
            Assert.IsFalse(map.Data.TryGetGroundItem(cell, out _));
            Assert.AreEqual(1, job.Worker.Data.Inventory.Count(ItemType.Wood));

            for (int i = 0; i < 30 && trenches.Jobs.Count > 0; i++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(0.01f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.Wood));
            Assert.AreEqual(0, collector.Data.Inventory.Count(ItemType.Wood));
            Transform depotDisplay = view.transform.Find("Depot stockpile items");
            var labels = new HashSet<string>();
            foreach (TextMesh label in depotDisplay.GetComponentsInChildren<TextMesh>())
                labels.Add(label.text);
            CollectionAssert.Contains(labels, "Legna x1");
        }

        [Test]
        public void QueuedJobsReserveDistinctWorkersAndAdjacentEndpointsAndContinueAutomatically()
        {
            for (int x = 50; x < 62; x++)
                Assert.IsTrue(trenches.Designate(new Vector2Int(x, 10)));
            trenches.Tick(0.01f);
            var workers = new HashSet<SoldierView>();
            var endpoints = new HashSet<Vector2Int>();
            foreach (TrenchJob job in trenches.Jobs)
            {
                if (job.Worker == null)
                    continue;
                Assert.IsTrue(workers.Add(job.Worker));
                Assert.IsTrue(endpoints.Add(job.Worker.Destination));
                Assert.AreEqual(1, Mathf.Abs(job.Cell.x - job.Worker.Destination.x)
                    + Mathf.Abs(job.Cell.y - job.Worker.Destination.y));
            }
            Assert.AreEqual(2, workers.Count);
            for (int step = 0; step < 60 && trenches.Jobs.Count > 0; step++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(100f);
                trenches.Tick(1f);
            }
            Assert.Zero(trenches.Jobs.Count);
            for (int x = 50; x < 62; x++)
                Assert.AreEqual(TerrainKind.Trench, map.Data[x, 10]);
        }

        private void ReturnShovelsToDepot()
        {
            foreach (SoldierView soldier in roster.Soldiers)
            {
                int count = soldier.Data.Inventory.Count(ItemType.Shovel);
                if (count > 0)
                {
                    Assert.IsTrue(soldier.Data.Inventory.TryTake(ItemType.Shovel, count));
                    map.Data.StorageStockpile.Add(ItemType.Shovel, count);
                }
            }
        }

        [Test]
        public void CancellationReleasesWorkerKeepsTerrainAndDoesNotAwardExperience()
        {
            var cell = roster.Soldiers[0].Cell + Vector2Int.down;
            TerrainKind original = map.Data[cell.x, cell.y];
            trenches.Designate(cell);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            int xp = worker.Data.GetSkill(SoldierSkill.Strength).Experience;
            trenches.Tick(1f);
            Assert.IsTrue(trenches.Cancel(cell));
            Assert.IsFalse(trenches.Cancel(cell));
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreEqual(original, map.Data[cell.x, cell.y]);
            Assert.AreEqual(xp, worker.Data.GetSkill(SoldierSkill.Strength).Experience);
            Assert.Zero(trenches.Jobs.Count);
        }

        [Test]
        public void CancelInTransitDoesNotTeleportOrInterruptOtherMovement()
        {
            var cell = new Vector2Int(50, 10);
            trenches.Designate(cell);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            Assert.IsTrue(worker.IsMoving);
            Vector3 before = worker.transform.position;
            trenches.Cancel(cell);
            Assert.IsFalse(worker.HasWorkAssignment);
            Assert.IsTrue(worker.IsMoving);
            Assert.AreEqual(before, worker.transform.position);
            worker.AdvanceMovement(100f);
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreNotEqual(TerrainKind.Trench, map.Data[cell.x, cell.y]);
        }

        [Test]
        public void UnreachableWorkRemainsPendingAndRetriesAfterAccessOpens()
        {
            var cell = new Vector2Int(50, 10);
            foreach (Vector2Int delta in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(cell + delta, false);
            Assert.IsTrue(trenches.Designate(cell));
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.AreEqual("Accesso bloccato o occupato", trenches.Jobs[0].Status);
            map.Data.SetWalkable(cell + Vector2Int.down, true);
            trenches.Tick(1f);
            Assert.IsNotNull(trenches.Jobs[0].Worker);
            LogAssert.Expect(LogType.Warning, "Ostacoli debug: annulla o completa prima gli scavi.");
            Assert.IsFalse(movement.ToggleObstacle(new Vector2Int(10, 10)));
        }

        [Test]
        public void AssignedWorkersAreExcludedFromSharedMovementAndDisabledWorkersReleaseJob()
        {
            var cell = roster.Soldiers[0].Cell + Vector2Int.down;
            trenches.Designate(cell);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            movement.Selected = roster.Soldiers[1];
            movement.Selected.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(movement.IssueMove(worker.Cell + Vector2Int.left));
            Assert.IsFalse(worker.IsMoving);
            worker.enabled = false;
            trenches.Tick(0.1f);
            Assert.IsFalse(worker.HasWorkAssignment);
            Assert.IsNull(trenches.Jobs[0].Worker);
        }

        [Test]
        public void InvalidDesignationsAndPauseDoNotProduceWork()
        {
            Assert.IsFalse(trenches.Designate(new Vector2Int(-1, 0)));
            var cell = new Vector2Int(50, 10);
            map.Data.SetWalkable(cell, false);
            Assert.IsFalse(trenches.Designate(cell));
            map.Data.SetWalkable(cell, true);
            trenches.Designate(cell);
            trenches.Tick(0f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => trenches.Tick(float.NaN));
        }

        [Test]
        public void BlockedEarlierJobDoesNotStarveLaterReachableJob()
        {
            var blocked = new Vector2Int(50, 10);
            foreach (Vector2Int delta in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(blocked + delta, false);
            trenches.Designate(blocked);
            trenches.Designate(roster.Soldiers[0].Cell + Vector2Int.down);
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.IsNotNull(trenches.Jobs[1].Worker);
        }

        [Test]
        public void ManualMovementCannotClaimReservedWorkEndpoint()
        {
            trenches.Designate(new Vector2Int(50, 10));
            trenches.Tick(0.01f);
            Vector2Int reserved = trenches.Jobs[0].Worker.Destination;
            movement.Selected = roster.Soldiers[7];
            movement.Selected.RequestDuty(SoldierDuty.Guard);
            movement.Selected.AdvanceMovement(100f);
            LogAssert.Expect(LogType.Warning, "Destinazione gia occupata o prenotata.");
            Assert.IsFalse(movement.IssueMove(reserved));
        }

        [Test]
        public void GuardRequestWaitsForCurrentJobThenExcludesWorkerFromNextJob()
        {
            SoldierView worker = roster.Soldiers[0];
            Assert.AreEqual(9, roster.WorkCount);
            Assert.Zero(roster.GuardCount);
            trenches.Designate(worker.Cell + Vector2Int.down);
            trenches.Tick(0.01f);
            Assert.AreSame(worker, trenches.Jobs[0].Worker);
            worker.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(worker.GuardRequested);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            Assert.AreEqual(9, roster.WorkCount);
            trenches.Tick(10f);
            Assert.AreEqual(SoldierDuty.Guard, worker.Duty);
            Assert.IsFalse(worker.GuardRequested);
            Assert.AreEqual(8, roster.WorkCount);
            Assert.AreEqual(1, roster.GuardCount);
            Assert.IsFalse(worker.CanTakeEngineeringWork);
            trenches.Designate(worker.Cell + Vector2Int.left);
            trenches.Tick(1f);
            Assert.AreNotSame(worker, trenches.Jobs[0].Worker);
            worker.RequestDuty(SoldierDuty.Work);
            Assert.IsTrue(worker.CanTakeEngineeringWork);
        }

        [Test]
        public void PendingGuardCanBeCancelledAndCookCannotLeaveWork()
        {
            SoldierView worker = roster.Soldiers[0];
            trenches.Designate(worker.Cell + Vector2Int.down);
            trenches.Tick(0.01f);
            worker.RequestDuty(SoldierDuty.Guard);
            worker.RequestDuty(SoldierDuty.Work);
            trenches.Tick(10f);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            Assert.IsFalse(worker.GuardRequested);
            LogAssert.Expect(LogType.Warning, "Il cuoco resta in Lavoro.");
            Assert.IsFalse(roster.Soldiers[8].RequestDuty(SoldierDuty.Guard));
            Assert.AreEqual(SoldierDuty.Work, roster.Soldiers[8].Duty);
        }

        [Test]
        public void GuardRequestDuringWorkTravelFinishesJobBeforeChangingState()
        {
            var target = new Vector2Int(50, 10);
            trenches.Designate(target);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            Assert.IsTrue(worker.IsMoving);
            worker.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(worker.GuardRequested);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            worker.AdvanceMovement(100f);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            trenches.Tick(1f);
            Assert.IsTrue(worker.HasWorkAssignment);
            Assert.IsTrue(worker.GuardRequested);
            trenches.Tick(10f);
            Assert.IsTrue(map.Data.IsTrench(target));
            Assert.AreEqual(SoldierDuty.Guard, worker.Duty);
            Assert.AreEqual(1, roster.GuardCount);
        }

        [Test]
        public void SelectedWorkerAndMissingSelectionCannotReceiveGuardMoves()
        {
            movement.Selected = null;
            LogAssert.Expect(LogType.Warning, "Seleziona un soldato in Guardia.");
            Assert.IsFalse(movement.IssueMove(new Vector2Int(50, 10)));
            movement.Selected = roster.Soldiers[0];
            LogAssert.Expect(LogType.Warning, "Il soldato deve essere in Guardia e libero.");
            Assert.IsFalse(movement.IssueMove(new Vector2Int(50, 10)));
            Assert.IsFalse(roster.Soldiers[0].IsMoving);
            roster.Soldiers[0].RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(movement.IssueMove(new Vector2Int(50, 10)));
            roster.Soldiers[0].AdvanceMovement(100f);
            Assert.AreEqual(SoldierDuty.Guard, roster.Soldiers[0].Duty);
            Assert.IsFalse(roster.Soldiers[0].CanTakeEngineeringWork);
            Assert.IsTrue(movement.IssueMove(roster.Soldiers[0].Cell));
        }

        [Test]
        public void LadderWorkUsesAdjacentWorkerExactDurationExperienceAndPersistentTile()
        {
            SoldierView worker = roster.Soldiers[0];
            Vector2Int cell = worker.Cell + Vector2Int.down;
            view.CompleteTrench(map.Data, cell);
            Assert.IsFalse(map.Data.CanStep(worker.Cell, cell));
            Assert.IsTrue(trenches.DesignateLadder(cell));
            Assert.IsFalse(trenches.DesignateLadder(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.AreEqual(TrenchJobKind.Ladder, job.Kind);
            Assert.AreSame(worker, job.Worker);
            Assert.IsFalse(worker.IsMoving);
            Assert.IsTrue(worker.HasWorkAssignment);
            Assert.AreEqual(TrenchJobKind.Ladder, worker.WorkKind);
            var strength = worker.Data.GetSkill(SoldierSkill.Strength);
            int xp = strength.Experience;
            float duration = 4f / (1f + strength.Value / 100f);
            trenches.Tick(duration / 2f);
            Assert.AreEqual(0.5f, job.Progress, 0.0001f);
            Assert.IsFalse(map.Data.HasLadder(cell));
            trenches.Tick(duration / 2f + 0.001f);
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreEqual(xp + 25, strength.Experience);
            Assert.IsTrue(map.Data.HasLadder(cell));
            Assert.IsTrue(map.Data.CanStep(worker.Cell, cell));
            Assert.AreEqual("Ladder", view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            view.Render(map.Data);
            Assert.AreEqual("Ladder", view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            Assert.IsFalse(trenches.DesignateLadder(cell));
            worker.RequestDuty(SoldierDuty.Guard);
            movement.Selected = worker;
            Assert.IsTrue(movement.IssueMove(cell));
            worker.AdvanceMovement(100f);
            Assert.AreEqual(cell, worker.Cell);
        }

        [Test]
        public void LadderCancellationPreservesTrenchAndDoesNotOpenAccessOrAwardExperience()
        {
            var worker = roster.Soldiers[0];
            var cell = worker.Cell + Vector2Int.down;
            Assert.IsFalse(trenches.DesignateLadder(cell));
            Assert.IsFalse(trenches.DesignateLadder(new Vector2Int(-1, 0)));
            view.CompleteTrench(map.Data, cell);
            Assert.IsTrue(trenches.DesignateLadder(cell));
            trenches.Tick(0.01f);
            int xp = worker.Data.GetSkill(SoldierSkill.Strength).Experience;
            trenches.Tick(1f);
            Assert.IsTrue(trenches.Cancel(cell));
            Assert.IsTrue(worker.IsAvailable);
            Assert.IsTrue(map.Data.IsTrench(cell));
            Assert.IsFalse(map.Data.HasLadder(cell));
            Assert.IsFalse(map.Data.CanStep(worker.Cell, cell));
            Assert.AreEqual(xp, worker.Data.GetSkill(SoldierSkill.Strength).Experience);
        }

        [Test]
        public void LadderTravelDoesNotCountAsInstallationAndUnreachableWorkWaits()
        {
            var cell = new Vector2Int(50, 10);
            view.CompleteTrench(map.Data, cell);
            foreach (Vector2Int delta in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(cell + delta, false);
            trenches.DesignateLadder(cell);
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.AreEqual("Accesso bloccato o occupato", trenches.Jobs[0].Status);
            map.Data.SetWalkable(cell + Vector2Int.down, true);
            trenches.Tick(1f);
            var worker = trenches.Jobs[0].Worker;
            Assert.IsNotNull(worker);
            Assert.IsTrue(worker.IsMoving);
            trenches.Tick(10f);
            Assert.Zero(trenches.Jobs[0].Progress);
            worker.AdvanceMovement(100f);
            trenches.Tick(10f);
            Assert.IsTrue(map.Data.HasLadder(cell));
        }

        [Test]
        public void ExcavationWaitsForExistingRouteInsteadOfRemovingEdgesUnderMovingSoldier()
        {
            var cell = roster.Soldiers[0].Cell + Vector2Int.down;
            Assert.IsTrue(trenches.Designate(cell));
            trenches.Tick(0.01f);
            var worker = trenches.Jobs[0].Worker;
            var passer = roster.Soldiers[1];
            var route = new List<Vector2Int>(GridPathfinder.FindNearest(map.Data, new[] { passer.Cell }, cell).Cells);
            route.Add(cell + Vector2Int.left);
            passer.BeginMove(view, route);
            trenches.Tick(10f);
            Assert.AreEqual(1f, trenches.Jobs[0].Progress);
            Assert.AreEqual("Attesa: cella o percorso occupato", trenches.Jobs[0].Status);
            Assert.IsFalse(map.Data.IsTrench(cell));
            passer.AdvanceMovement(100f);
            trenches.Tick(0.1f);
            Assert.IsTrue(map.Data.IsTrench(cell));
            Assert.IsTrue(worker.IsAvailable);
        }
    }
}
