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
using ARTargetPractice.Core; // Link to GameManager

namespace ARMagicBar.Resources.Scripts.PlacementBar
{
    public class ARPlacementPlaneMesh : MonoBehaviour
    {
        [SerializeField] private Camera mainCam;
        [SerializeField] public ARPlacementMethod placementMethod;
        [SerializeField] bool deactivateSpawning;

        public static ARPlacementPlaneMesh Instance;
        public static bool justPlaced = false; // Required by SelectObjectsLogic.cs

        public event Action<TransformableObject> OnSpawnObject;

        // Event pointers required by existing scripts (omitted definition for brevity)
        public event Action<Vector3> OnHitScreenAt;
        public event Action<GameObject> OnHitMeshObject;
        public event Action<(TransformableObject objectToSpawn, Vector2 screenPos)> OnSpawnObjectWithScreenPos;
        public event Action<(Vector3 position, Quaternion normal)> OnHitPlaneOrMeshAt;
        public event Action<(TransformableObject objectToSpawn, Vector3 hitPointPosition, Quaternion hitPointRotation)> OnSpawnObjectWithHitPosAndRotation;

        private ARRaycastManager arRaycastManager;
        private ARPlaneManager arPlaneManager;
        private ARAnchorManager arAnchorManager;
        private const float MIN_PLANE_AREA = 0.04f;

        // Game Logic Variables
        public ARAnchor WorldAnchor { get; set; }
        public bool IsWorldAnchored { get; set; } = false;
        public bool SetDeactivateSpawning { set; get; }

        private void Awake()
        {
            Instance = this;
            if (mainCam == null) mainCam = Camera.main;
        }

        private void Start()
        {
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
            arPlaneManager = FindObjectOfType<ARPlaneManager>();
            arAnchorManager = FindObjectOfType<ARAnchorManager>();
        }

        void Update()
        {
            if (EventSystem.current == null) return;

            // FIX/CLEANUP: We skip all tap input if the game is Playing, 
            //                 because the Shoot Button/Projectile handles the shot.
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                return;
            }

            // If NOT Playing (Placing Anchor or PreGameSetup), allow placement taps:
            HandlePlacementInput();
        }

        void HandlePlacementInput()
        {
            bool isPressed = false;
            Vector2 screenPos = Vector2.zero;
            int pointerId = -1;

            // Input Detection (Mouse or Touch)
#if UNITY_EDITOR
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isPressed = true; screenPos = Mouse.current.position.ReadValue();
            }
#endif
            if (!isPressed && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                isPressed = true;
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
                pointerId = Touchscreen.current.primaryTouch.touchId.value;
            }

            if (isPressed)
            {
                // UI Block
                PointerEventData pData = new PointerEventData(EventSystem.current) { position = screenPos, pointerId = pointerId };
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pData, results);
                if (results.Count > 0) return;

