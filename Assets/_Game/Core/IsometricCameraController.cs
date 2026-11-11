using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections.Generic;

namespace RoadsideHaven
{
    /// <summary>Bounded orthographic camera with touch, pinch, and Editor mouse controls.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricCameraController : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Look-at point on the ground plane.")]
        [SerializeField] private Vector3 focusPoint;
        [Tooltip("Bounds within which the camera focus may move.")]
        [SerializeField] private Vector2 focusBounds = new Vector2(10f, 9f);
        [Tooltip("Minimum and maximum orthographic camera size.")]
        [SerializeField] private Vector2 zoomBounds = new Vector2(7f, 16f);
        [Tooltip("Mouse wheel zoom multiplier.")]
        [SerializeField, Min(0.001f)] private float wheelZoomSpeed = 1.2f;
        [Tooltip("Screen-pixel movement before a pointer is considered a drag.")]
        [SerializeField, Min(1f)] private float dragThresholdPixels = 12f;
        [Tooltip("Degrees per second when holding Q or E to rotate the map view.")]
        [SerializeField, Min(1f)] private float rotationSpeed = 60f;
        [Tooltip("Distance along the view axis; keeps the expanded district in front of the near clip plane when zoomed out.")]
        [SerializeField, Min(60f)] private float viewDistance = 100f;

        public Vector3 FocusPoint => focusPoint;
        private Camera _camera;
        private bool _skylineView;
        private float _mapPitch;
        private Vector2 _pointerStart;
        private Vector2 _previousPointer;
        private bool _pointerActive;
        private bool _pointerBlockedByUi;
        private bool _dragged;
        private bool _mouseActive;
        private bool _mouseBlockedByUi;
        private bool _mouseDragged;
        private float _previousPinchDistance;
        private bool _pinchBlockedByUi;
        private bool _modalInputBlocked;
        private bool _overlayInputBlocked;
        public void SetOverlayInputBlocked(bool blocked) { _overlayInputBlocked = blocked; if(blocked) CancelGesture(); }
        private bool _waitForPointerRelease;

        /// <summary>Modal panels cancel existing gestures and consume the closing tap before world input resumes.</summary>
        public void SetModalInputBlocked(bool blocked)
        {
            _modalInputBlocked = blocked;
            _waitForPointerRelease = true;
            CancelGesture();
        }

        private void CancelGesture()
        {
            _pointerActive = false;
            _mouseActive = false;
            _previousPinchDistance = 0f;
            _pointerBlockedByUi = false;
            _mouseBlockedByUi = false;
            _dragged = false;
            _mouseDragged = false;
        }

