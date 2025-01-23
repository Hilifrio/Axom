using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.PlayerSettings;

[RequireComponent(typeof(LineRenderer))]
public abstract class Zone : MonoBehaviour
{
    public float radius = 2f;                           // Rayon du cercle

    public Vector2 _pos = Vector2.zero;
    public int segments = 90;                           // Nombre de segments pour lisser l'arc
    protected LineRenderer lineRenderer;                  // Composant LineRenderer
    protected Vector3 circleCenter = new Vector3(0,0,0);  // Centre du cercle

    public static Action<int> OnZoneEnded = delegate { };

    protected bool _isPlayerInZone = false;
    protected Player _player;

    [SerializeField] protected int _index = -1;


    public void Init(float start, float end , float radius, int index, int segments = 50)
    {
        this._pos = new Vector2(start, end);
        this.radius = radius;
        this.segments = segments;
        this._index = index;
    }

    void Start()
    {
        // Assurez-vous que le LineRenderer est défini
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
    }
    protected virtual void Update()
    {
        DrawZone();
        if (IsInZone())
        {
            _isPlayerInZone = true;
        }
        else if (_isPlayerInZone && !IsInZone())
        {
            _isPlayerInZone = false;
            //OnZoneEnded?.Invoke();
            //Destroy(gameObject);
        }
        ZoneManager.Instance.UpdateZonePosition(_index, _pos);
    }

    public void DrawZone()
    {
        // Configurez le LineRenderer pour dessiner l'arc
        if (lineRenderer == null) return;

        // Nombre de segments pour dessiner le cercle
        lineRenderer.positionCount = segments + 1;

        float startAngle = _pos.x;
        float endAngle = _pos.y;

        // Calculer la taille réelle de l'arc
        float arcLength = (endAngle >= startAngle)
            ? endAngle - startAngle
            : 360f - startAngle + endAngle;

        // Calcul de l'angle par segment
        float angleStep = arcLength / segments;

        // Dessiner le cercle en prenant en compte le wrap-around
        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + i * angleStep;
            angle %= 360f; // S'assurer que l'angle reste dans [0, 360]

            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float y = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            lineRenderer.SetPosition(i, circleCenter + new Vector3(x, y, 0));
        }
    }
    public float GetCenter()
    {
        float arcLength = (_pos.y >= _pos.x) ? (_pos.y - _pos.x) : 360f - _pos.x + (_pos.y);
        float zoneCenter = (_pos.y - arcLength / 2) % 360;
        if (zoneCenter < 0) zoneCenter += 360f;
        return zoneCenter;
    }

    protected bool IsInZone()
    {
        float playerAngle = _player.GetCurrentAngle();

        // Normaliser les angles pour s'assurer qu'ils sont entre 0 et 360
        float normalizedStartAngle = Mathf.Repeat(_pos.x, 360f);
        float normalizedEndAngle = Mathf.Repeat(_pos.y, 360f);
        float normalizedPlayerAngle = Mathf.Repeat(playerAngle, 360f);

        // Si la zone ne dépasse pas 360° (cas classique)
        if (normalizedStartAngle <= normalizedEndAngle)
        {
            return normalizedPlayerAngle >= normalizedStartAngle && normalizedPlayerAngle <= normalizedEndAngle;
        }
        else
        {
            // Si la zone traverse 0° (exemple : startAngle = 340, endAngle = 20)
            return normalizedPlayerAngle >= normalizedStartAngle || normalizedPlayerAngle <= normalizedEndAngle;
        }

    }

    public void SetCenter(Vector3 center)
    {
        circleCenter = center;
    }

    public void SetPlayer(Player player)
    {
        _player = player;
    }

}