                // Anchoring Logic
                if (!IsWorldAnchored && placementMethod == ARPlacementMethod.planeDetection)
                {
                    TryPlaceAnchor(screenPos);
                }
                else
                {
                    // Standard Placement Logic
                    if (placementMethod == ARPlacementMethod.planeDetection)
                        TouchToRayPlaneDetection(screenPos);
                    else
                        TouchToRayMeshing(screenPos);

                    OnHitScreenAt?.Invoke(screenPos);
                    OnSpawnObjectWithScreenPos?.Invoke((PlacementBarLogic.Instance.GetPlacementObject(), screenPos));
                }
            }
        }

        void TryPlaceAnchor(Vector2 screenPos)
        {
            Ray ray = mainCam.ScreenPointToRay(screenPos);
            List<ARRaycastHit> hits = new List<ARRaycastHit>();

            if (arRaycastManager.Raycast(ray, hits, TrackableType.Planes))
            {
                Pose hitPose = hits[0].pose;

                // We assume the first hit is the best hit and that it's an ARPlane.
                ARPlane arPlane = hits[0].trackable.GetComponent<ARPlane>();

                // Filter out small planes (often unstable or false positives)
                if (arPlane != null)
                {
                    float planeArea = arPlane.size.x * arPlane.size.y;
                    if (planeArea < MIN_PLANE_AREA)
                    {
                        Debug.LogWarning($"DEBUG PLACEMENT: Rejected placement on plane with area: {planeArea:F2} m^2 (Too small).");
                        return; // Exit and ignore the touch
                    }
                }

                // 1. CREATE LOGIC ANCHOR FIRST (Fixes the WorldAnchor is NULL error)
                GameObject anchorGO = new GameObject("ARGameAnchor");
                anchorGO.transform.position = hitPose.position;
                anchorGO.transform.rotation = hitPose.rotation;

                WorldAnchor = anchorGO.AddComponent<ARAnchor>();

                if (WorldAnchor != null)
                {
                    IsWorldAnchored = true;
                    DisablePlaneDetection();

                    // 2. NOW INSTANTIATE VISUAL (It will be parented correctly inside InstantiateObjectAtPosition)
                    InstantiateObjectAtPosition(hitPose.position, Quaternion.LookRotation(Vector3.forward));

                    // The first object (the anchor object) should also clear the placement bar selection
                    PlacementBarLogic.Instance.ClearObjectToInstantiate();
                    Debug.Log("DEBUG PLACEMENT: World Anchor set and first object placed as child of anchor.");
                }
            }
        }

        public void DisablePlaneDetection()
        {
            if (arPlaneManager != null)
            {
                arPlaneManager.enabled = false;
                foreach (var plane in arPlaneManager.trackables) plane.gameObject.SetActive(false);
            }
        }

        public void EnablePlaneDetection()
        {
            if (arPlaneManager != null)
            {
                arPlaneManager.enabled = true;
                foreach (var plane in arPlaneManager.trackables) plane.gameObject.SetActive(true);
            }
        }

        void TouchToRayPlaneDetection(Vector2 touch)
        {
            if (deactivateSpawning) OnHitScreenAt?.Invoke(touch);

            Ray ray = mainCam.ScreenPointToRay(touch);
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            if (arRaycastManager.Raycast(ray, hits, TrackableType.Planes))
            {
                // Check Plane Area on Subsequent Placements ---
                ARPlane arPlane = hits[0].trackable.GetComponent<ARPlane>();
                if (arPlane != null)
                {
                    float planeArea = arPlane.size.x * arPlane.size.y;
                    if (planeArea < MIN_PLANE_AREA)
                    {
                        Debug.LogWarning($"DEBUG PLACEMENT: Rejected placement on plane with area: {planeArea:F2} m^2 (Too small for stable placement).");
                        return; // Ignore placement
                    }
                }
                InstantiateObjectAtPosition(hits[0].pose.position, Quaternion.LookRotation(Vector3.forward));
            }
        }

        void TouchToRayMeshing(Vector2 touch)
        {
            if (deactivateSpawning) OnHitScreenAt?.Invoke(touch);

            Ray ray = mainCam.ScreenPointToRay(touch);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                OnHitMeshObject?.Invoke(hit.transform.gameObject);
                InstantiateObjectAtPosition(hit.point, hit.transform.rotation);
            }
        }

        void InstantiateObjectAtPosition(Vector3 pos, Quaternion rot)
        {
            if (deactivateSpawning)
            {
                OnHitPlaneOrMeshAt?.Invoke((pos, rot));
                OnSpawnObjectWithHitPosAndRotation?.Invoke((PlacementBarLogic.Instance.GetPlacementObject(), pos, rot));
                return;
            }

            var objToPlace = PlacementBarLogic.Instance.GetPlacementObject();
            if (objToPlace == null) return;

            TransformableObject newObj = Instantiate(objToPlace, pos, rot);

            if (WorldAnchor != null)
            {
                newObj.transform.parent = WorldAnchor.transform;
                Debug.Log($"DEBUG PLACEMENT: Target {newObj.name} parented to WorldAnchor. All targets should now be stable relative to the world anchor.");
            }
            else
            {
                // This log should only appear for the first object placement!
                Debug.LogWarning("DEBUG PLACEMENT: Target placed but WorldAnchor is NULL. This should only happen for the VERY FIRST anchor placement.");
            }
            justPlaced = true;

            OnSpawnObject?.Invoke(newObj);
        }
        public enum ARPlacementMethod { planeDetection, meshDetection }
    }
}