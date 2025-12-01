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
using UnityEngine.InputSystem.Controls;

namespace ARMagicBar.Resources.Scripts.PlacementBar
{
    public class ARPlacementPlaneMesh : MonoBehaviour
    {
        // Start is called before the first frame update

        private TransformableObject placementObject;
        private List<TransformableObject> instantiatedObjects = new();

        private Camera mainCam;
        private bool placed;


        [Header("Plane detection requires an AR Raycast Manager to be in the scene.")]
        [SerializeField] public ARPlacementMethod placementMethod;


        public static ARPlacementPlaneMesh Instance;
        public event Action<TransformableObject> OnSpawnObject;
        public static bool justPlaced = false;

        /// <summary>
        /// Can be used for example when not placing objects but using it for something else
        /// </summary>
        public event Action<(TransformableObject objectToSpawn, Vector2 screenPos)> OnSpawnObjectWithScreenPos;
        public event Action<(TransformableObject objectToSpawn, Vector3 hitPointPosition, Quaternion hitPointRotation)> OnSpawnObjectWithHitPosAndRotation;



        [Header("Use deactivate spawning, when you want to use the tap for something else, e.g. select a spell in the bar and cast it")]
        [SerializeField] bool deactivateSpawning;
        //If you want to use the touch event for something else, subscribe to these methods.
        public event Action<Vector3> OnHitScreenAt;
        public event Action<(Vector3 position, Quaternion normal)> OnHitPlaneOrMeshAt;
        public event Action<GameObject> OnHitMeshObject;


        public bool SetDeactivateSpawning
        {
            set;
            get;
        }

        private ARRaycastManager arRaycastManager;

        private void Awake()
        {
            if (!FindObjectOfType<EventSystem>())
            {
                Debug.LogError(AssetName.NAME + ": No event system found, please add an event system to the scene");
            }


            mainCam = FindObjectOfType<Camera>();
            Instance = this;
        }

        private void Start()
        {
            PreparePlacementMethod();

            CheckIfPlacementBarInScene();
        }

        private static void CheckIfPlacementBarInScene()
        {
            if (PlacementBarLogic.Instance == null)
            {
                CustomLog.Instance.InfoLog("Please add PlacementBarLogic to scene");
            }
        }


