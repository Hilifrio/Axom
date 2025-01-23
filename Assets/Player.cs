using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEditor.Experimental.GraphView.GraphView;

[RequireComponent(typeof(Movement))]
public class Player : MonoBehaviour
{
    public static Action OnHitPerformed;
    Movement _movement;
    void Start()
    {
        _movement = GetComponent<Movement>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Hit(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            OnHitPerformed?.Invoke();
        }
    }
    public float GetCurrentAngle()
    {
        return _movement.GetCurrentAngle();
    }
}
