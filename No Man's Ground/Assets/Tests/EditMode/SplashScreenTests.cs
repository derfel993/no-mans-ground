using System.Collections;
using NoMansGround.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoMansGround.Tests
{
    public sealed class SplashScreenTests : InputTestFixture
    {
        [Test]
        public void StartupSceneUsesSplashImageAndBuildLoadsSplashBeforeMap()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Splash.unity");
            try
            {
                var splash = Object.FindAnyObjectByType<GameSplashScreen>();
                Assert.IsNotNull(splash);
                var settings = new SerializedObject(splash);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/Splash/SplashScreen.jpg"),
                    settings.FindProperty("image").objectReferenceValue);
                Assert.AreEqual(13f, settings.FindProperty("displaySeconds").floatValue);
                Assert.AreEqual("Assets/Scenes/MapPrototype.unity", settings.FindProperty("nextScene").stringValue);
                Assert.AreEqual("Assets/Scenes/Splash.unity", EditorBuildSettings.scenes[0].path);
                Assert.IsTrue(EditorBuildSettings.scenes[0].enabled);
                Assert.AreEqual("Assets/Scenes/MapPrototype.unity", EditorBuildSettings.scenes[1].path);
                Assert.IsTrue(EditorBuildSettings.scenes[1].enabled);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void WindowsBuildProfileLoadsSplashBeforeMap()
        {
            var profile = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Build Profiles/Windows.asset");
            Assert.IsNotNull(profile);
            var settings = new SerializedObject(profile);
            var paths = new System.Collections.Generic.List<string>();
            if (settings.FindProperty("m_OverrideGlobalSceneList").boolValue)
            {
                var scenes = settings.FindProperty("m_Scenes");
                for (int index = 0; index < scenes.arraySize; index++)
                {
                    var scene = scenes.GetArrayElementAtIndex(index);
                    if (scene.FindPropertyRelative("m_enabled").boolValue)
                        paths.Add(scene.FindPropertyRelative("m_path").stringValue);
                }
            }
            else
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                    if (scene.enabled) paths.Add(scene.path);
            Assert.GreaterOrEqual(paths.Count, 2);
            Assert.AreEqual("Assets/Scenes/Splash.unity", paths[0]);
            Assert.AreEqual("Assets/Scenes/MapPrototype.unity", paths[1]);
        }

        [TestCase(1280, 720)]
        [TestCase(600, 1000)]
        public void SplashFitsViewportWithoutCroppingAndRendersTheImage(int width, int height)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Splash.unity");
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                var splash = Object.FindAnyObjectByType<GameSplashScreen>();
                typeof(GameSplashScreen).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(splash, null);
                camera = splash.GetComponentInChildren<Camera>();
                camera.targetTexture = target;
                camera.cullingMask = ~0;
                var canvas = splash.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                var picture = splash.GetComponentInChildren<RawImage>();
                Rect rect = picture.rectTransform.rect;
                Assert.LessOrEqual(rect.width, width + 0.1f);
                Assert.LessOrEqual(rect.height, height + 0.1f);
                Assert.AreEqual((float)picture.texture.width / picture.texture.height,
                    rect.width / rect.height, 0.001f);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Assert.Greater(pixels.GetPixel(width / 2, height / 2).maxColorComponent, 0.05f);
                if (height > width)
                    Assert.Less(pixels.GetPixel(width / 2, 5).maxColorComponent, 0.01f);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"NoMansGround-Splash-{width}x{height}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [UnityTest]
        public IEnumerator SplashWaitsThirteenSecondsThenRequiresNewClickEvenWhenTimeIsPaused()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Splash.unity");
            float previousSpeed = Time.timeScale;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Time.timeScale = 0f;
                var splash = Object.FindAnyObjectByType<GameSplashScreen>();
                var waiting = (IEnumerator)typeof(GameSplashScreen).GetMethod("WaitForContinue",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(splash, null);
                float started = Time.realtimeSinceStartup;
                Assert.IsTrue(waiting.MoveNext());
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                InputSystem.Update();
                Assert.IsTrue(waiting.MoveNext());
                while (Time.realtimeSinceStartup - started < 13.2f)
                    yield return null;
                InputSystem.Update();
                Assert.IsTrue(waiting.MoveNext(),
                    "An early click held past the timer must not dismiss the splash.");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Assert.IsTrue(waiting.MoveNext());
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                InputSystem.Update();
                Assert.IsTrue(waiting.MoveNext());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                InputSystem.Update();
                Assert.IsFalse(waiting.MoveNext());
            }
            finally
            {
                Time.timeScale = previousSpeed;
                InputSystem.RemoveDevice(mouse);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}