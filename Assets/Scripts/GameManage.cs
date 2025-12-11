using UnityEngine;
using System.Collections.Generic;
using ARMagicBar.Resources.Scripts.PlacementBar;
using ARMagicBar.Resources.Scripts.TransformLogic;
using UnityEngine.XR.ARFoundation;
using TMPro;
using UnityEngine.XR.ARSubsystems;

namespace ARTargetPractice.Core
{
    public enum GameState
    {
        PlacingWorldAnchor,
        PreGameSetup,
        Playing,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("Game Settings")]
        [SerializeField] private float sessionDuration = 60.0f;
        public GameState CurrentState { get; private set; } = GameState.PlacingWorldAnchor;
        public int Score { get; private set; } = 0;
        private float currentTime;

        [Header("Shooting Cooldown")]
        [Tooltip("Time delay (in seconds) applied after a projectile misses all targets.")]
        [SerializeField] private float missCooldownDuration = 0.5f;
        private float cooldownTimer = 0f;
        public bool IsOnCooldown => cooldownTimer > 0f;

        [Tooltip("Amount of time (in seconds) to subtract for each missed shot.")]
        [SerializeField] private float missTimePenalty = 10.0f;

        [Header("UI References")]
        [SerializeField] private GameObject placementBarUI;
        [SerializeField] private GameObject gameHUDUI;
        [SerializeField] private GameObject gameOverUI;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI finalScoreText;

        [Header("System References")]
        [SerializeField] private MonoBehaviour selectObjectLogic;
        [SerializeField] private ARSession arSession;

        private bool wasTrackingLost = false;

        public bool IsPlaying => CurrentState == GameState.Playing;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            if (ARPlacementPlaneMesh.Instance != null)
            {
                ARPlacementPlaneMesh.Instance.OnSpawnObject += OnWorldAnchorPlaced;
            }

            // Hook into your existing buttons
            UIButtonHandler.OnStartButtonClicked += StartGame;
            UIButtonHandler.OnResetButtonClicked += RestartGame;

