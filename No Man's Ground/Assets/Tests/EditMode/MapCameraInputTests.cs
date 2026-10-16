using System.Reflection;
using NoMansGround.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NoMansGround.Tests
{
    public sealed class MapCameraInputTests : InputTestFixture
    {
        [Test]
        public void MiddleMouseDragMovesCameraInWorldUnitsAndReleases()
        {
            var cameraObject = new GameObject("Test camera", typeof(Camera), typeof(MapCameraController));
            var viewport = new RenderTexture(800, 600, 0);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.targetTexture = viewport;
                camera.orthographic = true;
                camera.orthographicSize = 20f;
                cameraObject.transform.position = new Vector3(64f, 64f, -10f);
                var controller = cameraObject.GetComponent<MapCameraController>();
                controller.SetMapBounds(new Bounds(new Vector3(64f, 64f, 0f), new Vector3(128f, 128f, 0f)), true);
                Vector2 start = camera.pixelRect.center;
                Set(mouse.position, start);
                Press(mouse.middleButton);
                UpdateCamera(controller);
                Set(mouse.position, start + Vector2.right * 10f);
                UpdateCamera(controller);
                Assert.AreEqual(64f - 10f * 40f / 600f, cameraObject.transform.position.x, 0.001f);
                Vector3 afterDrag = cameraObject.transform.position;
                Release(mouse.middleButton);
                Set(mouse.position, start + Vector2.right * 20f);
                UpdateCamera(controller);
                Assert.AreEqual(afterDrag, cameraObject.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(viewport);
            }
        }

        [Test]
        public void MouseWheelUsesNormalizedStepsAndIgnoresOutsideViewport()
        {
            var cameraObject = new GameObject("Test camera", typeof(Camera), typeof(MapCameraController));
            var viewport = new RenderTexture(800, 600, 0);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.targetTexture = viewport;
                camera.orthographic = true;
                camera.orthographicSize = 20f;
                cameraObject.transform.position = new Vector3(64f, 64f, -10f);
                var controller = cameraObject.GetComponent<MapCameraController>();
                controller.SetMapBounds(new Bounds(new Vector3(64f, 64f, 0f), new Vector3(128f, 128f, 0f)), true);
                Set(mouse.position, camera.pixelRect.center);
                Set(mouse.scroll, Vector2.up);
                Assert.AreEqual(1f, mouse.scroll.ReadValue().y);
                UpdateCamera(controller);
                Assert.AreEqual(20f * Mathf.Exp(-0.15f), camera.orthographicSize, 0.001f);
                float zoom = camera.orthographicSize;
                Set(mouse.position, new Vector2(-100f, -100f));
                Set(mouse.scroll, Vector2.up);
                UpdateCamera(controller);
                Assert.AreEqual(zoom, camera.orthographicSize);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(viewport);
            }
        }

        private static void UpdateCamera(MapCameraController controller)
        {
            typeof(MapCameraController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
        }
    }
}
