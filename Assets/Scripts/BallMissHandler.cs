using UnityEngine;
using System.Collections;
using ARTargetPractice.Core;

// Attach this script to your 'ballPrefab'
public class BallMissHandler : MonoBehaviour
{
    [Tooltip("Time in seconds the ball flies before it's considered a miss.")]
    [SerializeField] private float missTimeout = 1.5f;

    private bool successfulHit = false;

    void Start()
    {
        // Start the flight timeout timer immediately
        StartCoroutine(MissTimeoutCoroutine());
    }

    // Target.cs will call the target's OnHit() which prevents the miss penalty.
    void OnCollisionEnter(Collision collision)
    {
        // Check if we hit a target before it expires
        if (collision.gameObject.GetComponent<Target>() != null)
        {
            successfulHit = true;
            // The Target.OnHit() logic is responsible for scoring and physics application.
            // We destroy the miss handler immediately to stop the timer.
            Destroy(this);
        }
    }

    IEnumerator MissTimeoutCoroutine()
    {
        // Wait for the ball's max flight time
        yield return new WaitForSeconds(missTimeout);

        // If the ball timed out AND it never registered a successful hit
        if (!successfulHit)
        {
            if (GameManager.Instance != null)
            {
                // Apply the cooldown penalty
                GameManager.Instance.ApplyMissCooldown();
            }
        }

        // Clean up the ball object itself
        Destroy(gameObject);
    }
}