using System;
using System.Collections.Generic;
using ARMagicBar.Resources.Scripts.Debugging;
using ARMagicBar.Resources.Scripts.Other;
using ARMagicBar.Resources.Scripts.TransformLogic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;

namespace ARMagicBar.Resources.Scripts.PlacementBar
{
    public class ARPlacementPlaneMesh : MonoBehaviour
    {
        private TransformableObject placementObject;
        private List<TransformableObject> instantiatedObjects = new();
        [SerializeField] private Camera mainCam;
        private bool placed;

        [SerializeField] public ARPlacementMethod placementMethod;
        [SerializeField] bool deactivateSpawning;

        public static ARPlacementPlaneMesh Instance;
        public event Action<TransformableObject> OnSpawnObject;
        public static bool justPlaced = false;

        public event Action<(TransformableObject objectToSpawn, Vector2 screenPos)> OnSpawnObjectWithScreenPos;
        public event Action<(TransformableObject objectToSpawn, Vector3 hitPointPosition, Quaternion hitPointRotation)> OnSpawnObjectWithHitPosAndRotation;
        public event Action<Vector3> OnHitScreenAt;
        public event Action<(Vector3 position, Quaternion normal)> OnHitPlaneOrMeshAt;
        public event Action<GameObject> OnHitMeshObject;

        private ARRaycastManager arRaycastManager;

        public bool SetDeactivateSpawning { set; get; }

        private void Awake()
        {
            if (!FindObjectOfType<EventSystem>())
            {
                Debug.LogError(AssetName.NAME + ": No event system found...");
            }

            Instance = this;

            // Fallback logic
            if (mainCam == null)
            {
                mainCam = Camera.main;
                if (mainCam == null) mainCam = FindObjectOfType<Camera>();
            }
        }

        private void Start()
        {
            if (placementMethod == ARPlacementMethod.planeDetection)
            {
                arRaycastManager = FindObjectOfType<ARRaycastManager>();
            }
        }

        void Update()
        {
            if (EventSystem.current == null) return;

            bool isPressed = false;
            Vector2 screenPos = Vector2.zero;
            // Initialize pointerId. -1 is commonly used for mouse input.
            int pointerId = -1;

            // Editor Mouse
#if UNITY_EDITOR
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isPressed = true;
                screenPos = Mouse.current.position.ReadValue();
                // pointerId remains -1 for mouse.
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
                    // Get the actual touch ID. 
                    // This is essential for reliable UI detection on mobile when using Input System.
                    pointerId = touch.touchId.value;
                }
            }

            if (isPressed)
            {
                // UI Block Check: Use the native, reliable check with the pointer ID.
                if (EventSystem.current.IsPointerOverGameObject(pointerId))
                {
                    // Hit UI, stop world placement logic immediately.
                    return;
                }

                // If code reaches here, the tap was NOT on UI, proceed with Placement Logic.

                // Placement Logic
                if (placementMethod == ARPlacementMethod.planeDetection)
                {
                    TouchToRayPlaneDetection(screenPos);
                }
                else
                {
                    TouchToRayMeshing(screenPos);
                    OnHitScreenAt?.Invoke(screenPos);
                }

                OnSpawnObjectWithScreenPos?.Invoke((PlacementBarLogic.Instance.GetPlacementObject(), screenPos));
            }
        }

        void TouchToRayPlaneDetection(Vector2 touch)
        {
            if (deactivateSpawning)
            {
                OnHitScreenAt?.Invoke(touch);
            }

            // Ensure we use the correct camera for the ray
            Ray ray = mainCam.ScreenPointToRay(touch);
            List<ARRaycastHit> hits = new();

            if (arRaycastManager.Raycast(ray, hits, TrackableType.Planes))
            {
                // Hit found
                Pose hitPose = hits[0].pose;
                InstantiateObjectAtPosition(hitPose.position, Quaternion.LookRotation(Vector3.forward));
            }
        }

        void TouchToRayMeshing(Vector2 touch)
        {
            if (deactivateSpawning)
            {
                OnHitScreenAt?.Invoke(touch);
            }

            Ray ray = mainCam.ScreenPointToRay(touch);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                OnHitMeshObject?.Invoke(hit.transform.gameObject);
                InstantiateObjectAtPosition(hit.point, hit.transform.rotation);
            }
        }

        public void SpawnObjectAtPosition(Vector3 position, Quaternion rotation)
        {
            InstantiateObjectAtPosition(position, rotation);
        }

        void InstantiateObjectAtPosition(Vector3 position, Quaternion rotation)
        {
            if (deactivateSpawning)
            {
                OnHitPlaneOrMeshAt?.Invoke((position, rotation));
                OnSpawnObjectWithHitPosAndRotation?.Invoke((PlacementBarLogic.Instance.GetPlacementObject(), position, rotation));
                return;
            }

            placementObject = PlacementBarLogic.Instance.GetPlacementObject();

            if (placementObject == null) return;

            TransformableObject placeObject = Instantiate(placementObject);
            OnSpawnObject?.Invoke(placeObject);

            placeObject.transform.position = position;

            instantiatedObjects.Add(placeObject);
            justPlaced = true;

            PlacementBarLogic.Instance.ClearObjectToInstantiate();
        }
    }

    public enum ARPlacementMethod { planeDetection, meshDetection }
}