using System.Collections;
using NoMansGround.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoMansGround.Tests
{
    public sealed class SplashScreenTests
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
                Assert.AreEqual(3f, settings.FindProperty("displaySeconds").floatValue);
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
            Assert.IsTrue(settings.FindProperty("m_OverrideGlobalSceneList").boolValue);
            var scenes = settings.FindProperty("m_Scenes");
            Assert.GreaterOrEqual(scenes.arraySize, 2);
            Assert.IsTrue(scenes.GetArrayElementAtIndex(0).FindPropertyRelative("m_enabled").boolValue);
            Assert.AreEqual("Assets/Scenes/Splash.unity",
                scenes.GetArrayElementAtIndex(0).FindPropertyRelative("m_path").stringValue);
            Assert.IsTrue(scenes.GetArrayElementAtIndex(1).FindPropertyRelative("m_enabled").boolValue);
            Assert.AreEqual("Assets/Scenes/MapPrototype.unity",
                scenes.GetArrayElementAtIndex(1).FindPropertyRelative("m_path").stringValue);
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
        public IEnumerator SplashShowsImageThenLoadsMapEvenWhenTimeIsPaused()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Splash.unity");
            yield return new EnterPlayMode();
            Time.timeScale = 0f;
            yield return null;
            var picture = Object.FindAnyObjectByType<RawImage>();
            Assert.IsNotNull(picture);
            Assert.IsNotNull(picture.texture);
            Assert.AreEqual("Splash", SceneManager.GetActiveScene().name);
            var fitter = picture.GetComponent<AspectRatioFitter>();
            Assert.AreEqual(AspectRatioFitter.AspectMode.FitInParent, fitter.aspectMode);
            Assert.AreEqual((float)picture.texture.width / picture.texture.height, fitter.aspectRatio, 0.0001f);
            float started = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual("Splash", SceneManager.GetActiveScene().name);
            while (SceneManager.GetActiveScene().name == "Splash" && Time.realtimeSinceStartup - started < 15f)
                yield return null;
            Assert.AreEqual("MapPrototype", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindAnyObjectByType<MapPrototype>());
            Assert.IsNull(Object.FindAnyObjectByType<GameSplashScreen>());
            Debug.Log("Splash transition verified: Splash -> MapPrototype, even while paused.");
            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }
    }
}