        void PreparePlacementMethod()
        {
            switch (placementMethod)
            {
                case ARPlacementMethod.planeDetection:
                    FindAndAssignRaycastManager();
                    break;
                case ARPlacementMethod.meshDetection:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void FindAndAssignRaycastManager()
        {
            if (placementMethod == ARPlacementMethod.meshDetection) return;
            CustomLog.Instance.InfoLog("Find assign Raycast => placemethod = " + placementMethod);

            arRaycastManager = FindObjectOfType<ARRaycastManager>();

            if (arRaycastManager == null)
            {
                Debug.LogError("Please add a AR raycast manager to your scene");
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (EventSystem.current == null) return;

            // --- Unified Input Handling for Editor (Mouse) and Device (Touch) ---

            bool isMouseClick = false;
            bool isTouchBegan = false;
            Vector2 screenPosition = Vector2.zero;

            // 1. Check for Mouse Input (for Editor only)
#if UNITY_EDITOR
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isMouseClick = true;
                screenPosition = Mouse.current.position.ReadValue();
            }
#endif

            // 2. Check for New Input System Touch (for Device and Editor)
            if (Touchscreen.current != null)
            {
                TouchControl primaryTouch = Touchscreen.current.primaryTouch;

                if (primaryTouch.press.wasPressedThisFrame) // Check if touch just started
                {
                    isTouchBegan = true;
                    screenPosition = primaryTouch.position.ReadValue();
                }
            }

            // 3. Process Input if detected (Either Mouse or Touch)
            if (isMouseClick || isTouchBegan)
            {
                // Check if the click/tap is over UI (Handles both mouse and touch via EventSystem)
                // Note: The new Input System also provides cleaner ways to handle UI hits,
                // but this re-uses the existing RaycastAll approach.

                PointerEventData pointerData = new PointerEventData(EventSystem.current);
                pointerData.position = screenPosition;

                List<RaycastResult> results = new List<RaycastResult>();

                EventSystem.current.RaycastAll(pointerData, results);

                if (results.Count > 0)
                {
                    CustomLog.Instance.InfoLog("Click / Tap over UI object (via EventSystem)");
                    return;
                }

                // If input is valid and not over UI, proceed with placement logic
                if (placementMethod == ARPlacementMethod.planeDetection)
                {
                    TouchToRayPlaneDetection(screenPosition);
                }
                else
                {
                    TouchToRayMeshing(screenPosition);
                    OnHitScreenAt?.Invoke(screenPosition);
                }

                OnSpawnObjectWithScreenPos?.Invoke((PlacementBarLogic.Instance.GetPlacementObject(), screenPosition));
            }
        }


        //Shoot ray against AR planes
        void TouchToRayPlaneDetection(Vector3 touch)
        {
            if (deactivateSpawning)
            {
                OnHitScreenAt?.Invoke(touch);
            }


            Ray ray = mainCam.ScreenPointToRay(touch);
            List<ARRaycastHit> hits = new();

            arRaycastManager.Raycast(ray, hits, TrackableType.Planes);
            CustomLog.Instance.InfoLog("ShootingRay Plane Detection, hitcount => " + hits.Count);
            if (hits.Count > 0)
            {
                InstantiateObjectAtPosition(hits[0].pose.position, Quaternion.LookRotation(Vector3.forward));
                // hits[0].pose.rotation);
            }
        }

        //Shoot ray against procedural AR Mesh
        void TouchToRayMeshing(Vector3 touch)
        {
            if (deactivateSpawning)
            {
                OnHitScreenAt?.Invoke(touch);
            }

            CustomLog.Instance.InfoLog("ShootingRay AR Meshing");

            Ray ray = mainCam.ScreenPointToRay(touch);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                OnHitMeshObject?.Invoke(hit.transform.gameObject);
                InstantiateObjectAtPosition(hit.point, hit.transform.rotation);
            }
        }


        /// <summary>
        /// Method to externally call to spawn an object, Invokes the OnSpawnObject event 
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void SpawnObjectAtPosition(Vector3 position, Quaternion rotation)
        {
            InstantiateObjectAtPosition(position, rotation);
        }

        //Instantiate Object at the raycast position
        void InstantiateObjectAtPosition(Vector3 position, Quaternion rotation)
        {
            CustomLog.Instance.InfoLog("Should instantiate object at position " + position);


            if (deactivateSpawning)
            {
                CustomLog.Instance.InfoLog("Preventing Spawning as deactivate Spawning is enabled");
                (Vector3, Quaternion) positionRotation = (position, rotation);
                OnHitPlaneOrMeshAt?.Invoke(positionRotation);
                OnSpawnObjectWithHitPosAndRotation?.Invoke((PlacementBarLogic.Instance.GetPlacementObject(), position, rotation));
                return;
            }

            placementObject = PlacementBarLogic.Instance.GetPlacementObject();
            CustomLog.Instance.InfoLog("PlacementBarLogic.Instance-GetPlacementObj => " + placementObject + " "
                + "functionReturns: " + PlacementBarLogic.Instance.GetPlacementObject());

            //Check if it should place
            if (placementObject == null) return;

            TransformableObject placeObject = Instantiate(placementObject);
            CustomLog.Instance.InfoLog("Placeobject => Instantiate " + placeObject.name);

            OnSpawnObject?.Invoke(placeObject);

            placeObject.transform.position = position;
            // placeObject.transform.rotation = rotation;

            instantiatedObjects.Add(placeObject);
            justPlaced = true;

            PlacementBarLogic.Instance.ClearObjectToInstantiate();
        }
    }

    public enum ARPlacementMethod
    {
        planeDetection,
        meshDetection
    }
}