        /// <summary>Raised only for a valid tap that did not begin over UI or become a drag.</summary>
        public event Action<Vector2> Tapped;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            ApplyCameraPosition();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame && !_modalInputBlocked && !_overlayInputBlocked)
            {
                if (!_skylineView) _mapPitch = transform.eulerAngles.x;
                _skylineView = !_skylineView;
                _camera.orthographic = !_skylineView;
                _camera.fieldOfView = 60f;
                transform.rotation = Quaternion.Euler(_skylineView ? 15f : _mapPitch, transform.eulerAngles.y, 0);
                ApplyCameraPosition();
            }
            if (_overlayInputBlocked || _modalInputBlocked || _waitForPointerRelease)
            {
                CancelGesture();
                bool pressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
                if (Touchscreen.current != null)
                    foreach (var touch in Touchscreen.current.touches)
                        pressed |= touch.press.isPressed;
                if (!_modalInputBlocked && !pressed) _waitForPointerRelease = false;
                return;
            }
            if (Keyboard.current != null)
            {
                float turn = (Keyboard.current.eKey.isPressed ? 1f : 0f) -
                             (Keyboard.current.qKey.isPressed ? 1f : 0f);
                if (turn != 0f)
                {
                    transform.Rotate(Vector3.up, turn * rotationSpeed * Time.unscaledDeltaTime, Space.World);
                    ApplyCameraPosition();
                }
            }
            HandleTouch();
            if (Touchscreen.current == null || !Touchscreen.current.primaryTouch.press.isPressed)
                HandleMouse();
        }

        private void HandleTouch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return;

            int activeCount = 0;
            Vector2 first = default;
            Vector2 second = default;
            TouchControl firstTouch = null;
            foreach (TouchControl touch in screen.touches)
            {
                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    phase == UnityEngine.InputSystem.TouchPhase.Canceled ||
                    phase == UnityEngine.InputSystem.TouchPhase.None)
                    continue;

                if (activeCount == 0)
                {
                    first = touch.position.ReadValue();
                    firstTouch = touch;
                }
                else if (activeCount == 1)
                {
                    second = touch.position.ReadValue();
                }
                activeCount++;
            }

            if (activeCount >= 2)
            {
                if (_previousPinchDistance <= 0f)
                    _pinchBlockedByUi = _pointerBlockedByUi || OverUi(first) || OverUi(second);
                _pointerActive = false;
                _pointerBlockedByUi = true;
                _dragged = true;
                float distance = Vector2.Distance(first, second);
                if (_previousPinchDistance > 0f && !_pinchBlockedByUi)
                    Zoom(_camera.orthographicSize * _previousPinchDistance / Mathf.Max(1f, distance));
                _previousPinchDistance = distance;
                return;
            }

            _previousPinchDistance = 0f;
            if (activeCount == 0)
            {
                if (_pointerActive && !_pointerBlockedByUi && !_dragged)
                    Tapped?.Invoke(_previousPointer);
                _pointerActive = false;
                _pointerBlockedByUi = false;
                _dragged = false;
                return;
            }

            bool justPressed = firstTouch != null && firstTouch.press.wasPressedThisFrame;
            if (justPressed || !_pointerActive)
            {
                _pointerActive = true;
                _pointerStart = first;
                _previousPointer = first;
                _dragged = !justPressed;
                _pointerBlockedByUi = OverUi(first);
            }

            if (_pointerBlockedByUi)
            {
                _previousPointer = first;
                return;
            }

            if (!_dragged && Vector2.Distance(_pointerStart, first) >= dragThresholdPixels)
                _dragged = true;
            if (_dragged)
                Pan(_previousPointer, first);
            _previousPointer = first;
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _mouseActive = true;
                _pointerStart = position;
                _previousPointer = position;
                _mouseDragged = false;
                _mouseBlockedByUi = IsOverUi(null);
            }

            if (_mouseActive && mouse.leftButton.isPressed)
            {
                if (!_mouseDragged && Vector2.Distance(_pointerStart, position) >= dragThresholdPixels)
                    _mouseDragged = true;
                if (!_mouseBlockedByUi && _mouseDragged)
                    Pan(_previousPointer, position);
                _previousPointer = position;
            }

            if (_mouseActive && mouse.leftButton.wasReleasedThisFrame)
            {
                if (!_mouseBlockedByUi && !_mouseDragged)
                    Tapped?.Invoke(position);
                _mouseActive = false;
                _mouseBlockedByUi = false;
                _mouseDragged = false;
            }

            if (!IsOverUi(null))
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    Zoom(_camera.orthographicSize - scroll * wheelZoomSpeed * 0.01f);
            }
        }

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);
        private PointerEventData uiPointer;
        private bool IsOverUi(TouchControl touch) => OverUi(touch != null ? touch.position.ReadValue() : Mouse.current.position.ReadValue());
        private bool OverUi(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            if (uiPointer == null) uiPointer = new PointerEventData(EventSystem.current);
            uiPointer.position = position;
            uiHits.Clear();
            EventSystem.current.RaycastAll(uiPointer, uiHits);
            return uiHits.Count > 0;
        }
        private void Pan(Vector2 previous, Vector2 current)
        {
            if (!TryGroundPoint(previous, out Vector3 oldPoint) ||
                !TryGroundPoint(current, out Vector3 newPoint))
                return;
            Vector3 offset = oldPoint - newPoint;
            focusPoint.x = Mathf.Clamp(focusPoint.x + offset.x, -focusBounds.x, focusBounds.x);
            focusPoint.z = Mathf.Clamp(focusPoint.z + offset.z, -focusBounds.y, focusBounds.y);
            ApplyCameraPosition();
        }

        private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }

        private void Zoom(float size)
        {
            _camera.orthographicSize = Mathf.Clamp(size, zoomBounds.x, zoomBounds.y);
            if (_skylineView) _camera.fieldOfView = Mathf.Clamp(size * 2f, 30f, 85f);
            ApplyCameraPosition();
        }

        /// <summary>Sets the generated scene's initial view and movement limits.</summary>
        public void Configure(Vector3 initialFocus, Vector2 bounds, Vector2 zoom)
        {
            focusPoint = initialFocus;
            focusBounds = bounds;
            zoomBounds = zoom;
            if (_camera != null)
            {
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, zoomBounds.x, zoomBounds.y);
                ApplyCameraPosition();
            }
        }

        private void ApplyCameraPosition()
        {
            if (_camera == null)
                return;
            Vector3 direction = transform.rotation * Vector3.forward;
            float distance = _skylineView ? 80f : Mathf.Max(Mathf.Max(100f, viewDistance), _camera.orthographicSize * 4f);
            _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, distance + 4000f);
            transform.position = focusPoint - direction * distance;
        }

        /// <summary>Moves the map focus for a locally controlled vehicle without changing zoom or rotation.</summary>
        public void FollowWorldPoint(Vector3 point)
        {
            focusPoint = new Vector3(Mathf.Clamp(point.x, -focusBounds.x, focusBounds.x), 0,
                Mathf.Clamp(point.z, -focusBounds.y, focusBounds.y));
            ApplyCameraPosition();
        }
    }
}


