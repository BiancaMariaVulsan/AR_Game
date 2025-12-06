using UnityEngine;
using System.Collections.Generic;
using ARMagicBar.Resources.Scripts.PlacementBar;
using ARMagicBar.Resources.Scripts.TransformLogic;
using UnityEngine.XR.ARFoundation;
using TMPro;

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

        [Header("UI References")]
        [SerializeField] private GameObject placementBarUI;
        [SerializeField] private GameObject gameHUDUI;
        [SerializeField] private GameObject gameOverUI;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI finalScoreText;

        [Header("System References")]
        [SerializeField] private MonoBehaviour selectObjectLogic;

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
            if (CurrentState == GameState.Playing)
            {
                currentTime -= Time.deltaTime;
                UpdateHUD();

                if (currentTime <= 0)
                {
                    EndGame();
                }
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