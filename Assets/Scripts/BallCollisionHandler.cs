// NEW FILE: BallCollisionHandler.cs (Attach to your ballPrefab)
using UnityEngine;
using ARTargetPractice.Core; // For Target.cs and GameManager

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class BallCollisionHandler : MonoBehaviour
{
    private bool hasHitTarget = false;
    private const float CleanupDelay = 3.0f;

    private void Start()
    {
        Debug.Log("DEBUG BALL 1: Ball instantiated and cleanup timer started.");
        Destroy(gameObject, CleanupDelay);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"DEBUG BALL 2: Ball collided with {collision.gameObject.name}. Checking for Target component...");

        if (hasHitTarget) return;

        Target target = collision.gameObject.GetComponentInParent<Target>();

        if (target != null)
        {
            hasHitTarget = true;
            Debug.Log("DEBUG BALL 3: Target found! Calling Target.OnHit()...");

            // Calculate force for target's physics reaction
            Rigidbody ballRb = GetComponent<Rigidbody>();
            Vector3 hitDirection = ballRb != null ? ballRb.linearVelocity.normalized : Vector3.forward;
            float hitForce = ballRb != null ? ballRb.linearVelocity.magnitude * ballRb.mass : 15f;

            // Call the Target's OnHit method
            target.OnHit(hitDirection, hitForce);

            Debug.Log("DEBUG BALL 4: Target hit processed. Destroying ball.");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log($"DEBUG BALL 3 (MISS): Collided with non-target object: {collision.gameObject.name}. Score will not increment.");
        }
    }
}