using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NoMansGround.Map
{
    public sealed class GameSplashScreen : MonoBehaviour
    {
        [SerializeField] private Texture2D image;
        [SerializeField, Min(0f)] private float displaySeconds = 3f;
        [SerializeField] private string nextScene = "Assets/Scenes/MapPrototype.unity";

        private void Awake()
        {
            if (image == null || !float.IsFinite(displaySeconds) || displaySeconds < 0f
                || string.IsNullOrEmpty(nextScene))
            {
                Debug.LogError("GameSplashScreen requires an image, valid duration and destination scene.", this);
                enabled = false;
                return;
            }

            var cameraObject = new GameObject("Splash Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;

            var canvasObject = new GameObject("Splash Canvas", typeof(RectTransform), typeof(Canvas), typeof(Image));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.GetComponent<Image>().color = Color.black;
            canvasObject.GetComponent<Image>().raycastTarget = false;

            var picture = new GameObject("Splash Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            picture.transform.SetParent(canvasObject.transform, false);
            var rawImage = picture.GetComponent<RawImage>();
            rawImage.texture = image;
            rawImage.raycastTarget = false;
            var fitter = picture.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)image.width / image.height;
        }

        private IEnumerator Start()
        {
            yield return null;
            float showUntil = Time.realtimeSinceStartup + displaySeconds;
            AsyncOperation loading = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
            loading.allowSceneActivation = false;
            while (Time.realtimeSinceStartup < showUntil || loading.progress < 0.9f)
                yield return null;
            loading.allowSceneActivation = true;
        }
    }
}