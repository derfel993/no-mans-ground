using System;
using System.Collections.Generic;
using System.Reflection;
using NoMansGround.Editor;
using NoMansGround.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace NoMansGround.Tests
{
    public sealed class MapPrototypeTests
    {
        [Test]
        public void SameSeedProducesSameCellsWithoutChangingUnityRandom()
        {
            UnityEngine.Random.State state = UnityEngine.Random.state;
            var settings = new MapGenerationSettings();
            MapData first = MapGenerator.Generate(settings);
            MapData second = MapGenerator.Generate(settings);
            Assert.AreEqual(state, UnityEngine.Random.state);
            Assert.AreEqual(128, first.Width);
            Assert.AreEqual(128, first.Height);
            for (int y = 0; y < first.Height; y++)
                for (int x = 0; x < first.Width; x++)
                {
                    Assert.AreEqual(first[x, y], second[x, y], $"Cell {x}, {y}");
                    Assert.AreEqual(first.HasTree(new Vector2Int(x, y)), second.HasTree(new Vector2Int(x, y)),
                        $"Tree {x}, {y}");
                }
        }

        [Test]
        public void DifferentSeedChangesTerrainAndAllTerrainTypesExist()
        {
            var settings = new MapGenerationSettings();
            MapData first = MapGenerator.Generate(settings);
            settings.seed++;
            MapData second = MapGenerator.Generate(settings);
            int differences = 0;
            int treeEligibleCells = 0;
            int treeCells = 0;
            Assert.IsTrue(first.HasStorageArea);
            Assert.IsTrue(first.HasPantryArea);
            var counts = new int[Enum.GetValues(typeof(TerrainKind)).Length];
            for (int y = 0; y < first.Height; y++)
            {
                for (int x = 0; x < first.Width; x++)
                {
                    counts[(int)first[x, y]]++;
                    var cell = new Vector2Int(x, y);
                    if (!first.IsMineable(cell)
                        && !first.IsStorageCell(cell) && !first.IsPantryCell(cell))
                        treeEligibleCells++;
                    if (first.HasTree(cell))
                    {
                        treeCells++;
                        Assert.IsFalse(first.IsMineable(cell));
                        Assert.IsFalse(first.IsStorageCell(cell));
                        Assert.IsTrue(first.IsWalkable(cell));
                    }
                    if (first[x, y] != second[x, y])
                        differences++;
                }
            }
            Assert.Greater(differences, first.Width * first.Height / 10);
            Assert.AreEqual(treeEligibleCells * 2 / 5, treeCells);
            foreach (TerrainKind terrain in new[]
                { TerrainKind.Dirt, TerrainKind.Grass, TerrainKind.Mud, TerrainKind.Road, TerrainKind.Rock, TerrainKind.Mountain })
                Assert.Greater(counts[(int)terrain], 0);
            Assert.AreEqual(24, counts[(int)TerrainKind.Road]);
            Assert.Zero(counts[(int)TerrainKind.Trench]);
            Assert.Zero(counts[(int)TerrainKind.Tunnel]);
        }

        [Test]
        public void MountainsAreSolidAndTunnelsAreWalkableCoveredSpace()
        {
            var map = new MapData(5, 5);
            var cell = new Vector2Int(2, 2);
            map.SetTree(cell, true);
            map[cell.x, cell.y] = TerrainKind.Mountain;
            Assert.IsTrue(map.IsMineable(cell));
            Assert.IsFalse(map.IsWalkable(cell));
            Assert.IsFalse(map.HasTree(cell));
            Assert.Throws<InvalidOperationException>(() => map.SetTree(cell, true));
            Assert.Throws<InvalidOperationException>(() => map.AddGroundItem(cell, ItemType.BuildingStone));
            map[cell.x, cell.y] = TerrainKind.Tunnel;
            Assert.IsTrue(map.IsWalkable(cell));
            Assert.IsTrue(map.IsTunnel(cell));
            Assert.IsTrue(map.IsIndoors(cell));
            Assert.IsTrue(map.CanStep(cell + Vector2Int.down, cell));
            Assert.IsFalse(map.IsMineable(cell));
            Assert.Throws<InvalidOperationException>(() => map.SetTree(cell, true));
            map.AddGroundItem(cell, ItemType.BuildingStone);
            Assert.IsTrue(map.TryGetGroundItem(cell, out _));
        }

        [Test]
        public void TunnelShelterClosesOnlyAfterEveryExitDoorIsBuiltAndReopensAfterMining()
        {
            var map = new MapData(9, 9);
            for (int y = 1; y <= 7; y++)
                for (int x = 2; x <= 6; x++)
                    map[x, y] = TerrainKind.Mountain;
            for (int y = 1; y <= 7; y++)
                map[4, y] = TerrainKind.Tunnel;
            var first = new Vector2Int(4, 1);
            var second = new Vector2Int(4, 7);
            var inside = new Vector2Int(4, 4);
            Assert.IsTrue(map.IsIndoors(inside));
            Assert.IsFalse(map.IsClosedShelter(inside));
            Assert.AreEqual(1, map.OpenTunnelCount);
            Assert.IsTrue(map.TryPlanTunnelDoor(first));
            Assert.IsTrue(map.TryPlanTunnelDoor(second));
            Assert.IsFalse(map.IsClosedShelter(inside));
            Assert.IsTrue(map.CompleteTunnelDoor(first));
            Assert.IsFalse(map.IsClosedShelter(inside));
            Assert.IsTrue(map.CompleteTunnelDoor(second));
            Assert.IsTrue(map.IsClosedShelter(inside));
            Assert.AreEqual(1, map.ClosedTunnelShelterCount);
            Assert.Zero(map.OpenTunnelCount);
            Assert.IsTrue(map.CanStep(new Vector2Int(4, 0), first));
            Assert.IsTrue(map.CanStep(first, new Vector2Int(4, 2)));
            Assert.IsFalse(map.CanStep(new Vector2Int(3, 0), first));
            map[3, 4] = TerrainKind.Tunnel;
            map[2, 4] = TerrainKind.Tunnel;
            Assert.IsFalse(map.IsClosedShelter(inside));
            Assert.Zero(map.ClosedTunnelShelterCount);
            Assert.AreEqual(1, map.OpenTunnelCount);
        }

        [Test]
        public void TunnelDoorsRejectOpenRoomsResourcesAndDuplicatesAndCancellationLeavesNoDoor()
        {
            var map = new MapData(7, 7);
            var door = new Vector2Int(3, 2);
            map[3, 2] = TerrainKind.Tunnel;
            map[3, 3] = TerrainKind.Tunnel;
            Assert.IsFalse(map.TryPlanTunnelDoor(door));
            map[2, 2] = TerrainKind.Mountain;
            map[4, 2] = TerrainKind.Mountain;
            map.AddGroundItem(door, ItemType.BuildingStone);
            Assert.IsFalse(map.TryPlanTunnelDoor(door));
            map.TryTakeGroundItem(door, out _);
            Assert.IsTrue(map.TryPlanTunnelDoor(door));
            Assert.IsFalse(map.TryPlanTunnelDoor(door));
            map.CancelTunnelDoor(door);
            Assert.IsNull(map.GetTunnelDoor(door));
            Assert.IsFalse(map.CompleteTunnelDoor(door));
            Assert.IsTrue(map.TryPlanTunnelDoor(door));
            Assert.IsTrue(map.CompleteTunnelDoor(door));
            map.CancelTunnelDoor(door);
            Assert.IsTrue(map.GetTunnelDoor(door).IsBuilt);
            Assert.IsTrue(map.IsWalkable(door));
        }

        [Test]
        public void GeneratedDepotIsFourByFourBesideRoadAndHasStartingTools()
        {
            MapData map = MapGenerator.Generate(new MapGenerationSettings());
            Assert.IsTrue(map.HasStorageArea);
            Assert.AreEqual(4, map.StorageWidth);
            Assert.AreEqual(4, map.StorageHeight);
            Assert.IsTrue(map.HasPantryArea);
            Assert.AreEqual(2, map.PantryWidth);
            Assert.AreEqual(4, map.PantryHeight);
            Assert.AreEqual(map.StorageOrigin.x, map.PantryOrigin.x);
            Assert.AreEqual(map.StorageOrigin.y, map.PantryOrigin.y + map.PantryHeight);
            Assert.GreaterOrEqual(map.StorageOrigin.y, 4);
            Assert.AreEqual(12, map.StorageStockpile.TotalCount);
            Assert.AreEqual(6, map.StorageStockpile.Count(ItemType.Shovel));
            Assert.AreEqual(2, map.StorageStockpile.Count(ItemType.Axe));
            Assert.AreEqual(2, map.StorageStockpile.Count(ItemType.Pickaxe));
            Assert.AreEqual(2, map.StorageStockpile.Count(ItemType.ConstructionHammer));
            Assert.AreEqual(1000, map.PantryStockpile.Count(ItemType.Rations));
            Assert.AreEqual(ItemCategory.Food, ItemCatalog.Get(ItemType.Rations).Category);
            Assert.AreEqual(250, ItemCatalog.Get(ItemType.Rations).StackSize);
            IReadOnlyList<ItemStack> rationStacks = map.PantryStockpile.GetStacks(ItemType.Rations);
            Assert.AreEqual(4, rationStacks.Count);
            foreach (ItemStack stack in rationStacks)
                Assert.AreEqual(250, stack.Count);
            Assert.IsFalse(map.StorageStockpile.TryTake(ItemType.Shovel, 7));
            Assert.AreEqual(6, map.StorageStockpile.Count(ItemType.Shovel));
            Assert.IsTrue(map.StorageStockpile.TryTake(ItemType.Shovel, 1));
            Assert.AreEqual(5, map.StorageStockpile.Count(ItemType.Shovel));

            int roadWidth = Mathf.Min(4, map.Width);
            int roadStartX = (map.Width - roadWidth) / 2;
            Assert.IsTrue(map.StorageOrigin.x + map.StorageWidth == roadStartX
                || map.StorageOrigin.x == roadStartX + roadWidth);
            bool adjacentToRoad = false;
            for (int y = map.StorageOrigin.y; y < map.StorageOrigin.y + map.StorageHeight; y++)
                for (int x = map.StorageOrigin.x; x < map.StorageOrigin.x + map.StorageWidth; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Assert.IsTrue(map.IsStorageCell(cell));
                    Assert.IsTrue(map.IsWalkable(cell));
                    Assert.IsFalse(map.HasTree(cell));
                    Assert.AreNotEqual(TerrainKind.Rock, map[cell.x, cell.y]);
                    foreach (Vector2Int direction in new[]
                        { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up })
                        if (map.Contains(cell + direction) && map[cell.x + direction.x, cell.y + direction.y] == TerrainKind.Road)
                            adjacentToRoad = true;
                }
            Assert.IsTrue(adjacentToRoad);
            for (int y = map.PantryOrigin.y; y < map.PantryOrigin.y + map.PantryHeight; y++)
                for (int x = map.PantryOrigin.x; x < map.PantryOrigin.x + map.PantryWidth; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Assert.IsTrue(map.IsPantryCell(cell));
                    Assert.IsTrue(map.IsWalkable(cell));
                    Assert.IsFalse(map.HasTree(cell));
                    Assert.AreNotEqual(TerrainKind.Road, map[cell.x, cell.y]);
                }

            ItemDefinition shovel = ItemCatalog.Get(ItemType.Shovel);
            ItemDefinition axe = ItemCatalog.Get(ItemType.Axe);
            ItemDefinition pickaxe = ItemCatalog.Get(ItemType.Pickaxe);
            Assert.AreEqual(ItemCategory.Tool, shovel.Category);
            Assert.AreEqual(ItemCategory.Tool, axe.Category);
            Assert.AreEqual(ItemCategory.Tool, pickaxe.Category);
            Assert.AreEqual(ItemAction.DigTrench, shovel.Action);
            Assert.AreEqual(ItemAction.CutTree, axe.Action);
            Assert.AreEqual(ItemAction.BreakRock, pickaxe.Action);
            ItemDefinition hammer = ItemCatalog.Get(ItemType.ConstructionHammer);
            Assert.AreEqual("Martello da Costruzione", hammer.DisplayName);
            Assert.AreEqual(ItemCategory.Tool, hammer.Category);
            Assert.AreEqual(ItemAction.Build, hammer.Action);
            Assert.AreEqual(1, hammer.StackSize);
        }

        [TestCase(128, 128, 1916)]
        [TestCase(129, 100, 1917)]
        [TestCase(4, 6, 10)]
        [TestCase(3, 8, 42)]
        [TestCase(8, 3, 42)]
        [TestCase(1, 1, 42)]
        public void RoadIsCentredOnSouthEdgeWithExactFootprintAndWalkableConnections(int width, int height, int seed)
        {
            MapData map = MapGenerator.Generate(new MapGenerationSettings { width = width, height = height, seed = seed });
            int roadWidth = Mathf.Min(4, width);
            int roadLength = Mathf.Min(6, height);
            int startX = (width - roadWidth) / 2;
            int roadCount = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    bool expectedRoad = x >= startX && x < startX + roadWidth && y < roadLength;
                    Assert.AreEqual(expectedRoad, map[x, y] == TerrainKind.Road, $"Cell {cell}");
                    if (!expectedRoad)
                        continue;
                    roadCount++;
                    Assert.IsTrue(map.IsWalkable(cell));
                    foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        if (map.Contains(cell + direction))
                            Assert.IsTrue(map.CanStep(cell, cell + direction));
                }
            Assert.AreEqual(roadWidth * roadLength, roadCount);
        }

        [TestCase(0, 128)]
        [TestCase(128, 0)]
        [TestCase(513, 128)]
        public void InvalidDimensionsAreRejected(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MapGenerator.Generate(new MapGenerationSettings { width = width, height = height }));
        }

        [Test]
        public void InvalidNoiseAndThresholdsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MapGenerator.Generate(new MapGenerationSettings { noiseScale = float.NaN }));
            Assert.Throws<ArgumentException>(() =>
                MapGenerator.Generate(new MapGenerationSettings { mudThreshold = 0.8f, grassThreshold = 0.2f }));
            Assert.Throws<ArgumentException>(() =>
                MapGenerator.Generate(new MapGenerationSettings { grassThreshold = 0.7f, rockThreshold = 0.6f }));
            Assert.Throws<ArgumentException>(() =>
                MapGenerator.Generate(new MapGenerationSettings { mudThreshold = 0.1f, grassThreshold = 0.2f, rockThreshold = -0.1f }));
            var map = new MapData(3, 2);
            Assert.IsFalse(map.Contains(new Vector2Int(-1, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => { TerrainKind unused = map[3, 0]; });
        }

        [TestCase(TerrainKind.Rock)]
        [TestCase(TerrainKind.Mountain)]
        public void SolidTerrainCannotBeMadeWalkable(TerrainKind terrain)
        {
            var map = new MapData(1, 1);
            map[0, 0] = terrain;
            Assert.IsFalse(map.IsWalkable(Vector2Int.zero));
            Assert.Throws<InvalidOperationException>(() => map.SetTree(Vector2Int.zero, true));
            map.SetWalkable(Vector2Int.zero, true);
            Assert.IsFalse(map.IsWalkable(Vector2Int.zero));
        }

        [Test]
        public void RenderingUsesCorrectTilesAndClearsPreviousMap()
        {
            var grid = new GameObject("Test grid", typeof(Grid));
            var ground = new GameObject("Test ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(MapTilemapView));
            ground.transform.SetParent(grid.transform, false);
            grid.transform.position = new Vector3(10f, 20f, 0f);
            var dirt = ScriptableObject.CreateInstance<Tile>();
            var grass = ScriptableObject.CreateInstance<Tile>();
            var mud = ScriptableObject.CreateInstance<Tile>();
            var road = ScriptableObject.CreateInstance<Tile>();
            var rock = ScriptableObject.CreateInstance<Tile>();
            var gravel = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var map = new MapData(3, 2);
                map[1, 0] = TerrainKind.Grass;
                map[2, 1] = TerrainKind.Mud;
                map[2, 0] = TerrainKind.Gravel;
                map[0, 1] = TerrainKind.Road;
                map[1, 1] = TerrainKind.Rock;
                map.SetTree(new Vector2Int(1, 0), true);
                var view = ground.GetComponent<MapTilemapView>();
                view.Configure(dirt, grass, mud);
                Assert.Throws<InvalidOperationException>(() => view.Render(map));
                view.ConfigureRoad(road);
                Assert.Throws<InvalidOperationException>(() => view.Render(map));
                view.ConfigureRock(rock);
                Assert.Throws<InvalidOperationException>(() => view.Render(map));
                view.ConfigureGravel(gravel);
                view.Render(map);
                Assert.AreEqual(dirt, view.Tilemap.GetTile(Vector3Int.zero));
                Assert.AreEqual(grass, view.Tilemap.GetTile(new Vector3Int(1, 0, 0)));
                Assert.AreEqual(mud, view.Tilemap.GetTile(new Vector3Int(2, 1, 0)));
                Assert.AreEqual(gravel, view.Tilemap.GetTile(new Vector3Int(2, 0, 0)));
                Assert.AreEqual(road, view.Tilemap.GetTile(new Vector3Int(0, 1, 0)));
                Assert.AreEqual(rock, view.Tilemap.GetTile(new Vector3Int(1, 1, 0)));
                Assert.IsFalse(map.IsWalkable(new Vector2Int(1, 1)));
                Assert.IsTrue(map.IsWalkable(new Vector2Int(1, 0)));
                Tilemap treeTilemap = view.transform.Find("Trees").GetComponent<Tilemap>();
                Assert.IsNotNull(treeTilemap.GetTile(new Vector3Int(1, 0, 0)));
                Assert.AreEqual(new Vector3(10f, 20f, 0f), view.WorldBounds.min);
                Assert.AreEqual(new Vector3(13f, 22f, 0f), view.WorldBounds.max);
                view.CompleteRockMine(map, new Vector2Int(1, 1));
                Assert.AreEqual(TerrainKind.Dirt, map[1, 1]);
                Assert.AreSame(dirt, view.Tilemap.GetTile(new Vector3Int(1, 1, 0)));
                Assert.IsTrue(map.IsWalkable(new Vector2Int(1, 1)));
                Assert.IsTrue(map.TryGetGroundItem(new Vector2Int(1, 1), out ItemType resource));
                Assert.AreEqual(ItemType.BuildingStone, resource);
                Assert.AreEqual("Pietra da costruzione", treeTilemap.GetTile(new Vector3Int(1, 1, 0)).name);
                view.Render(new MapData(1, 1));
                Assert.IsNull(view.Tilemap.GetTile(new Vector3Int(2, 1, 0)));
                Assert.IsNull(treeTilemap.GetTile(new Vector3Int(1, 0, 0)));
            }
            finally
            {
                Object.DestroyImmediate(grid);
                Object.DestroyImmediate(dirt);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(mud);
                Object.DestroyImmediate(road);
                Object.DestroyImmediate(rock);
                Object.DestroyImmediate(gravel);
            }
        }

        [TestCase(1.777778f, 128f, 128f)]
        [TestCase(0.5625f, 128f, 128f)]
        [TestCase(3.555556f, 128f, 128f)]
        [TestCase(2f, 2f, 1f)]
        public void CameraViewStaysInsideMapAtZoomLimitsAndAfterResize(float aspect, float width, float height)
        {
            var cameraObject = new GameObject("Test camera", typeof(Camera), typeof(MapCameraController));
            var viewport = new RenderTexture(800, 600, 0);
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.targetTexture = viewport;
                camera.orthographic = true;
                camera.aspect = aspect;
                cameraObject.transform.position = new Vector3(-500f, 500f, -10f);
                var controller = cameraObject.GetComponent<MapCameraController>();
                var bounds = new Bounds(new Vector3(width / 2f, height / 2f, 0f), new Vector3(width, height, 0f));
                controller.SetMapBounds(bounds, true);
                controller.ZoomAt(camera.pixelRect.center, -1000f);
                float expectedMaximum = Mathf.Min(48f, Mathf.Min(height / 2f, width / (2f * aspect)));
                Assert.AreEqual(expectedMaximum, camera.orthographicSize, 0.001f);
                AssertInside(camera, bounds);
                controller.ZoomAt(camera.pixelRect.center, 1000f);
                Assert.AreEqual(Mathf.Min(4f, expectedMaximum), camera.orthographicSize, 0.001f);
                AssertInside(camera, bounds);
                camera.aspect = aspect * 2f;
                cameraObject.transform.position = new Vector3(-500f, 500f, -10f);
                UpdateCamera(controller);
                AssertInside(camera, bounds);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(viewport);
            }
        }

        [Test]
        public void ZoomKeepsWorldPointUnderCursorAwayFromEdges()
        {
            var cameraObject = new GameObject("Test camera", typeof(Camera), typeof(MapCameraController));
            var viewport = new RenderTexture(800, 600, 0);
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.targetTexture = viewport;
                camera.orthographic = true;
                camera.aspect = 16f / 9f;
                camera.orthographicSize = 20f;
                cameraObject.transform.position = new Vector3(64f, 64f, -10f);
                var controller = cameraObject.GetComponent<MapCameraController>();
                controller.SetMapBounds(new Bounds(new Vector3(64f, 64f, 0f), new Vector3(128f, 128f, 0f)), true);
                Vector3 cursor = new Vector3(camera.pixelWidth * 0.7f, camera.pixelHeight * 0.6f, 10f);
                Vector3 before = camera.ScreenToWorldPoint(cursor);
                controller.ZoomAt(cursor, 1f);
                Assert.Less(Vector3.Distance(before, camera.ScreenToWorldPoint(cursor)), 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(viewport);
            }
        }

        [Test]
        public void SavedPrototypeSceneHasAllReferencesAndGeneratesExpectedTilemap()
        {
            var scene = EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                MapPrototype prototype = null;
                MapTilemapView view = null;
                Camera camera = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>())
                        Assert.Zero(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
                    if (root.TryGetComponent(out MapPrototype foundPrototype))
                        prototype = foundPrototype;
                    if (root.TryGetComponent(out Camera foundCamera))
                        camera = foundCamera;
                    MapTilemapView foundView = root.GetComponentInChildren<MapTilemapView>();
                    if (foundView != null)
                        view = foundView;
                }
                Assert.IsNotNull(prototype);
                Assert.IsNotNull(view);
                Assert.IsNotNull(camera);
                prototype.Regenerate();
                Assert.AreEqual(new Vector3Int(128, 128, 1), view.Tilemap.cellBounds.size);
                Assert.AreEqual(6, view.Tilemap.GetUsedTilesCount());
                Tile road = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Prototype/Road.asset");
                Tile rock = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Prototype/Rock.asset");
                Tile gravel = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Prototype/Gravel.asset");
                Assert.IsNotNull(road);
                Assert.IsNotNull(rock);
                Assert.IsNotNull(gravel);
                Assert.IsNotNull(road.sprite);
                Assert.IsNotNull(rock.sprite);
                Assert.AreEqual(Tile.ColliderType.None, road.colliderType);
                Assert.AreEqual(Tile.ColliderType.None, rock.colliderType);
                Assert.IsTrue(prototype.Data.HasStorageArea);
                Assert.AreEqual(12, prototype.Data.StorageStockpile.TotalCount);
                Transform depotDisplay = view.transform.Find("Depot stockpile items");
                Assert.IsNotNull(depotDisplay);
                Assert.AreEqual(5, depotDisplay.GetComponentsInChildren<SpriteRenderer>().Length);
                Assert.AreEqual(5, depotDisplay.GetComponentsInChildren<TextMesh>().Length);
                SpriteRenderer hammerDisplay = depotDisplay.Find("ConstructionHammer").GetComponent<SpriteRenderer>();
                Assert.IsTrue(hammerDisplay.gameObject.activeSelf);
                Assert.IsNotNull(hammerDisplay.sprite);
                Assert.Greater(hammerDisplay.sprite.texture.GetPixel(7, 12).a, 0f);
                Assert.AreEqual("Martello\nx2", hammerDisplay.GetComponentInChildren<TextMesh>().text);
                Assert.IsTrue(prototype.Data.StorageStockpile.TryTake(ItemType.Pickaxe, 1));
                Assert.AreEqual(6, prototype.Data.StorageStockpile.Count(ItemType.Shovel));
                Assert.AreEqual(2, prototype.Data.StorageStockpile.Count(ItemType.Axe));
                Assert.AreEqual(1, prototype.Data.StorageStockpile.Count(ItemType.Pickaxe));
                Assert.AreEqual(5, depotDisplay.GetComponentsInChildren<SpriteRenderer>().Length);
                var labels = new HashSet<string>();
                foreach (TextMesh label in depotDisplay.GetComponentsInChildren<TextMesh>())
                    labels.Add(label.text);
                CollectionAssert.Contains(labels, "Pala x6");
                CollectionAssert.Contains(labels, "Accetta x2");
                CollectionAssert.Contains(labels, "Piccone x1");
                int rockCells = 0;
                for (int y = 0; y < prototype.Data.Height; y++)
                    for (int x = 0; x < prototype.Data.Width; x++)
                        if (prototype.Data[x, y] == TerrainKind.Rock)
                        {
                            rockCells++;
                            Assert.IsFalse(prototype.Data.IsWalkable(new Vector2Int(x, y)));
                            Assert.GreaterOrEqual(y, 10);
                            Assert.AreSame(rock, view.Tilemap.GetTile(new Vector3Int(x, y, 0)));
                        }
                Assert.Greater(rockCells, 0);
                for (int y = 0; y < 6; y++)
                    for (int x = 62; x < 66; x++)
                        Assert.AreSame(road, view.Tilemap.GetTile(new Vector3Int(x, y, 0)));
                prototype.Data[62, 0] = TerrainKind.Dirt;
                prototype.Regenerate();
                Assert.AreEqual(TerrainKind.Road, prototype.Data[62, 0]);
                Assert.AreSame(road, view.Tilemap.GetTile(new Vector3Int(62, 0, 0)));
                Assert.AreEqual(prototype.Data.Width * prototype.Data.Height,
                    view.Tilemap.GetTilesBlock(view.Tilemap.cellBounds).Length);
                foreach (TileBase tile in view.Tilemap.GetTilesBlock(view.Tilemap.cellBounds))
                    Assert.IsNotNull(tile);
                AssertInside(camera, view.WorldBounds);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void UpdateCamera(MapCameraController controller)
        {
            typeof(MapCameraController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        private static void AssertInside(Camera camera, Bounds bounds)
        {
            const float tolerance = 0.001f;
            float height = camera.orthographicSize;
            float width = height * camera.aspect;
            Assert.GreaterOrEqual(camera.transform.position.x - width, bounds.min.x - tolerance);
            Assert.LessOrEqual(camera.transform.position.x + width, bounds.max.x + tolerance);
            Assert.GreaterOrEqual(camera.transform.position.y - height, bounds.min.y - tolerance);
            Assert.LessOrEqual(camera.transform.position.y + height, bounds.max.y + tolerance);
        }
    }
}
