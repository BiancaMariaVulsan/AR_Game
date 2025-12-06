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

        private Rigidbody rb;
        private bool isHit = false;

        // FIX CS0115: Use Awake() for initialization
        protected void Awake()
        {
            // Note: TransformableObject.OnEnable() is inaccessible, so we handle setup here.
            rb = GetComponent<Rigidbody>();

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
    }
}