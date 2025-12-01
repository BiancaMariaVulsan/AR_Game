using UnityEngine;
using UnityEngine.UI;
using System;

public class UIButtonHandler : MonoBehaviour
{
    [SerializeField] private Button UIStartButton;
    [SerializeField] private Button UIShootButton;
    [SerializeField] private Button UIResetButton;

    public static event Action OnStartButtonClicked;
    public static event Action OnShootButtonClicked;
    public static event Action OnResetButtonClicked;

    void Start()
    {
        UIStartButton.onClick.AddListener(HandleStartButtonClick);
        UIShootButton.onClick.AddListener(HandleShootButtonClick);
        UIResetButton.onClick.AddListener(HandleResetButtonClick);

        UIShootButton.gameObject.SetActive(false);
    }

    private void HandleStartButtonClick()
    {
        OnStartButtonClicked?.Invoke();
        UIStartButton.gameObject.SetActive(false);
        UIShootButton.gameObject.SetActive(true);
    }

    private void HandleShootButtonClick()
    {
        OnShootButtonClicked?.Invoke();
    }

    private void HandleResetButtonClick()
    {
        OnResetButtonClicked?.Invoke();
        UIStartButton.gameObject.SetActive(true);
        UIShootButton.gameObject.SetActive(false);
    }
}