            SetState(GameState.PlacingWorldAnchor);
        }

        void Update()
        {
            if (CurrentState != GameState.GameOver)
            {
                CheckTrackingStateAndStabilize();
            }

            if (CurrentState == GameState.Playing)
            {
                currentTime -= Time.deltaTime;
                UpdateHUD();

                if (currentTime <= 0)
                {
                    EndGame();
                }
            }

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer < 0f)
                {
                    cooldownTimer = 0f;
                    // Optional: Add visual/audio feedback that shooting is available again
                }
            }
        }

        public void ApplyMissCooldown()
        {
            if (CurrentState == GameState.Playing)
            {
                currentTime -= missTimePenalty;
                Debug.Log($"DEBUG PENALTY: Miss detected. Subtracting {missTimePenalty}s. Time left: {currentTime:F2}");

                // Ensure the game ends immediately if the penalty drops time to zero or below
                if (currentTime <= 0)
                {
                    EndGame();
                }
                UpdateHUD();
            }
        }

        private void CheckTrackingStateAndStabilize()
        {
            // Check if the AR subsystem is initialized and running
            if (arSession == null || arSession.subsystem == null) return;

            // Get current tracking status
            TrackingState currentState = arSession.subsystem.trackingState;

            // Determine if tracking is poor (usually Limited or None)
            bool trackingIsPoor = currentState != TrackingState.Tracking;

            if (trackingIsPoor && !wasTrackingLost)
            {
                // Tracking was stable, but now it's poor -> FREEZE DYNAMIC OBJECTS
                wasTrackingLost = true;
                Debug.LogWarning($"STABILITY: AR Tracking degraded ({currentState}). Freezing dynamic objects.");

                // Freeze the physics of ALL targets
                Target[] allTargets = FindObjectsByType<Target>(FindObjectsSortMode.None);
                foreach (var target in allTargets)
                {
                    Rigidbody rb = target.GetComponent<Rigidbody>();
                    // Only targets already in a dynamic state should be frozen
                    if (rb != null && !rb.isKinematic)
                    {
                        rb.isKinematic = true;
                    }
                }
            }
            else if (!trackingIsPoor && wasTrackingLost)
            {
                // Tracking was poor, but is now stable -> RESUME DYNAMIC OBJECTS
                wasTrackingLost = false;
                Debug.Log($"STABILITY: AR Tracking recovered ({currentState}). Resuming dynamic objects.");

                // Re-enable physics for targets ONLY if the game is actively playing
                if (CurrentState == GameState.Playing)
                {
                    EnableTargetPhysics(); // This method already exists and sets kinematic = false
                }

                // If the game is in PreGameSetup, the targets remain kinematic.
            }
        }

        // --- State Logic ---

        public void SetState(GameState newState)
        {
            CurrentState = newState;
            Debug.Log("Game State: " + newState);
            UpdateUI();
            UpdateSystems();
        }

        private void UpdateUI()
        {
            bool showBar = (CurrentState == GameState.PlacingWorldAnchor || CurrentState == GameState.PreGameSetup);
            if (placementBarUI != null) placementBarUI.SetActive(showBar);

            if (gameHUDUI != null) gameHUDUI.SetActive(CurrentState == GameState.Playing);

            if (gameOverUI != null) gameOverUI.SetActive(CurrentState == GameState.GameOver);
        }

        private void UpdateSystems()
        {
            // Disable moving/selecting objects while playing
            if (selectObjectLogic != null)
                selectObjectLogic.enabled = (CurrentState != GameState.Playing && CurrentState != GameState.GameOver);
        }

        // NEW HELPER METHOD: Finds all targets and calls EnablePhysics()
        private void EnableTargetPhysics()
        {
            // This line finds all active game objects that have the Target script attached
            Target[] allTargets = FindObjectsByType<Target>(FindObjectsSortMode.None);

            Debug.Log($"DEBUG PHYSICS: Found {allTargets.Length} targets. Activating physics.");

            foreach (var target in allTargets)
            {
                // Calls the public method in Target.cs which sets isKinematic = false
                target.EnablePhysics();
            }
        }

        // 1. Called automatically when user places the FIRST object (the anchor)
        private void OnWorldAnchorPlaced(TransformableObject obj)
        {
            if (CurrentState == GameState.PlacingWorldAnchor)
            {
                SetState(GameState.PreGameSetup);
            }
        }

        // 2. Called by UI Start Button
        public void StartGame()
        {
            if (CurrentState == GameState.PreGameSetup || CurrentState == GameState.PlacingWorldAnchor)
            {
                Score = 0;
                currentTime = sessionDuration;

                // FIX: Call the helper method to unfreeze all targets!
                EnableTargetPhysics();

                SetState(GameState.Playing);
            }
        }

        // 3. Called when Timer ends
        public void EndGame()
        {
            SetState(GameState.GameOver);
            if (finalScoreText != null) finalScoreText.text = "Final Score: " + Score;
        }

        // 4. Called by UI Reset Button
        public void RestartGame()
        {
            SetState(GameState.PlacingWorldAnchor);
            Score = 0;

            // Reset AR Anchor logic
            if (ARPlacementPlaneMesh.Instance != null)
            {
                if (ARPlacementPlaneMesh.Instance.WorldAnchor != null)
                {
                    Destroy(ARPlacementPlaneMesh.Instance.WorldAnchor.gameObject);
                    ARPlacementPlaneMesh.Instance.WorldAnchor = null;
                }
                ARPlacementPlaneMesh.Instance.IsWorldAnchored = false;
                ARPlacementPlaneMesh.Instance.EnablePlaneDetection();
            }
        }

        // --- Scoring ---
        public void AddScore(int amount)
        {
            if (CurrentState == GameState.Playing)
            {
                Debug.Log($"DEBUG MANAGER 1: Score SUCCESS! Adding {amount}. Old Score: {Score}");
                Score += amount;
                UpdateHUD();
            }
            else
            {
                Debug.LogError($"DEBUG MANAGER 1: Score FAILED. GameState is {CurrentState}, not Playing. Score was rejected.");
            }
        }

        private void UpdateHUD()
        {
            if (scoreText != null) scoreText.text = "Score: " + Score;

            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(currentTime / 60F);
                int seconds = Mathf.FloorToInt(currentTime % 60F);
                timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }
    }
}