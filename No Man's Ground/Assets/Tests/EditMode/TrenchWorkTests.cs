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
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Digger);
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
            Assert.IsTrue(data.TryDesignateSpecializedRoom(RoomSpecialization.Hospital,
                new Vector2Int(3, 3), new Vector2Int(3, 3), out _));
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

        [Test]
        public void SpecializedRoomsRequireCompletedBuildingInteriorAndCannotOverlap()
        {
            var data = new MapData(12, 12);
            Assert.IsFalse(data.TryDesignateSpecializedRoom(RoomSpecialization.Command,
                new Vector2Int(3, 3), new Vector2Int(4, 4), out _));
            Assert.IsTrue(data.TryPlanBuilding(new Vector2Int(2, 2), new Vector2Int(8, 8), ItemType.Wood,
                out BuildingRoom building));
            Assert.IsFalse(data.TryDesignateSpecializedRoom(RoomSpecialization.Command,
                new Vector2Int(3, 3), new Vector2Int(4, 4), out _));
            var door = new Vector2Int(5, 2);
            Assert.IsTrue(data.TrySetBuildingDoor(door));
            for (int y = building.Bounds.yMin; y < building.Bounds.yMax; y++)
                for (int x = building.Bounds.xMin; x < building.Bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (building.IsPerimeter(cell))
                        Assert.IsTrue(data.CompleteBuildingTile(cell));
                }

            Assert.IsTrue(data.TryDesignateSpecializedRoom(RoomSpecialization.Command,
                new Vector2Int(3, 3), new Vector2Int(4, 4), out SpecializedRoom room));
            Assert.AreEqual(RoomSpecialization.Command, room.Specialization);
            Assert.AreSame(building, room.Building);
            Assert.IsFalse(data.TryDesignateSpecializedRoom(RoomSpecialization.Kitchen,
                new Vector2Int(4, 4), new Vector2Int(5, 5), out _));
            Assert.IsFalse(data.TryDesignateSpecializedRoom(RoomSpecialization.Hospital,
                new Vector2Int(2, 3), new Vector2Int(3, 4), out _));
            Assert.IsTrue(data.TryDesignateSpecializedRoom(RoomSpecialization.Kitchen,
                new Vector2Int(3, 3), new Vector2Int(4, 4), out SpecializedRoom reassigned));
            Assert.AreSame(room, reassigned);
            Assert.AreEqual(RoomSpecialization.Kitchen, room.Specialization);
            Assert.AreEqual(1, data.SpecializedRooms.Count);
        }

        [Test]
        public void StandaloneWallCanBeCompletedWithoutDoorOrRoof()
        {
            var data = new MapData(12, 12);
            var cell = new Vector2Int(5, 5);
            Assert.IsTrue(data.TryPlanWall(cell, ItemType.Wood, out BuildingRoom wall));
            Assert.IsTrue(wall.IsStandaloneWall);
            Assert.IsFalse(data.TrySetBuildingDoor(cell));
            Assert.IsTrue(data.CompleteBuildingTile(cell));
            Assert.IsTrue(wall.IsComplete);
            Assert.IsFalse(data.IsWalkable(cell));
            Assert.IsTrue(data.BlocksSight(cell));

            view.RenderBuildings(data);

            Tilemap roofs = view.transform.Find("Roofs").GetComponent<Tilemap>();
            Assert.IsFalse(roofs.HasTile(new Vector3Int(cell.x, cell.y, 0)));
        }

        [Test]
        public void FourBuiltWallLinesFormRoomAndDoorCompletesIt()
        {
            var data = new MapData(16, 16);
            var bounds = new RectInt(4, 4, 6, 6);
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (x != bounds.xMin && x != bounds.xMax - 1
                        && y != bounds.yMin && y != bounds.yMax - 1)
                        continue;
                    Assert.IsTrue(data.TryPlanWall(cell, ItemType.Wood, out _));
                    Assert.IsTrue(data.CompleteBuildingTile(cell));
                }
            Assert.IsTrue(data.TryMergeWallRectangle(new Vector2Int(bounds.xMin, bounds.yMin),
                out _, out _));

            BuildingRoom room = data.GetBuilding(new Vector2Int(bounds.xMin, bounds.yMin));
            Assert.IsFalse(room.IsStandaloneWall);
            Assert.IsTrue(room.IsWallAssembly);
            Assert.AreEqual(bounds, room.Bounds);
            Assert.IsFalse(room.IsComplete);
            var door = new Vector2Int(6, bounds.yMin);
            Assert.IsTrue(data.TrySetBuildingDoor(door));
            Assert.IsFalse(room.DoorBuilt);
            Assert.IsFalse(room.IsComplete);
            Assert.IsFalse(data.IsWalkable(door));
            Assert.IsTrue(data.CompleteBuildingTile(door));
            Assert.IsTrue(room.DoorBuilt);
            Assert.IsTrue(room.IsComplete);
            Assert.IsTrue(data.IsWalkable(door));
        }

        [TestCase(ItemType.Wood)]
        [TestCase(ItemType.BuildingStone)]
        public void FourWallLineJobsBuildPerimeterThenDoorJobCreatesRoof(ItemType material)
        {
            PrepareBuildingArea();
            EquipConstructionHammers();
            map.Data.StorageStockpile.Add(material, 9);
            var bottomLeft = new Vector2Int(50, 10);
            var topLeft = new Vector2Int(50, 12);
            var topRight = new Vector2Int(52, 12);
            var bottomRight = new Vector2Int(52, 10);

            Assert.IsTrue(trenches.DesignateWallLine(bottomLeft, topLeft, material));
            Assert.IsTrue(trenches.DesignateWallLine(topLeft, topRight, material));
            Assert.IsTrue(trenches.DesignateWallLine(topRight, bottomRight, material));
            Assert.IsTrue(trenches.DesignateWallLine(bottomRight, bottomLeft, material));
            Assert.AreEqual(8, trenches.Jobs.Count);
            BuildingRoom room = map.Data.GetBuilding(bottomLeft);
            Assert.IsTrue(room.IsWallAssembly);
            Assert.IsFalse(room.IsComplete);
            var door = new Vector2Int(51, 12);
            Assert.IsFalse(trenches.DesignateDoor(door));
            Assert.IsNull(room.Door);

            Tilemap roofs = view.transform.Find("Roofs").GetComponent<Tilemap>();
            for (int iteration = 0; iteration < 100 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(8, room.CompletedTiles);
            Assert.IsFalse(room.IsComplete);
            Assert.IsFalse(roofs.HasTile(new Vector3Int(51, 11, 0)));

            Assert.IsTrue(room.IsBuilt(door));
            Assert.IsTrue(trenches.DesignateDoor(door));
            Assert.AreEqual(1, trenches.Jobs.Count);
            Assert.AreEqual(TrenchJobKind.BuildDoor, trenches.Jobs[0].Kind);
            Assert.IsFalse(room.DoorBuilt);
            Assert.IsFalse(map.Data.IsWalkable(door));

            for (int iteration = 0; iteration < 20 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(room.DoorBuilt);
            Assert.IsTrue(room.IsComplete);
            Assert.IsTrue(map.Data.IsWalkable(door));
            Assert.IsTrue(roofs.HasTile(new Vector3Int(51, 11, 0)));
            Assert.Zero(map.Data.StorageStockpile.Count(material));
        }

        [Test]
        public void SingleWallJobConsumesWoodAndBuildsWithoutDoor()
        {
            PrepareBuildingArea();
            EquipConstructionHammers();
            map.Data.StorageStockpile.Add(ItemType.Wood, 1);
            var cell = new Vector2Int(51, 11);

            Assert.IsTrue(trenches.DesignateWall(cell, ItemType.Wood));
            Assert.AreEqual(1, trenches.Jobs.Count);
            Assert.AreEqual(TrenchJobKind.BuildWall, trenches.Jobs[0].Kind);
            Assert.IsTrue(map.Data.GetBuilding(cell).IsStandaloneWall);
            Assert.IsNull(map.Data.GetBuilding(cell).Door);

            for (int iteration = 0; iteration < 20 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.GetBuilding(cell).IsComplete);
            Assert.IsFalse(map.Data.IsWalkable(cell));
            Assert.Zero(map.Data.StorageStockpile.Count(ItemType.Wood));
        }

        [Test]
        public void WoodWallLineCanBeDesignatedOnRememberedTiles()
        {
            PrepareBuildingArea();
            var from = new Vector2Int(50, 10);
            var to = from + Vector2Int.up;
            map.Data.Fog.Enable();
            map.Data.Fog.Update(map.Data,
                new[] { new FogObserver(from, Vector2.up, 360f) }, 4);
            map.Data.Fog.Update(map.Data,
                new[] { new FogObserver(new Vector2Int(10, 10), Vector2.up, 360f) }, 2);
            Assert.IsTrue(map.Data.Fog.IsExplored(from));
            Assert.IsFalse(map.Data.Fog.IsVisible(from));
            Assert.IsTrue(map.Data.Fog.IsExplored(to));
            Assert.IsFalse(map.Data.Fog.IsVisible(to));

            Assert.IsTrue(trenches.DesignateWallLine(from, to, ItemType.Wood));

            Assert.AreEqual(2, trenches.Jobs.Count);
            Assert.IsTrue(map.Data.GetBuilding(from).IsStandaloneWall);
            Assert.IsTrue(map.Data.GetBuilding(to).IsStandaloneWall);
        }

        [Test]
        public void WallCellItemIsHauledToStorageBeforeConstruction()
        {
            PrepareBuildingArea();
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Hauler);
            SoldierView builder = roster.Soldiers[7];
            TaskTestSetup.Assign(builder, SoldierTask.Builder);
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.ConstructionHammer, 1));
            builder.Data.Inventory.Add(ItemType.ConstructionHammer, 1);
            map.Data.StorageStockpile.Add(ItemType.Wood, 1);
            var cell = new Vector2Int(51, 11);
            map.Data.AddGroundItem(cell, ItemType.BuildingStone);
            view.Render(map.Data);

            Assert.IsTrue(trenches.DesignateWallLine(cell, cell, ItemType.Wood));
            Assert.AreEqual(2, trenches.Jobs.Count);
            TrenchJob clearance = trenches.Jobs[0];
            TrenchJob wall = trenches.Jobs[1];
            Assert.IsTrue(clearance.IsBuildClearance);
            Assert.AreEqual(TrenchJobKind.CollectItem, clearance.Kind);
            Assert.AreEqual(TrenchJobKind.BuildWall, wall.Kind);

            trenches.Tick(0.01f);
            Assert.IsNotNull(clearance.Worker);
            Assert.IsNull(wall.Worker);
            Assert.IsTrue(map.Data.TryGetGroundItem(cell, out _));
            for (int iteration = 0; iteration < 40 && trenches.Jobs.Count > 1; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(0.1f);
            }

            Assert.AreEqual(1, trenches.Jobs.Count);
            Assert.IsFalse(map.Data.TryGetGroundItem(cell, out _));
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.BuildingStone));
            trenches.Tick(0.5f);
            Assert.IsNotNull(wall.Worker);
            for (int iteration = 0; iteration < 20 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.GetBuilding(cell).IsComplete);
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.BuildingStone));
            Assert.Zero(map.Data.StorageStockpile.Count(ItemType.Wood));
        }

        [Test]
        public void WallClearanceHaulsGroundItemBeforeBuilderStarts()
        {
            PrepareBuildingArea();
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Hauler);
            SoldierView builder = roster.Soldiers[7];
            TaskTestSetup.Assign(builder, SoldierTask.Builder);
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.ConstructionHammer, 1));
            builder.Data.Inventory.Add(ItemType.ConstructionHammer, 1);
            map.Data.StorageStockpile.Add(ItemType.Wood, 1);
            var cell = new Vector2Int(51, 11);
            map.Data.AddGroundItem(cell, ItemType.BuildingStone);
            view.Render(map.Data);

            Assert.IsTrue(trenches.DesignateWallLine(cell, cell, ItemType.Wood));
            Assert.AreEqual(2, trenches.Jobs.Count);
            TrenchJob clearance = trenches.Jobs[0];
            TrenchJob wall = trenches.Jobs[1];
            Assert.IsTrue(clearance.IsBuildClearance);
            Assert.AreEqual(TrenchJobKind.CollectItem, clearance.Kind);
            Assert.AreEqual(TrenchJobKind.BuildWall, wall.Kind);

            trenches.Tick(0.01f);
            Assert.IsNotNull(clearance.Worker);
            Assert.IsNull(wall.Worker);
            Assert.IsTrue(map.Data.TryGetGroundItem(cell, out _));

            for (int iteration = 0; iteration < 40 && trenches.Jobs.Count > 1; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(0.1f);
            }

            Assert.AreEqual(1, trenches.Jobs.Count);
            Assert.IsFalse(map.Data.TryGetGroundItem(cell, out _));
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.BuildingStone));
            trenches.Tick(0.5f);
            Assert.IsNotNull(wall.Worker);
            for (int iteration = 0; iteration < 20 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.GetBuilding(cell).IsComplete);
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.BuildingStone));
            Assert.Zero(map.Data.StorageStockpile.Count(ItemType.Wood));
        }

        [Test]
        public void FourWallLinesFormRoomAndDoorOnBuiltWallCreatesRoof()
        {
            PrepareBuildingArea();
            EquipConstructionHammers();
            map.Data.StorageStockpile.Add(ItemType.Wood, 9);
            var bottomLeft = new Vector2Int(50, 10);
            var topLeft = new Vector2Int(50, 12);
            var topRight = new Vector2Int(52, 12);
            var bottomRight = new Vector2Int(52, 10);

            Assert.IsTrue(trenches.DesignateWallLine(bottomLeft, topLeft, ItemType.Wood));
            Assert.IsTrue(trenches.DesignateWallLine(topLeft, topRight, ItemType.Wood));
            Assert.IsTrue(trenches.DesignateWallLine(topRight, bottomRight, ItemType.Wood));
            Assert.IsTrue(trenches.DesignateWallLine(bottomRight, bottomLeft, ItemType.Wood));
            BuildingRoom room = map.Data.GetBuilding(bottomLeft);
            Assert.IsNotNull(room);
            Assert.IsTrue(room.IsWallAssembly);
            Assert.AreEqual(8, trenches.Jobs.Count);

            for (int iteration = 0; iteration < 100 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(8, room.CompletedTiles);
            Assert.IsFalse(room.IsComplete);
            Assert.IsNull(room.Door);
            Tilemap roofs = view.transform.Find("Roofs").GetComponent<Tilemap>();
            Assert.IsFalse(roofs.HasTile(new Vector3Int(51, 11, 0)));

            var door = new Vector2Int(51, 12);
            Assert.IsTrue(room.IsBuilt(door));
            Assert.IsTrue(trenches.DesignateDoor(door));
            Assert.AreEqual(1, trenches.Jobs.Count);
            Assert.AreEqual(TrenchJobKind.BuildDoor, trenches.Jobs[0].Kind);
            Assert.AreEqual(8, room.CompletedTiles);
            Assert.IsFalse(room.DoorBuilt);
            Assert.IsFalse(map.Data.IsWalkable(door));

            for (int iteration = 0; iteration < 20 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }

            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(room.DoorBuilt);
            Assert.IsTrue(room.IsComplete);
            Assert.IsTrue(map.Data.IsWalkable(door));
            Assert.IsTrue(roofs.HasTile(new Vector3Int(51, 11, 0)));
            Assert.Zero(map.Data.StorageStockpile.Count(ItemType.Wood));
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
            Assert.IsTrue(data.TryDesignateSpecializedRoom(RoomSpecialization.Command,
                new Vector2Int(3, 3), new Vector2Int(3, 3), out _));
            view.RenderBuildings(data);
            Tilemap specializedRooms = view.transform.Find("Specialized Rooms").GetComponent<Tilemap>();
            Assert.IsTrue(specializedRooms.HasTile(new Vector3Int(3, 3, 0)));
            Assert.IsFalse(specializedRooms.HasTile(new Vector3Int(7, 3, 0)));
            TextMesh roomLabel = view.transform.Find("Specialized Room Labels").GetComponentInChildren<TextMesh>();
            Assert.AreEqual("COMANDO", roomLabel.text);
            Assert.Greater(roomLabel.GetComponent<MeshRenderer>().sortingOrder,
                roofs.GetComponent<TilemapRenderer>().sortingOrder);
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

        [TestCase(false)]
        [TestCase(true)]
        public void BuildingDesignationEvacuatesSoldiersFromInteriorAndPerimeter(bool onPerimeter)
        {
            Vector2Int from = PrepareBuildingAreaAroundSoldier(onPerimeter, out SoldierView soldier);
            Vector2Int to = from + new Vector2Int(2, 2);
            var bounds = new RectInt(from.x, from.y, 3, 3);
            Assert.IsTrue(bounds.Contains(soldier.Cell));
            Assert.AreEqual(onPerimeter, soldier.Cell.x == bounds.xMin || soldier.Cell.x == bounds.xMax - 1
                || soldier.Cell.y == bounds.yMin || soldier.Cell.y == bounds.yMax - 1);

            EquipConstructionHammers();
            Vector2Int bottomLeft = from;
            Vector2Int topLeft = new Vector2Int(from.x, to.y);
            Vector2Int topRight = to;
            Vector2Int bottomRight = new Vector2Int(to.x, from.y);
            Assert.IsTrue(trenches.DesignateWallLine(bottomLeft, topLeft, ItemType.Wood));
            Assert.IsTrue(trenches.DesignateWallLine(topLeft, topRight, ItemType.Wood));
            Assert.IsTrue(trenches.DesignateWallLine(topRight, bottomRight, ItemType.Wood));
            Assert.IsTrue(trenches.DesignateWallLine(bottomRight, bottomLeft, ItemType.Wood));
            Assert.IsTrue(map.Data.GetBuilding(bottomLeft).IsWallAssembly);
            trenches.Tick(0.01f);

            Assert.IsTrue(soldier.IsMoving);
            for (int i = 0; i < 20 && soldier.IsEvacuatingBuilding; i++)
            {
                soldier.AdvanceMovement(1000f);
                trenches.Tick(0.01f);
            }
            Assert.IsFalse(bounds.Contains(soldier.Cell));
            Assert.IsFalse(soldier.IsEvacuatingBuilding);
        }

        private Vector2Int PrepareBuildingAreaAroundSoldier(bool onPerimeter, out SoldierView selected)
        {
            foreach (SoldierView soldier in roster.Soldiers)
                for (int offsetY = 0; offsetY < 3; offsetY++)
                    for (int offsetX = 0; offsetX < 3; offsetX++)
                    {
                        bool perimeter = offsetX == 0 || offsetX == 2 || offsetY == 0 || offsetY == 2;
                        if (perimeter != onPerimeter)
                            continue;
                        Vector2Int from = soldier.Cell - new Vector2Int(offsetX, offsetY);
                        var bounds = new RectInt(from.x, from.y, 3, 3);
                        bool valid = true;
                        for (int y = bounds.yMin; y < bounds.yMax && valid; y++)
                            for (int x = bounds.xMin; x < bounds.xMax; x++)
                            {
                                var cell = new Vector2Int(x, y);
                                if (!map.Data.Contains(cell) || map.Data.IsStorageCell(cell)
                                    || map.Data.IsPantryCell(cell) || map.Data.GetBuilding(cell) != null
                                    || map.Data.TryGetZone(cell, out _))
                                {
                                    valid = false;
                                    break;
                                }
                            }
                        if (!valid)
                            continue;
                        for (int y = bounds.yMin; y < bounds.yMax; y++)
                            for (int x = bounds.xMin; x < bounds.xMax; x++)
                            {
                                var cell = new Vector2Int(x, y);
                                map.Data.TryTakeGroundItem(cell, out _);
                                map.Data.SetTree(cell, false);
                                map.Data[x, y] = TerrainKind.Dirt;
                                map.Data.SetWalkable(cell, true);
                            }
                        view.Render(map.Data);
                        selected = soldier;
                        return from;
                    }
            Assert.Fail("No valid building area containing a soldier was found.");
            selected = null;
            return default;
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
            EquipConstructionHammers();
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
            EquipConstructionHammers();
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
            var diagnostics = new List<string>();
            foreach (TrenchJob job in trenches.Jobs)
                diagnostics.Add($"{job.Cell}: {job.Status}, worker={job.Worker?.Cell}, pickup={job.PickingTool}, source={job.GroundPickupCell}");
            foreach (SoldierView soldier in roster.Soldiers)
                diagnostics.Add($"{soldier.Cell}: hammer={soldier.Data.Inventory.Count(ItemType.ConstructionHammer)}, material={soldier.Data.Inventory.Count(material)}");
            Assert.Zero(trenches.Jobs.Count, string.Join("\n", diagnostics));
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
            EquipConstructionHammers();
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

        [Test]
        public void TreeCutAndCollectionCanBeDesignatedOnRememberedTerrain()
        {
            var treeCell = new Vector2Int(50, 10);
            var itemCell = new Vector2Int(51, 10);
            map.Data.SetTree(treeCell, true);
            map.Data.AddGroundItem(itemCell, ItemType.Wood);
            map.Data.Fog.Enable();
            map.Data.Fog.Update(map.Data, new[] { treeCell }, 4);
            map.Data.Fog.Update(map.Data, new Vector2Int[0], 4);
            Assert.IsTrue(map.Data.Fog.IsExplored(treeCell));
            Assert.IsFalse(map.Data.Fog.IsVisible(treeCell));
            Assert.IsTrue(map.Data.Fog.IsExplored(itemCell));
            Assert.IsFalse(map.Data.Fog.IsVisible(itemCell));

            Assert.AreEqual(1, trenches.DesignateTree(treeCell, treeCell));
            Assert.IsTrue(trenches.DesignateCollection(itemCell));
            Assert.AreEqual(2, trenches.Jobs.Count);
        }

        private void EquipConstructionHammers()
        {
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Builder);
            for (int index = 0; index < 2; index++)
            {
                Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.ConstructionHammer, 1));
                roster.Soldiers[index].Data.Inventory.Add(ItemType.ConstructionHammer, 1);
            }
        }

        private void PrepareBuildingArea()
        {
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Builder);
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
            Assert.AreEqual(5, depotDisplay.GetComponentsInChildren<SpriteRenderer>().Length);
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
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Woodcutter);
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
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Woodcutter);
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
        public void TwoWoodcuttersCompleteAdjacentTreesWithoutBlockingEachOther()
        {
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Woodcutter);
            for (int index = 0; index < 2; index++)
            {
                Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.Axe, 1));
                roster.Soldiers[index].Data.Inventory.Add(ItemType.Axe, 1);
            }
            var firstTree = new Vector2Int(50, 10);
            var secondTree = firstTree + Vector2Int.right;
            foreach (Vector2Int cell in new[] { firstTree, secondTree })
            {
                map.Data.TryTakeGroundItem(cell, out _);
                map.Data.SetTree(cell, false);
                map.Data.SetTree(cell, true);
            }
            view.Render(map.Data);
            Assert.IsTrue(trenches.DesignateTree(firstTree));
            Assert.IsTrue(trenches.DesignateTree(secondTree));
            trenches.Tick(0.01f);

            Assert.AreEqual(2, trenches.Jobs.Count);
            foreach (TrenchJob job in trenches.Jobs)
            {
                Assert.IsNotNull(job.Worker);
                Assert.AreNotEqual(firstTree, job.Worker.Destination);
                Assert.AreNotEqual(secondTree, job.Worker.Destination);
            }
            for (int iteration = 0; iteration < 40 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(1f);
            }

            Assert.Zero(trenches.Jobs.Count);
            foreach (Vector2Int cell in new[] { firstTree, secondTree })
            {
                Assert.IsFalse(map.Data.HasTree(cell));
                Assert.IsTrue(map.Data.TryGetGroundItem(cell, out ItemType item));
                Assert.AreEqual(ItemType.Wood, item);
            }
        }

        [Test]
        public void TreeCutCompletesWhileAnotherSoldiersRouteCrossesTheTreeCell()
        {
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Woodcutter);
            SoldierView cutter = roster.Soldiers[0];
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.Axe, 1));
            cutter.Data.Inventory.Add(ItemType.Axe, 1);
            var treeCell = new Vector2Int(50, 10);
            map.Data.SetTree(treeCell, true);
            Assert.IsTrue(trenches.DesignateTree(treeCell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.AreSame(cutter, job.Worker);

            SoldierView passer = roster.Soldiers[7];
            passer.BeginMove(view, new List<Vector2Int>
                { passer.Cell, treeCell, treeCell + Vector2Int.up });
            Assert.IsTrue(passer.IsMoving);
            Assert.AreEqual(treeCell, passer.CurrentPath[passer.NextWaypoint]);
            trenches.Tick(10f);

            Assert.Zero(trenches.Jobs.Count);
            Assert.IsFalse(map.Data.HasTree(treeCell));
            Assert.IsTrue(map.Data.TryGetGroundItem(treeCell, out ItemType drop));
            Assert.AreEqual(ItemType.Wood, drop);
        }

        [TestCase(TerrainKind.Rock, TerrainKind.Dirt, "Dirt")]
        [TestCase(TerrainKind.Mountain, TerrainKind.Tunnel, "Galleria")]
        public void MineJobCollectsPickaxeWorksTenSecondsAndDropsBuildingStoneOnExpectedTerrain(
            TerrainKind original, TerrainKind expected, string tileName)
        {
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Miner);
            var cell = new Vector2Int(50, 10);
            map.Data[cell.x, cell.y] = original;
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
            Assert.AreEqual(original, map.Data[cell.x, cell.y]);
            Assert.IsFalse(map.Data.TryGetGroundItem(cell, out _));
            trenches.Tick(0.02f);
            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(expected, map.Data[cell.x, cell.y]);
            Assert.IsTrue(map.Data.IsWalkable(cell));
            Assert.AreEqual(original == TerrainKind.Mountain, map.Data.IsIndoors(cell));
            Assert.IsTrue(map.Data.TryGetGroundItem(cell, out ItemType drop));
            Assert.AreEqual(ItemType.BuildingStone, drop);
            Assert.AreEqual(tileName, view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            Tilemap nature = view.transform.Find("Trees").GetComponent<Tilemap>();
            Assert.AreEqual("Pietra da costruzione", nature.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            view.Render(map.Data);
            Assert.AreEqual(tileName, view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
        }

        [Test]
        public void MountainMiningOpensAccessToQueuedInteriorTunnelJobs()
        {
            PrepareBuildingArea();
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Miner);
            for (int y = 10; y <= 12; y++)
                for (int x = 50; x <= 52; x++)
                    map.Data[x, y] = TerrainKind.Mountain;
            view.Render(map.Data);
            var entrance = new Vector2Int(51, 10);
            var interior = new Vector2Int(51, 11);
            Assert.IsTrue(trenches.DesignateMine(interior));
            Assert.IsTrue(trenches.DesignateMine(entrance));
            trenches.Tick(0.01f);
            Assert.AreEqual(ItemType.Pickaxe, trenches.Jobs[0].PickingTool);
            Assert.Zero(trenches.Jobs[0].Progress);
            Assert.AreEqual(TerrainKind.Mountain, map.Data[interior.x, interior.y]);
            for (int iteration = 0; iteration < 60 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.IsTunnel(entrance));
            Assert.IsTrue(map.Data.IsTunnel(interior));
            Assert.IsTrue(map.Data.IsIndoors(interior));
            Assert.IsFalse(map.Data.IsWalkable(new Vector2Int(50, 11)));
            GridPath path = GridPathfinder.FindNearest(map.Data, new[] { new Vector2Int(51, 9) }, interior);
            Assert.IsNotNull(path);
            CollectionAssert.Contains(path.Cells, entrance);
            Assert.IsTrue(map.Data.TryGetGroundItem(entrance, out ItemType firstDrop));
            Assert.AreEqual(ItemType.BuildingStone, firstDrop);
            Assert.IsTrue(map.Data.TryGetGroundItem(interior, out ItemType secondDrop));
            Assert.AreEqual(ItemType.BuildingStone, secondDrop);
            Assert.IsFalse(trenches.Designate(interior));
        }

        [TestCase(1280, 720)]
        [TestCase(600, 1000)]
        public void MountainTextureIsDarkerAndTunnelWallsOpenWhenMiningContinues(int width, int height)
        {
            PrepareBuildingArea();
            for (int y = 10; y <= 12; y++)
                for (int x = 50; x <= 52; x++)
                    map.Data[x, y] = TerrainKind.Mountain;
            map.Data[49, 10] = TerrainKind.Rock;
            view.Render(map.Data);
            var entrance = new Vector2Int(51, 10);
            var interior = new Vector2Int(51, 11);
            var entrancePosition = new Vector3Int(51, 10, 0);
            Tile mountain = view.Tilemap.GetTile<Tile>(entrancePosition);
            Assert.AreEqual("Montagna", mountain.name);
            view.CompleteRockMine(map.Data, entrance);
            Tile tunnelBefore = view.Tilemap.GetTile<Tile>(entrancePosition);
            Assert.AreEqual("Galleria", tunnelBefore.name);
            Assert.Less(tunnelBefore.sprite.texture.GetPixel(8, 15).grayscale,
                tunnelBefore.sprite.texture.GetPixel(8, 8).grayscale);
            view.CompleteRockMine(map.Data, interior);
            Tile tunnelAfter = view.Tilemap.GetTile<Tile>(entrancePosition);
            Assert.AreNotSame(tunnelBefore, tunnelAfter);
            Assert.Greater(tunnelAfter.sprite.texture.GetPixel(8, 15).grayscale,
                tunnelBefore.sprite.texture.GetPixel(8, 15).grayscale);
            map.Data.TryTakeGroundItem(entrance, out _);
            map.Data.TryTakeGroundItem(interior, out _);
            view.RefreshGroundItems(map.Data);
            Camera camera = Camera.main;
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Vector3 previousPosition = camera.transform.position;
            float previousSize = camera.orthographicSize;
            try
            {
                camera.targetTexture = target;
                camera.orthographicSize = 4.5f;
                camera.transform.position = new Vector3(51.5f, 11f, -10f);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Color Sample(Vector3Int cell)
                {
                    Vector3 screen = camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(cell));
                    return pixels.GetPixel(Mathf.FloorToInt(screen.x), Mathf.FloorToInt(screen.y));
                }
                Assert.Less(Sample(new Vector3Int(50, 11, 0)).grayscale, Sample(new Vector3Int(49, 10, 0)).grayscale);
                Assert.Greater(Sample(new Vector3Int(51, 11, 0)).grayscale, Sample(new Vector3Int(50, 11, 0)).grayscale);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"NoMansGround-MountainTunnel-{width}x{height}.png"), pixels.EncodeToPNG());
                Color beforeDoor = Sample(entrancePosition);
                Assert.IsTrue(map.Data.TryPlanTunnelDoor(entrance));
                Assert.IsTrue(map.Data.CompleteTunnelDoor(entrance));
                view.RenderBuildings(map.Data);
                Assert.IsTrue(map.Data.IsClosedShelter(interior));
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Assert.Greater(((Vector4)beforeDoor - (Vector4)Sample(entrancePosition)).sqrMagnitude, 0.001f);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"NoMansGround-MountainRefuge-{width}x{height}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = previousTarget;
                camera.transform.position = previousPosition;
                camera.orthographicSize = previousSize;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
            view.Render(map.Data);
            Assert.AreSame(tunnelAfter, view.Tilemap.GetTile(entrancePosition));
        }

        [Test]
        public void BuilderFetchesHammerBeforeMaterialAndKeepsItAfterFinishing()
        {
            PrepareBuildingArea();
            var door = new Vector2Int(51, 10);
            map.Data[50, 10] = TerrainKind.Mountain;
            map.Data[52, 10] = TerrainKind.Mountain;
            map.Data[51, 10] = TerrainKind.Tunnel;
            map.Data[51, 11] = TerrainKind.Tunnel;
            map.Data.StorageStockpile.Add(ItemType.Wood, 1);
            view.Render(map.Data);
            Assert.IsTrue(trenches.DesignateDoor(door));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            SoldierView worker = job.Worker;
            Assert.IsNotNull(worker);
            Assert.AreEqual(ItemType.ConstructionHammer, job.PickingTool);
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.Wood));
            Assert.Zero(job.Progress);
            worker.AdvanceMovement(1000f);
            trenches.Tick(0.01f);
            Assert.AreEqual(1, worker.Data.Inventory.Count(ItemType.ConstructionHammer));
            Assert.AreEqual(ItemType.Wood, job.PickingTool);
            Assert.AreSame(worker, job.Worker);
            worker.AdvanceMovement(1000f);
            trenches.Tick(0.01f);
            Assert.AreEqual(1, worker.Data.Inventory.Count(ItemType.Wood));
            for (int iteration = 0; iteration < 30 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.GetTunnelDoor(door).IsBuilt);
            Assert.AreEqual(1, worker.Data.Inventory.Count(ItemType.ConstructionHammer));
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.ConstructionHammer));
            Assert.Zero(worker.Data.Inventory.Count(ItemType.Wood));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConstructionAndLaddersWaitForHammerAndRetryWhenOneBecomesAvailable(bool ladder)
        {
            PrepareBuildingArea();
            var cell = new Vector2Int(51, 10);
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.ConstructionHammer, 2));
            if (ladder)
            {
                view.CompleteTrench(map.Data, cell);
                Assert.IsTrue(trenches.DesignateLadder(cell));
            }
            else
            {
                map.Data[50, 10] = TerrainKind.Mountain;
                map.Data[52, 10] = TerrainKind.Mountain;
                map.Data[51, 10] = TerrainKind.Tunnel;
                map.Data[51, 11] = TerrainKind.Tunnel;
                Assert.IsTrue(trenches.DesignateDoor(cell));
            }
            map.Data.StorageStockpile.Add(ItemType.Wood, 1);
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.Zero(trenches.Jobs[0].Progress);
            StringAssert.Contains("martello", trenches.Jobs[0].Status);
            Assert.AreEqual(1, map.Data.StorageStockpile.Count(ItemType.Wood));
            map.Data.StorageStockpile.Add(ItemType.ConstructionHammer, 1);
            for (int iteration = 0; iteration < 40 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }
            Assert.Zero(trenches.Jobs.Count);
            if (ladder) Assert.IsTrue(map.Data.HasLadder(cell));
            else Assert.IsTrue(map.Data.GetTunnelDoor(cell).IsBuilt);
            int hammers = map.Data.StorageStockpile.Count(ItemType.ConstructionHammer);
            foreach (SoldierView soldier in roster.Soldiers)
                hammers += soldier.Data.Inventory.Count(ItemType.ConstructionHammer);
            Assert.AreEqual(1, hammers);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TunnelDoorUsesWoodFromGroundOrStorageAndClosesShelterWithoutBlockingMovement(bool fromGround)
        {
            PrepareBuildingArea();
            for (int y = 10; y <= 12; y++)
                for (int x = 50; x <= 52; x++)
                    map.Data[x, y] = TerrainKind.Mountain;
            var door = new Vector2Int(51, 10);
            var inside = new Vector2Int(51, 11);
            map.Data[door.x, door.y] = TerrainKind.Tunnel;
            map.Data[inside.x, inside.y] = TerrainKind.Tunnel;
            var source = new Vector2Int(45, 10);
            if (fromGround) map.Data.AddGroundItem(source, ItemType.Wood);
            else map.Data.StorageStockpile.Add(ItemType.Wood, 1);
            view.Render(map.Data);
            Assert.IsFalse(map.Data.IsClosedShelter(inside));
            Assert.IsTrue(trenches.DesignateDoor(door));
            Assert.IsFalse(trenches.DesignateDoor(door));
            Assert.AreEqual(TrenchJobKind.BuildDoor, trenches.Jobs[0].Kind);
            Assert.AreEqual(ItemType.Wood, trenches.Jobs[0].Material);
            Assert.IsFalse(map.Data.GetTunnelDoor(door).IsBuilt);
            for (int iteration = 0; iteration < 40 && trenches.Jobs.Count > 0; iteration++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(1000f);
                trenches.Tick(10f);
            }
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(map.Data.GetTunnelDoor(door).IsBuilt);
            Assert.IsTrue(map.Data.IsClosedShelter(inside));
            Assert.AreEqual(1, map.Data.ClosedTunnelShelterCount);
            Assert.Zero(map.Data.OpenTunnelCount);
            Assert.IsTrue(map.Data.CanStep(new Vector2Int(51, 9), door));
            Assert.IsTrue(map.Data.CanStep(door, inside));
            Assert.IsNotNull(GridPathfinder.FindNearest(map.Data, new[] { new Vector2Int(51, 9) }, inside));
            Assert.Zero(map.Data.StorageStockpile.Count(ItemType.Wood));
            Assert.IsFalse(map.Data.TryGetGroundItem(source, out _));
            foreach (SoldierView soldier in roster.Soldiers)
                Assert.Zero(soldier.Data.Inventory.Count(ItemType.Wood));
            Tilemap buildings = view.transform.Find("Buildings").GetComponent<Tilemap>();
            Assert.AreEqual("WoodDoor", buildings.GetTile(new Vector3Int(door.x, door.y, 0)).name);
            map.Regenerate();
            Assert.Zero(map.Data.TunnelDoors.Count);
            Assert.Zero(map.Data.ClosedTunnelShelterCount);
            Assert.IsFalse(buildings.HasTile(new Vector3Int(door.x, door.y, 0)));
        }

        [Test]
        public void TunnelDoorWaitsForWoodAndCancellationRemovesOnlyThePlan()
        {
            PrepareBuildingArea();
            EquipConstructionHammers();
            var door = new Vector2Int(51, 10);
            map.Data[50, 10] = TerrainKind.Mountain;
            map.Data[52, 10] = TerrainKind.Mountain;
            map.Data[51, 10] = TerrainKind.Tunnel;
            map.Data[51, 11] = TerrainKind.Tunnel;
            view.Render(map.Data);
            Assert.IsTrue(trenches.DesignateDoor(door));
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.Zero(trenches.Jobs[0].Progress);
            Assert.IsTrue(trenches.Cancel(door));
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsNull(map.Data.GetTunnelDoor(door));
            Assert.IsTrue(map.Data.IsTunnel(door));
            Assert.IsTrue(map.Data.IsWalkable(door));
        }

        [Test]
        public void CollectJobPicksUpGroundResourceAndDeliversItToTheDepot()
        {
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Hauler);
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
        public void CollectionWaitsWhenNoWorkerCanCarryTheResource()
        {
            TaskTestSetup.AssignWorkers(roster, SoldierTask.Hauler);
            foreach (SoldierView soldier in roster.Soldiers)
                soldier.Data.Inventory.Add(ItemType.BuildingStone, 20);
            var cell = new Vector2Int(50, 10);
            map.Data.AddGroundItem(cell, ItemType.Wood);
            view.Render(map.Data);

            Assert.IsTrue(trenches.DesignateCollection(cell));
            trenches.Tick(0.01f);

            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.AreEqual("Carico insufficiente", trenches.Jobs[0].Status);
            Assert.IsTrue(map.Data.TryGetGroundItem(cell, out ItemType item));
            Assert.AreEqual(ItemType.Wood, item);
            foreach (SoldierView soldier in roster.Soldiers)
                Assert.Zero(soldier.Data.Inventory.Count(ItemType.Wood));
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
            int shovelStock = map.Data.StorageStockpile.Count(ItemType.Shovel);
            Assert.AreEqual(1, worker.Data.Inventory.Count(ItemType.Shovel));
            Assert.IsTrue(worker.IsReturningTaskTool);
            CompleteToolReturn(worker, ItemType.Shovel, shovelStock);
            Assert.IsFalse(worker.CanTakeEngineeringWork);
            trenches.Designate(worker.Cell + Vector2Int.left);
            trenches.Tick(1f);
            Assert.AreNotSame(worker, trenches.Jobs[0].Worker);
            worker.RequestDuty(SoldierDuty.Work);
            Assert.IsTrue(worker.CanTakeEngineeringWork);
        }

        [Test]
        public void SwitchingIdleDiggerToGuardReturnsShovelToStorage()
        {
            SoldierView worker = roster.Soldiers[0];
            int shovelStock = map.Data.StorageStockpile.Count(ItemType.Shovel);
            Assert.IsTrue(worker.RequestDuty(SoldierDuty.Guard));

            Assert.AreEqual(SoldierDuty.Guard, worker.Duty);
            Assert.IsTrue(worker.IsReturningTaskTool);
            Assert.AreEqual(1, worker.Data.Inventory.Count(ItemType.Shovel));
            CompleteToolReturn(worker, ItemType.Shovel, shovelStock);
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
            EquipConstructionHammers();
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
            EquipConstructionHammers();
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
            EquipConstructionHammers();
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

        private void CompleteToolReturn(SoldierView soldier, ItemType tool, int initialStock)
        {
            trenches.Tick(0.01f);
            Assert.IsTrue(soldier.IsMoving);
            Assert.AreEqual(1, soldier.Data.Inventory.Count(tool));
            for (int i = 0; i < 100 && soldier.IsReturningTaskTool; i++)
            {
                soldier.AdvanceMovement(1000f);
                trenches.Tick(0.01f);
            }
            Assert.IsFalse(soldier.IsReturningTaskTool);
            Assert.AreEqual(SoldierDuty.Guard, soldier.Duty);
            Assert.Zero(soldier.Data.Inventory.Count(tool));
            Assert.AreEqual(initialStock + 1, map.Data.StorageStockpile.Count(tool));
        }
    }
}
