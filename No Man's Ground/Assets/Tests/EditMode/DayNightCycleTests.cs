using System.Collections;
using NoMansGround.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMansGround.Tests
{
    public sealed class DayNightCycleTests
    {
        [TestCase(0d, DayPhase.Night, 0f)]
        [TestCase(4.99d, DayPhase.Night, 0f)]
        [TestCase(5d, DayPhase.Dawn, 0f)]
        [TestCase(6d, DayPhase.Dawn, 0.5f)]
        [TestCase(7d, DayPhase.Day, 1f)]
        [TestCase(12d, DayPhase.Day, 1f)]
        [TestCase(18d, DayPhase.Dusk, 1f)]
        [TestCase(19d, DayPhase.Dusk, 0.5f)]
        [TestCase(20d, DayPhase.Night, 0f)]
        [TestCase(24d, DayPhase.Night, 0f)]
        public void DefaultPhasesFollowConfiguredHours(double hour, DayPhase phase, float daylight)
        {
            var owner = new GameObject("Cycle test", typeof(DayNightCycle));
            try
            {
                DayNightState state = owner.GetComponent<DayNightCycle>().Evaluate(hour);
                Assert.AreEqual(phase, state.Phase);
                Assert.AreEqual(daylight, state.Daylight, 0.0001f);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase(0d)]
        [TestCase(5d)]
        [TestCase(6d)]
        [TestCase(7d)]
        [TestCase(18d)]
        [TestCase(19d)]
        [TestCase(20d)]
        public void TintAndDaylightRemainContinuousAtEveryBoundary(double hour)
        {
            var owner = new GameObject("Cycle test", typeof(DayNightCycle));
            try
            {
                var cycle = owner.GetComponent<DayNightCycle>();
                DayNightState before = cycle.Evaluate(hour - 0.0001d);
                DayNightState after = cycle.Evaluate(hour + 0.0001d);
                Assert.Less(Vector4.Distance(before.Tint, after.Tint), 0.001f);
                Assert.AreEqual(before.Daylight, after.Daylight, 0.001f);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void InspectorHoursAreConfigurableAndInvalidOrderingIsCorrected()
        {
            var owner = new GameObject("Cycle test", typeof(DayNightCycle));
            try
            {
                var cycle = owner.GetComponent<DayNightCycle>();
                var settings = new SerializedObject(cycle);
                settings.FindProperty("dawnStart").floatValue = 3f;
                settings.FindProperty("dawnEnd").floatValue = 4f;
                settings.FindProperty("duskStart").floatValue = 16f;
                settings.FindProperty("duskEnd").floatValue = 17f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(DayPhase.Dawn, cycle.Evaluate(3.5d).Phase);
                Assert.AreEqual(DayPhase.Day, cycle.Evaluate(5d).Phase);
                Assert.AreEqual(DayPhase.Night, cycle.Evaluate(18d).Phase);
                settings.FindProperty("dawnEnd").floatValue = 0f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                settings.Update();
                Assert.Greater(settings.FindProperty("dawnEnd").floatValue,
                    settings.FindProperty("dawnStart").floatValue);
                Assert.Throws<System.ArgumentOutOfRangeException>(() => cycle.Evaluate(double.NaN));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void FilterMultipliesWorldPreservesBlackAndSupportsReset()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("Map", typeof(MapPrototype), typeof(DayNightCycle));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var blackObject = new GameObject("Unexplored", typeof(SpriteRenderer));
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.black);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            blackObject.GetComponent<SpriteRenderer>().sprite = sprite;
            blackObject.transform.position = Vector3.left;
            var target = new RenderTexture(320, 180, 24);
            var pixels = new Texture2D(320, 180, TextureFormat.RGB24, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 1.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.gray;
                camera.targetTexture = target;
                var map = owner.GetComponent<MapPrototype>();
                var cycle = owner.GetComponent<DayNightCycle>();
                cycle.Configure(map, camera);
                Shader shader = Resources.Load<Shader>("DayNightTint");
                Assert.IsNotNull(shader);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
                Assert.IsTrue(shader.isSupported);
                Color day = ReadPixel(camera, target, pixels, 220, 90);
                map.Clock.AdvanceTime(1500d);
                cycle.Refresh();
                Color night = ReadPixel(camera, target, pixels, 220, 90);
                Assert.Less(night.r, day.r * 0.85f);
                Assert.Less(night.g, day.g * 0.85f);
                Assert.Less(night.b, day.b * 0.9f);
                Assert.Less(ReadPixel(camera, target, pixels, 100, 90).maxColorComponent, 0.02f);
                Assert.IsFalse(owner.GetComponentInChildren<Image>().raycastTarget);
                Assert.IsNull(owner.GetComponentInChildren<GraphicRaycaster>());
                map.Clock.AdvanceTime(0d);
                cycle.Refresh();
                Assert.AreEqual(night, ReadPixel(camera, target, pixels, 220, 90));
                map.Clock.Reset();
                cycle.Refresh();
                Assert.AreEqual(DayPhase.Day, cycle.State.Phase);
                Assert.AreEqual(day, ReadPixel(camera, target, pixels, 220, 90));
                Assert.AreEqual(1, owner.GetComponentsInChildren<Canvas>().Length);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(blackObject);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Time.timeScale = 1f;
            }
        }

        private static Color ReadPixel(Camera camera, RenderTexture target, Texture2D pixels, int column, int row)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            return pixels.GetPixel(column, row);
        }

        [UnityTest]
        public IEnumerator MapAutomaticallyStartsCycleAndFollowsSpeedPauseAndRegeneration()
        {
            yield return new EnterPlayMode();
            var owner = new GameObject("Map", typeof(Grid), typeof(MapPrototype));
            var ground = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(MapTilemapView));
            ground.transform.SetParent(owner.transform, false);
            var cameraObject = new GameObject("Camera", typeof(Camera), typeof(MapCameraController));
            cameraObject.GetComponent<Camera>().orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var map = owner.GetComponent<MapPrototype>();
            var view = ground.GetComponent<MapTilemapView>();
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            view.Configure(tile, tile, tile);
            view.ConfigureRoad(tile);
            view.ConfigureRock(tile);
            view.ConfigureGravel(tile);
            view.ConfigureTrench(tile);
            view.ConfigureLadder(tile);
            map.Configure(view, cameraObject.GetComponent<MapCameraController>());
            yield return null;
            var cycle = map.GetComponent<DayNightCycle>();
            Assert.IsNotNull(cycle);
            Assert.AreEqual(DayPhase.Day, cycle.State.Phase);
            map.SetSimulationSpeed(0);
            map.Clock.AdvanceTime(1000d - map.Clock.ElapsedSeconds);
            cycle.Refresh();
            Assert.AreEqual(DayPhase.Dusk, cycle.State.Phase);
            DayNightState paused = cycle.State;
            double pausedSeconds = map.Clock.ElapsedSeconds;
            yield return null;
            yield return null;
            Assert.AreEqual(pausedSeconds, map.Clock.ElapsedSeconds);
            Assert.AreEqual(paused.Tint, cycle.State.Tint);
            cycle.enabled = false;
            Assert.IsNull(map.GetComponentInChildren<Canvas>());
            cycle.enabled = true;
            Assert.AreEqual(paused.Tint, cycle.State.Tint);
            map.SetSimulationSpeed(10);
            yield return null;
            yield return null;
            Assert.Greater(map.Clock.ElapsedSeconds, pausedSeconds);
            map.SetSimulationSpeed(0);
            map.Regenerate();
            Assert.AreEqual(9d, map.Clock.TimeOfDayHours);
            Assert.AreEqual(DayPhase.Day, cycle.State.Phase);
            Assert.AreEqual(1, map.GetComponentsInChildren<Canvas>().Length);
            Object.Destroy(owner);
            Object.Destroy(cameraObject);
            Object.Destroy(tile);
            yield return new ExitPlayMode();
        }
    }
}