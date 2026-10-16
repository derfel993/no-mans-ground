using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NoMansGround.Map
{
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float panSpeed = 25f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.15f;
        [SerializeField, Min(0.01f)] private float minimumZoom = 4f;
        [SerializeField, Min(0.01f)] private float maximumZoom = 48f;

        private Camera mapCamera;
        private Bounds mapBounds;
        private bool hasBounds;
        private bool dragging;
        private Vector2 previousPointer;

        private Camera MapCamera => mapCamera != null ? mapCamera : mapCamera = GetComponent<Camera>();

        public void SetMapBounds(Bounds bounds, bool recenter)
        {
            if (bounds.size.x <= 0f || bounds.size.y <= 0f)
                throw new ArgumentException("Map bounds must have positive width and height.", nameof(bounds));
            if (!MapCamera.orthographic || Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f)
                throw new InvalidOperationException("The map camera must be orthographic and face the XY plane.");
            if (minimumZoom <= 0f || maximumZoom < minimumZoom || panSpeed <= 0f || zoomSensitivity <= 0f)
                throw new InvalidOperationException("Camera speeds and zoom limits must be positive, with min <= max.");

            mapBounds = bounds;
            hasBounds = true;
            dragging = false;
            if (recenter)
                transform.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
            ConstrainToMap();
        }

        public void FocusOn(Vector3 worldPosition)
        {
            if (!hasBounds)
                throw new InvalidOperationException("Set map bounds before focusing the camera.");
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            ConstrainToMap();
        }

        private void LateUpdate()
        {
            if (!hasBounds)
                return;

            // Re-evaluate the fit every frame, including when the Game view changes aspect ratio.
            ConstrainToMap();
            HandleKeyboard();
            HandleMouse();
            ConstrainToMap();
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            Vector2 direction = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
            Vector2 movement = Vector2.ClampMagnitude(direction, 1f) * (panSpeed * Time.unscaledDeltaTime);
            transform.position += new Vector3(movement.x, movement.y, 0f);
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                dragging = false;
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool overMap = MapCamera.pixelRect.Contains(pointer)
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            if (!mouse.middleButton.isPressed)
                dragging = false;
            if (!dragging && mouse.middleButton.wasPressedThisFrame && overMap)
            {
                dragging = true;
                previousPointer = pointer;
            }
            if (dragging)
            {
                Vector3 movement = ScreenToMap(previousPointer) - ScreenToMap(pointer);
                transform.position += movement;
                previousPointer = pointer;
            }

            if (overMap)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                    ZoomAt(pointer, scroll);
            }
        }

        public void ZoomAt(Vector2 screenPosition, float steps)
        {
            if (!hasBounds)
                throw new InvalidOperationException("Set map bounds before zooming.");
            Vector3 anchor = ScreenToMap(screenPosition);
            MapCamera.orthographicSize = Mathf.Clamp(
                MapCamera.orthographicSize * Mathf.Exp(-steps * zoomSensitivity), EffectiveMinimum, EffectiveMaximum);
            transform.position += anchor - ScreenToMap(screenPosition);
            ConstrainToMap();
        }

        private Vector3 ScreenToMap(Vector2 screenPosition)
        {
            return MapCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y,
                mapBounds.center.z - transform.position.z));
        }

        private float EffectiveMaximum => Mathf.Min(maximumZoom,
            Mathf.Min(mapBounds.extents.y, mapBounds.extents.x / MapCamera.aspect));
        private float EffectiveMinimum => Mathf.Min(minimumZoom, EffectiveMaximum);

        private void ConstrainToMap()
        {
            MapCamera.orthographicSize = Mathf.Clamp(MapCamera.orthographicSize, EffectiveMinimum, EffectiveMaximum);
            float halfHeight = MapCamera.orthographicSize;
            float halfWidth = halfHeight * MapCamera.aspect;
            Vector3 position = transform.position;
            position.x = ClampAxis(position.x, mapBounds.min.x, mapBounds.max.x, halfWidth);
            position.y = ClampAxis(position.y, mapBounds.min.y, mapBounds.max.y, halfHeight);
            transform.position = position;
        }

        private static float ClampAxis(float position, float min, float max, float extent)
        {
            return extent * 2f >= max - min ? (min + max) * 0.5f
                : Mathf.Clamp(position, min + extent, max - extent);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                dragging = false;
        }

        private void OnDisable()
        {
            dragging = false;
        }
    }
}
