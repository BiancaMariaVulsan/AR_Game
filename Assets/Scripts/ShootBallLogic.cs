using UnityEngine;

public class ShootBallLogic : MonoBehaviour
{
    private Camera mainCamera;

    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private float shootForce = 500f;

    void Start()
    {
        mainCamera = FindObjectOfType<Camera>();
        UIButtonHandler.OnShootButtonClicked += OnShootButtonClicked;
    }

    private void OnShootButtonClicked()
    {
        // Get the position and rotation of the camera with a slight offset forward
        Vector3 spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * 0.1f;
        Quaternion spawnRotation = mainCamera.transform.rotation;

        GameObject ball = Instantiate(ballPrefab, spawnPosition, spawnRotation);
        Rigidbody ballRigidbody = ball.GetComponent<Rigidbody>();

        if (ballRigidbody != null)
        {
            ballRigidbody.AddForce(mainCamera.transform.forward * shootForce);
        }

        // Destroy the ball after 5 seconds to clean up
        Destroy(ball, 5f);
    }
}
