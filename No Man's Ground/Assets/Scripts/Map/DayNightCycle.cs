using System;
using UnityEngine;
using UnityEngine.UI;

namespace NoMansGround.Map
{
    public enum DayPhase { Night, Dawn, Day, Dusk }

    public readonly struct DayNightState
    {
        public DayPhase Phase { get; }
        public float Daylight { get; }
        public Color Tint { get; }

        public DayNightState(DayPhase phase, float daylight, Color tint)
        {
            Phase = phase;
            Daylight = daylight;
            Tint = tint;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class DayNightCycle : MonoBehaviour
    {
        [SerializeField, Range(0f, 24f)] private float dawnStart = 5f;
        [SerializeField, Range(0f, 24f)] private float dawnEnd = 7f;
        [SerializeField, Range(0f, 24f)] private float duskStart = 18f;
        [SerializeField, Range(0f, 24f)] private float duskEnd = 20f;
        [SerializeField] private Color dayTint = Color.white;
        [SerializeField] private Color dawnTint = new Color(1f, 0.85f, 0.72f);
        [SerializeField] private Color duskTint = new Color(1f, 0.72f, 0.58f);
        [SerializeField] private Color nightTint = new Color(0.38f, 0.44f, 0.6f);
        private MapPrototype map;
        private Camera mapCamera;
        private GameObject filterObject;
        private Image filter;
        private Material filterMaterial;

        public DayNightState State { get; private set; } = new DayNightState(DayPhase.Day, 1f, Color.white);

        public void Configure(MapPrototype prototype, Camera camera)
        {
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Unsubscribe();
            map = prototype;
            mapCamera = camera;
            if (isActiveAndEnabled)
            {
                map.Generated += HandleGenerated;
                Refresh();
            }
        }

        public DayNightState Evaluate(double timeOfDayHours)
        {
            if (!double.IsFinite(timeOfDayHours))
                throw new ArgumentOutOfRangeException(nameof(timeOfDayHours));
            float hour = (float)((timeOfDayHours % 24d + 24d) % 24d);
            if (hour < dawnStart || hour >= duskEnd)
                return new DayNightState(DayPhase.Night, 0f, nightTint);
            if (hour < dawnEnd)
            {
                float progress = Mathf.InverseLerp(dawnStart, dawnEnd, hour);
                return new DayNightState(DayPhase.Dawn, Mathf.SmoothStep(0f, 1f, progress),
                    TransitionTint(nightTint, dawnTint, dayTint, progress));
            }
            if (hour < duskStart)
                return new DayNightState(DayPhase.Day, 1f, dayTint);
            float duskProgress = Mathf.InverseLerp(duskStart, duskEnd, hour);
            return new DayNightState(DayPhase.Dusk, Mathf.SmoothStep(1f, 0f, duskProgress),
                TransitionTint(dayTint, duskTint, nightTint, duskProgress));
        }

        private static Color TransitionTint(Color start, Color middle, Color end, float progress)
        {
            return progress < 0.5f
                ? Color.Lerp(start, middle, Mathf.SmoothStep(0f, 1f, progress * 2f))
                : Color.Lerp(middle, end, Mathf.SmoothStep(0f, 1f, progress * 2f - 1f));
        }

        private void OnValidate()
        {
            dawnStart = Mathf.Clamp(dawnStart, 0f, 23.97f);
            dawnEnd = Mathf.Clamp(dawnEnd, dawnStart + 0.01f, 23.98f);
            duskStart = Mathf.Clamp(duskStart, dawnEnd + 0.01f, 23.99f);
            duskEnd = Mathf.Clamp(duskEnd, duskStart + 0.01f, 24f);
        }

        private void OnEnable()
        {
            if (map != null)
            {
                Unsubscribe();
                map.Generated += HandleGenerated;
                Refresh();
            }
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void HandleGenerated(MapData data)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled || map == null || mapCamera == null)
                return;
            State = Evaluate(map.Clock.TimeOfDayHours);
            if (filterObject == null)
                CreateFilter();
            Canvas canvas = filterObject.GetComponent<Canvas>();
            canvas.worldCamera = mapCamera;
            canvas.planeDistance = Mathf.Lerp(mapCamera.nearClipPlane, mapCamera.farClipPlane, 0.001f);
            Color tint = State.Tint;
            tint.a = 1f;
            filter.color = tint;
            filterObject.SetActive(true);
        }

        private void CreateFilter()
        {
            Shader shader = Resources.Load<Shader>("DayNightTint");
            if (shader == null)
                throw new InvalidOperationException("The day-night cycle requires the DayNightTint shader.");
            filterMaterial = new Material(shader);
            filterObject = new GameObject("Day Night Filter", typeof(RectTransform), typeof(Canvas));
            filterObject.transform.SetParent(transform, false);
            Canvas canvas = filterObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = short.MaxValue;
            canvas.sortingLayerID = SortingLayer.layers[SortingLayer.layers.Length - 1].id;
            var imageObject = new GameObject("Tint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(filterObject.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            filter = imageObject.GetComponent<Image>();
            filter.material = filterMaterial;
            filter.raycastTarget = false;
        }

        private void Unsubscribe()
        {
            if (map != null)
                map.Generated -= HandleGenerated;
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (filterObject != null)
                filterObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Application.isPlaying)
            {
                if (filterObject != null) Destroy(filterObject);
                if (filterMaterial != null) Destroy(filterMaterial);
            }
            else
            {
                if (filterObject != null) DestroyImmediate(filterObject);
                if (filterMaterial != null) DestroyImmediate(filterMaterial);
            }
        }
    }
}