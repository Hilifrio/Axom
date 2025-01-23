using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEditor.PlayerSettings;

public class HitZone : Zone
{
    // Start is called before the first frame update
    public static Action<int> OnZoneHitted = delegate { };
    public static Action<float> OnHitPerformed = delegate { };

    float perfectThreshold = 0.08f;
    float innerThreshold = 0.2f;
    float outerThreshold = 1.2f;

    public float _speed;
    private void OnEnable()
    {
        ZoneManager.OnZoneTargeted += OnHitPerformedHandler;
    }

    private void OnDisable()
    {
        ZoneManager.OnZoneTargeted -= OnHitPerformedHandler;
    }

    private void OnDestroy()
    {
        OnZoneEnded?.Invoke(_index);
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();

        // Déplacement des angles en fonction de la vitesse
        float delta = Time.deltaTime * _speed;

        // Mettre à jour les positions angulaires en conservant la plage [0, 360[
        _pos.x = (_pos.x + delta) % 360f;
        _pos.y = (_pos.y + delta) % 360f;

        // Si l'angle devient négatif (vitesse négative), corrigez-le
        if (_pos.x < 0) _pos.x += 360f;
        if (_pos.y < 0) _pos.y += 360f;

        Vector2 fixedPosition = ZoneManager.Instance.ResolveOverlap(_index);

        if (fixedPosition != Vector2.zero && _speed != 0)
        {
            _speed *= -1;
            _pos = fixedPosition;
        }
    }

    private void OnHitPerformedHandler(int index)
    {
        if(index != _index)
            return;

        float playerAngle = _player.GetCurrentAngle();
        float zoneSize = (_pos.y >= _pos.x) ? (_pos.y - _pos.x) : 360f - _pos.x + (_pos.y);
        float zoneCenter = GetCenter();
        float distanceToCenter = Mathf.Min(
        Mathf.Abs(playerAngle - zoneCenter),             // Distance directe
        360f - Mathf.Abs(playerAngle - zoneCenter)      // Distance circulaire opposée
        );
        int score = 0;

        if (distanceToCenter < zoneSize * perfectThreshold)
        {
            Debug.Log("Perfect");
            score = 5;
        }
        else if (distanceToCenter < zoneSize * innerThreshold)
        {
            Debug.Log("Great");
            score = 3;
        }
        else if (distanceToCenter < zoneSize * 0.8f)
        {
            Debug.Log("Good");
            score = 1;
        }
        else if (distanceToCenter < zoneSize * outerThreshold)
        {
            Debug.Log("Missed");
            score = 0;
        }
        else
        {
            Debug.Log("Out of range");
            return;
        }

        OnZoneHitted?.Invoke(score);
        Destroy(gameObject);

    }

    public void SetSpeed(float speed)
    {
       _speed = speed;
    }
}
