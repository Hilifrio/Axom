using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Entrées du joueur : un seul bouton, appuyé ou relâché.
public class Player : MonoBehaviour
{
    public static Action OnHitPressed;
    public static Action OnHitReleased;

    [SerializeField] InputActionAsset _actions;
    InputActionAsset _instance;
    InputAction _hit;

    void OnEnable()
    {
        _instance = Instantiate(_actions);
        _hit = _instance.FindAction("Controls/Hit", true);
        _hit.started += OnStarted;
        _hit.canceled += OnCanceled;
        _hit.Enable();
    }

    void OnDisable()
    {
        _hit.started -= OnStarted;
        _hit.canceled -= OnCanceled;
        _hit.Disable();
        Destroy(_instance);
    }

    void OnStarted(InputAction.CallbackContext context)
    {
        OnHitPressed?.Invoke();
    }

    void OnCanceled(InputAction.CallbackContext context)
    {
        OnHitReleased?.Invoke();
    }
}
