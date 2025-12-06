using UnityEngine;

public class EnablePhysicsInEvent : MonoBehaviour
{

    [SerializeField] private Rigidbody rb;

    void Start()
    {
        UIButtonHandler.OnStartButtonClicked += EnablePhysics;
        rb.isKinematic = true;
    }

    public void EnablePhysics()
    {
        if (rb != null)
        {
            // 1. Unfreeze and enable gravity
            rb.isKinematic = false;
            rb.useGravity = true;

            // 2. Force the Rigidbody to re-evaluate its position immediately (prevents sleeping/lag)
            rb.WakeUp();
        }
    }

    private void OnDestroy()
    {
        UIButtonHandler.OnStartButtonClicked -= EnablePhysics;
    }
}
