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
        // private GameObject selectedObject;
        private TransformableObject selectedObject;
        
        private Camera mainCam;

        public static SelectObjectsLogic Instance;
        public event Action OnDeselectAll;
        public event Action OnSelectObject;

        public event Action<GameObject> OnGizmoSelected;

        // public event Action<Vector3> OnGizmoMoved;  

        private bool isManipulating;
        private bool isDragging; 
        
        private Vector3 initialPosition;
        private Vector3 axis;

        private bool disableTransformOptions;

        public bool DisableTransformOptions
        {
            get => disableTransformOptions;
            set => disableTransformOptions = value;
        }
        
        private void Awake()
        {
            if(Instance == null)
                Instance = this;
            
            
            mainCam = FindObjectOfType<Camera>();
        }

        public TransformableObject GetSelectedObject()
        {
            return selectedObject;
        }

        public void DeleteSelectedObject()
        {
            selectedObject.Delete();
        }


        void Update()
        {
            if(FindObjectOfType<EventSystem>() ==false) return;
            
            //If any object from the bar is selected 
            if (PlacementBarLogic.Instance.GetPlacementObject() != null) return;

            //If the player is currently manipulating a placed objects
            if (GlobalSelectState.Instance.GetTransformstate() ==
                SelectState.manipulating) return;
            
            if(disableTransformOptions) return;

            if (ARPlacementPlaneMesh.justPlaced)
            {
                ARPlacementPlaneMesh.justPlaced = false;
                return;
            }

            // CustomLog.Instance.InfoLog("Straight after disableTransformOptions");

#if UNITY_EDITOR
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                // 1. Get the screen position using the New Input System
                Vector2 screenPosition2D = Mouse.current.position.ReadValue();

                // Check if the input hit a UI element using the EventSystem
                // Note: EventSystem.current.IsPointerOverGameObject() implicitly works for mouse input,
                // so this check is valid here.
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    CustomLog.Instance.InfoLog("UI Hit was recognized");
                    return;
                }

                // 2. Pass the correct Vector2 position (or cast to Vector3 if needed) 
                //    to the Raycasting function
                TouchToRayCasting(screenPosition2D);
            }

#endif
#if UNITY_IOS || UNITY_ANDROID
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                // This replaces the error-causing line and defines a Vector2 to hold the screen position.
                // It is functionally equivalent to the legacy Input.GetTouch(0).position.
                Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();

                // Use the new Vector2 position for the UI raycast
                PointerEventData pointerData = new PointerEventData(EventSystem.current);
                pointerData.position = touchPosition; // Use the new variable touchPosition

                List<RaycastResult> results = new List<RaycastResult>();

                EventSystem.current.RaycastAll(pointerData, results);

                if (results.Count > 0)
                {
                    // We hit a UI element
                    Debug.Log("We hit an UI Element");
                    return;
                }

                // Pass the new Vector2 position to your Raycasting function
                TouchToRayCasting(touchPosition); // Use the new variable touchPosition
            }

#endif
        }

        //Shoot ray from the touch position 
        void TouchToRayCasting(Vector3 touch)
        {
            Ray ray = mainCam.ScreenPointToRay(touch);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                SelectObject(hit.collider.gameObject);
            }
            //Else, deselect all objects
            else 
            {
                OnDeselectAll?.Invoke();
                selectedObject = null;
            }
        }

        void SelectObject(GameObject objectThatWasHit)
        {
            //Select the specific Axis to move 
            if (isManipulating)
            {
                GizmoObject gizmoObject;
                if (objectThatWasHit.TryGetComponent(out gizmoObject))
                {
                    CustomLog.Instance.InfoLog("Gizmo was selected");
                    OnGizmoSelected?.Invoke(objectThatWasHit);
                }
                return;
            }
            
            CustomLog.Instance.InfoLog("SelectObject, Object that was hit" + 
                      objectThatWasHit.name);
            
            //Only one objects should be selected at a time
            TransformableObject obj;
            if (objectThatWasHit.GetComponentInParent<TransformableObject>())
            {
                OnDeselectAll?.Invoke();
                selectedObject = null;

                
                obj = objectThatWasHit.GetComponentInParent<TransformableObject>(); 
                selectedObject = obj;
                if (obj.GetSelected())
                {
                    obj.SetSelected(false);
                    return;
                }
                
                obj.SetSelected(true);
                OnSelectObject?.Invoke();
            }
            else
            {
                OnDeselectAll?.Invoke();
                selectedObject = null;
            }
        }
    }
}