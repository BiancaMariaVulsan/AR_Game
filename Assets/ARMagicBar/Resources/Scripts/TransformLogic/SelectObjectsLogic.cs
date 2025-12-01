using System;
using System.Collections.Generic;
using ARMagicBar.Resources.Scripts.Debugging;
using ARMagicBar.Resources.Scripts.Gizmo;
using ARMagicBar.Resources.Scripts.PlacementBar;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ARMagicBar.Resources.Scripts.TransformLogic
{
    /// <summary>
    /// Handles Raycasting and setting transformable Object to selected
    /// </summary>
    public class SelectObjectsLogic : MonoBehaviour
    {
        private TransformableObject selectedObject;
        private Camera mainCam;

        public static SelectObjectsLogic Instance;
        public event Action OnDeselectAll;
        public event Action OnSelectObject;
        public event Action<GameObject> OnGizmoSelected;

        private bool isManipulating;
        private bool disableTransformOptions;

        public bool DisableTransformOptions
        {
            get => disableTransformOptions;
            set => disableTransformOptions = value;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            // FIX: Use Camera.main to ensure we get the tagged AR Camera
            mainCam = Camera.main;
            if (mainCam == null) mainCam = FindObjectOfType<Camera>();
        }

        public TransformableObject GetSelectedObject() => selectedObject;

        public void DeleteSelectedObject()
        {
            if (selectedObject != null) selectedObject.Delete();
        }

        void Update()
        {
            if (EventSystem.current == null) return;

            // Logic gates to prevent selection during other actions
            if (PlacementBarLogic.Instance.GetPlacementObject() != null) return;
            if (GlobalSelectState.Instance.GetTransformstate() == SelectState.manipulating) return;
            if (disableTransformOptions) return;
            if (ARPlacementPlaneMesh.justPlaced)
            {
                ARPlacementPlaneMesh.justPlaced = false;
                return;
            }

            // --- Unified Input Check ---
            bool isPressed = false;
            Vector2 screenPos = Vector2.zero;

            // Editor Mouse
#if UNITY_EDITOR
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isPressed = true;
                screenPos = Mouse.current.position.ReadValue();
            }
#endif

            // Android Touch
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    isPressed = true;
                    screenPos = touch.position.ReadValue();
                }
            }

            // Execution
            if (isPressed)
            {
                // UI BLOCK CHECK
                PointerEventData pointerData = new PointerEventData(EventSystem.current);
                pointerData.position = screenPos;
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                // If we hit UI, stop. 
                // NOTE: Ensure your Background Panel has 'Raycast Target' UNCHECKED in the Inspector!
                if (results.Count > 0)
                {
                    return;
                }

                TouchToRayCasting(screenPos);
            }
        }

        //Shoot ray from the touch position 
        void TouchToRayCasting(Vector3 touch)
        {
            Ray ray = mainCam.ScreenPointToRay(touch);
            RaycastHit hit;

            // Physics Raycast against World Objects
            if (Physics.Raycast(ray, out hit))
            {
                SelectObject(hit.collider.gameObject);
            }
            else
            {
                // Tapped empty space (like the AR Plane) -> Deselect
                OnDeselectAll?.Invoke();
                selectedObject = null;
            }
        }

        void SelectObject(GameObject objectThatWasHit)
        {
            // Gizmo Handling
            if (isManipulating)
            {
                if (objectThatWasHit.TryGetComponent(out GizmoObject gizmoObject))
                {
                    OnGizmoSelected?.Invoke(objectThatWasHit);
                }
                return;
            }

            // Selection Logic
            TransformableObject obj = objectThatWasHit.GetComponentInParent<TransformableObject>();
            if (obj)
            {
                // Logic to toggle selection
                bool isSameObject = (selectedObject == obj);

                OnDeselectAll?.Invoke(); // Deselect previous

                if (isSameObject && obj.GetSelected())
                {
                    // If clicking the same object that is already selected, deselect it
                    selectedObject = null;
                    obj.SetSelected(false);
                }
                else
                {
                    // Select new object
                    selectedObject = obj;
                    obj.SetSelected(true);
                    OnSelectObject?.Invoke();
                }
            }
            else
            {
                OnDeselectAll?.Invoke();
                selectedObject = null;
            }
        }
    }
}