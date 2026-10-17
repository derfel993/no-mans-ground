using System;
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
            int nonRockCells = 0;
            int treeCells = 0;
            var counts = new int[Enum.GetValues(typeof(TerrainKind)).Length];
            for (int y = 0; y < first.Height; y++)
            {
                for (int x = 0; x < first.Width; x++)
                {
                    counts[(int)first[x, y]]++;
                    var cell = new Vector2Int(x, y);
                    if (first[x, y] != TerrainKind.Rock)
                        nonRockCells++;
                    if (first.HasTree(cell))
                    {
                        treeCells++;
                        Assert.AreNotEqual(TerrainKind.Rock, first[x, y]);
                        Assert.IsTrue(first.IsWalkable(cell));
                    }
                    if (first[x, y] != second[x, y])
                        differences++;
                }
            }
            Assert.Greater(differences, first.Width * first.Height / 10);
            Assert.AreEqual(nonRockCells * 2 / 5, treeCells);
            foreach (TerrainKind terrain in new[]
                { TerrainKind.Dirt, TerrainKind.Grass, TerrainKind.Mud, TerrainKind.Road, TerrainKind.Rock })
                Assert.Greater(counts[(int)terrain], 0);
            Assert.AreEqual(24, counts[(int)TerrainKind.Road]);
            Assert.Zero(counts[(int)TerrainKind.Trench]);
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

        [Test]
        public void RockTerrainCannotBeMadeWalkable()
        {
            var map = new MapData(1, 1);
            map[0, 0] = TerrainKind.Rock;
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
            try
            {
                var map = new MapData(3, 2);
                map[1, 0] = TerrainKind.Grass;
                map[2, 1] = TerrainKind.Mud;
                map[0, 1] = TerrainKind.Road;
                map[1, 1] = TerrainKind.Rock;
                map.SetTree(new Vector2Int(1, 0), true);
                var view = ground.GetComponent<MapTilemapView>();
                view.Configure(dirt, grass, mud);
                Assert.Throws<InvalidOperationException>(() => view.Render(map));
                view.ConfigureRoad(road);
                Assert.Throws<InvalidOperationException>(() => view.Render(map));
                view.ConfigureRock(rock);
                view.Render(map);
                Assert.AreEqual(dirt, view.Tilemap.GetTile(Vector3Int.zero));
                Assert.AreEqual(grass, view.Tilemap.GetTile(new Vector3Int(1, 0, 0)));
                Assert.AreEqual(mud, view.Tilemap.GetTile(new Vector3Int(2, 1, 0)));
                Assert.AreEqual(road, view.Tilemap.GetTile(new Vector3Int(0, 1, 0)));
                Assert.AreEqual(rock, view.Tilemap.GetTile(new Vector3Int(1, 1, 0)));
                Assert.IsFalse(map.IsWalkable(new Vector2Int(1, 1)));
                Assert.IsTrue(map.IsWalkable(new Vector2Int(1, 0)));
                Tilemap treeTilemap = view.transform.Find("Trees").GetComponent<Tilemap>();
                Assert.IsNotNull(treeTilemap.GetTile(new Vector3Int(1, 0, 0)));
                Assert.AreEqual(new Vector3(10f, 20f, 0f), view.WorldBounds.min);
                Assert.AreEqual(new Vector3(13f, 22f, 0f), view.WorldBounds.max);
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
                Assert.AreEqual(5, view.Tilemap.GetUsedTilesCount());
                Tile road = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Prototype/Road.asset");
                Tile rock = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Prototype/Rock.asset");
                Assert.IsNotNull(road);
                Assert.IsNotNull(rock);
                Assert.IsNotNull(road.sprite);
                Assert.IsNotNull(rock.sprite);
                Assert.AreEqual(Tile.ColliderType.None, road.colliderType);
                Assert.AreEqual(Tile.ColliderType.None, rock.colliderType);
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
