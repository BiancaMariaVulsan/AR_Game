using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

public class ResetRbPositionOfObject : MonoBehaviour
{
    [SerializeField] Rigidbody rb;
    private Vector3 rbStartPosition;
    private Quaternion rbStartRotation;

    void Start()
    {
        UIButtonHandler.OnResetButtonClicked += ResetRbPositionOnButtonClicked;

        rbStartPosition = rb.transform.localPosition;
        rbStartRotation = rb.transform.localRotation;
    }

    private void ResetRbPositionOnButtonClicked()
    {
        rb.isKinematic = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.transform.localPosition = rbStartPosition;
        rb.transform.localRotation = rbStartRotation;
    }

    private void OnDestroy()
    {
        UIButtonHandler.OnResetButtonClicked -= ResetRbPositionOnButtonClicked;
    }

}
