using UnityEngine;

public class HideBarOnStart : MonoBehaviour
{
    [SerializeField] private Canvas ARMagicBar;

    private void Start()
    {
        UIButtonHandler.OnStartButtonClicked += OnStartButtonClicked;
        UIButtonHandler.OnResetButtonClicked += OnResetButtonClicked;
    }

    private void OnStartButtonClicked()
    {
        ARMagicBar.enabled = false;
    }

    private void OnResetButtonClicked()
    {
        ARMagicBar.enabled = true;
    }
}
