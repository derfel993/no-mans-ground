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
                    Assert.AreEqual(first[x, y], second[x, y], $"Cell {x}, {y}");
        }

        [Test]
        public void DifferentSeedChangesTerrainAndAllTerrainTypesExist()
        {
            var settings = new MapGenerationSettings();
            MapData first = MapGenerator.Generate(settings);
            settings.seed++;
            MapData second = MapGenerator.Generate(settings);
            int differences = 0;
            var counts = new int[3];
            for (int y = 0; y < first.Height; y++)
            {
                for (int x = 0; x < first.Width; x++)
                {
                    counts[(int)first[x, y]]++;
                    if (first[x, y] != second[x, y])
                        differences++;
                }
            }
            Assert.Greater(differences, first.Width * first.Height / 10);
            foreach (int count in counts)
                Assert.Greater(count, 0);
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
            var map = new MapData(3, 2);
            Assert.IsFalse(map.Contains(new Vector2Int(-1, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => { TerrainKind unused = map[3, 0]; });
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
            try
            {
                var map = new MapData(3, 2);
                map[1, 0] = TerrainKind.Grass;
                map[2, 1] = TerrainKind.Mud;
                var view = ground.GetComponent<MapTilemapView>();
                view.Configure(dirt, grass, mud);
                view.Render(map);
                Assert.AreEqual(dirt, view.Tilemap.GetTile(Vector3Int.zero));
                Assert.AreEqual(grass, view.Tilemap.GetTile(new Vector3Int(1, 0, 0)));
                Assert.AreEqual(mud, view.Tilemap.GetTile(new Vector3Int(2, 1, 0)));
                Assert.AreEqual(new Vector3(10f, 20f, 0f), view.WorldBounds.min);
                Assert.AreEqual(new Vector3(13f, 22f, 0f), view.WorldBounds.max);
                view.Render(new MapData(1, 1));
                Assert.IsNull(view.Tilemap.GetTile(new Vector3Int(2, 1, 0)));
            }
            finally
            {
                Object.DestroyImmediate(grid);
                Object.DestroyImmediate(dirt);
                Object.DestroyImmediate(grass);
                Object.DestroyImmediate(mud);
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
                Assert.AreEqual(3, view.Tilemap.GetUsedTilesCount());
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
