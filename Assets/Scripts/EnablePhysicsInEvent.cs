using UnityEngine;

public class EnablePhysicsInEvent : MonoBehaviour
{

    [SerializeField] private Rigidbody rb;

    void Start()
    {
        UIButtonHandler.OnStartButtonClicked += EnablePhysics;
        rb.isKinematic = true;
    }

    private void EnablePhysics()
    {
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    private void OnDestroy()
    {
        UIButtonHandler.OnStartButtonClicked -= EnablePhysics;
    }
}
