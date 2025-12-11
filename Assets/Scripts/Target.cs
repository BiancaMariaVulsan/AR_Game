using UnityEngine;
using ARMagicBar.Resources.Scripts.TransformLogic; // Base class namespace
using ARTargetPractice.Core; // GameManager namespace
using System.Collections;

namespace ARTargetPractice.Core
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Target : TransformableObject
    {
        [Header("Target Score/Physics")]
        [SerializeField] private int scoreValue = 10;
        [SerializeField] private float destroyDelay = 1.0f;
        [SerializeField] private GameObject destructionEffectPrefab; // Assign your particle effect here

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Time (in seconds) the target will exist *before* being hit. Game over if time runs out.")]
        [SerializeField] private float targetLifespan = 5.0f;

        private Rigidbody rb;
        private bool isHit = false;

        private float currentLifespan;
        private bool timerRunning = false;

        protected void Awake()
        {
            // Note: TransformableObject.OnEnable() is inaccessible, so we handle setup here.
            rb = GetComponent<Rigidbody>();

            currentLifespan = targetLifespan;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            // Starts frozen until StartGame is called
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            Debug.Log($"DEBUG TARGET 1: Target {gameObject.name} initialized. Ready to be hit.");
        }

        // This method is called by GameManager.StartGame()
        public void EnablePhysics()
        {
            if (rb != null)
            {
                // 1. Unfreeze and enable gravity
                rb.isKinematic = false;
                rb.useGravity = true;

                // 2. Force the Rigidbody to re-evaluate its position immediately (prevents sleeping/lag)
                rb.WakeUp();

                Debug.Log("DEBUG PHYSICS 2: Target is now dynamic. If it jumps, it's a placement/scale issue.");
            }
        }

        // CALLED BY BallCollisionHandler.OnCollisionEnter()
        public void OnHit(Vector3 hitDirection, float hitForce)
        {
            if (isHit) return;
            isHit = true;
            Debug.Log($"DEBUG TARGET 2: OnHit received from projectile. Score value: {scoreValue}");

            timerRunning = false;

            if (audioSource != null && audioSource.clip != null)
            {
                // Play the sound effect immediately on hit
                audioSource.Play();
            }
            else
            {
                Debug.LogWarning($"Target {gameObject.name} was hit, but no boom sound was played because AudioSource is missing or clip is not assigned.");
            }

            if (destructionEffectPrefab != null)
            {
                // Spawn effect right at hit location
                GameObject boom = Instantiate(
                    destructionEffectPrefab,
                    transform.position,
                    Quaternion.LookRotation(hitDirection)
                );

                // Ensure it doesn't inherit odd AR scale
                boom.transform.localScale = Vector3.one;

                // Force short lifetime particle systems to play
                var ps = boom.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();

                    float duration =
                        ps.main.duration +
                        ps.main.startLifetime.constantMax;

                    Destroy(boom, duration);
                }
                else
                {
                    Destroy(boom, 1f);
                }
            }


            // 1. Add Score
            if (GameManager.Instance != null)
            {
                Debug.Log($"DEBUG TARGET 3: Calling GameManager.AddScore({scoreValue}).");
                GameManager.Instance.AddScore(scoreValue);
            }
            else
            {
                Debug.LogError("FATAL ERROR: GameManager Instance is NULL! Check scene and GameManager.Awake().");
            }

            // 2. Apply Physics Force for realism (using the force passed by the ball)
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(hitDirection * hitForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * hitForce * 0.1f);
            }

            // 3. Prevent subsequent hits and destroy after a delay
            if (GetComponent<Collider>() != null)
                GetComponent<Collider>().enabled = false;

            Destroy(gameObject, destroyDelay);
        }

        // Update method to run the timer
        void Update()
        {
            // Only count down if the timer is explicitly running AND the target hasn't been hit yet
            if (GameManager.Instance != null &&
                            GameManager.Instance.CurrentState == GameState.Playing &&
                            !isHit)
            {
                currentLifespan -= Time.deltaTime;

                // Check for expiration
                if (currentLifespan <= 0f)
                {
                    isHit = true;
                    // Target expired logic (No score awarded, destroy object)
                    Debug.Log($"DEBUG TARGET: Target {gameObject.name} expired. Destroying.");
                    Destroy(gameObject);
                }
            }
        }
    }
}