using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Random = UnityEngine.Random;

public class ZoneManager : MonoSingleton<ZoneManager>
{
    // Start is called before the first frame update
    public float minZoneSize = 20f;
    public float maxZoneSize = 90f;
    public GameObject hitZonePrefab;
    private int _multiplicator = 1;
    private int _score = 0;

    private int _scoreThreshold = 10;
    private int _currentScoreIndex = 0;
    [SerializeField] private int _nbMaxZone = 1;
    [SerializeField] private int _nbCurrentZone = 0;

    [SerializeField] private Vector2[] _zonePositions = new Vector2[0];
    [SerializeField] int _currentZoneIndex = 0;

    [SerializeField]
    private Player _player;
    private float minDistanceFromPlayer = 20;
    private float maxDistanceFromPlayer = 140;

    [SerializeField]
    private List<int> scoreThresholds = new List<int>(); 
    [SerializeField]
    private List<int> zoneLimits = new List<int>();

    public static Action<int> OnZoneTargeted = delegate { };
    private void OnEnable()
    {
        HitZone.OnZoneHitted += OnZoneHittedHandler;
        Player.OnHitPerformed += OnHitPerformedHandler;
        Zone.OnZoneEnded += OnZoneEndedHandler;
    }
    private void OnDisable()
    {
        HitZone.OnZoneHitted -= OnZoneHittedHandler;
        Player.OnHitPerformed -= OnHitPerformedHandler;
        Zone.OnZoneEnded -= OnZoneEndedHandler;
    }

    void Start()
    {
        _zonePositions = new Vector2[_nbMaxZone];
        CreateZone();
    }

    // Update is called once per frame
    void Update()
    {
    }

    [ContextMenu("Create Zone")]
    void CreateZone()
    {
        if(_nbCurrentZone >= _nbMaxZone)
            return;

        List<Vector2> availableIntervals = GetAvailableIntervals();

        if (availableIntervals.Count == 0)
        {
            Debug.LogWarning("No available space to create a new zone.");
            return;
        }

        Vector2 chosenInterval = availableIntervals[Random.Range(0, availableIntervals.Count)];

        float startAngle = Random.Range(chosenInterval.x, chosenInterval.y - minZoneSize);
        float zoneSize = Random.Range(minZoneSize, minZoneSize);
        float endAngle = startAngle + zoneSize;

        float randomOffset = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);

        //float startAngle = _player.GetCurrentAngle() + randomOffset;
        //float zoneSize = Random.Range(minZoneSize, maxZoneSize);
        //float endAngle = startAngle + zoneSize;

        startAngle %= 360f;

        _zonePositions[_currentZoneIndex] = new Vector2(startAngle, endAngle);

        HitZone hitzone = GameObject.Instantiate(hitZonePrefab).GetComponent<HitZone>();
        hitzone.Init(startAngle, endAngle, 2f, _currentZoneIndex);
        hitzone.SetCenter(Vector3.zero);
        hitzone.SetPlayer(_player);
        hitzone.DrawZone();

        if(_score>_scoreThreshold)
        {
            if(Random.value>0.5f)
            {

                float speed = Random.Range(10, 50);
                if (Random.value > 0.5f)
                    hitzone.SetSpeed(speed);
                else
                    hitzone.SetSpeed(speed * -1);
            }
        }
        _nbCurrentZone++;
    }

    void OnZoneHittedHandler(int score)
    {
        _score += _multiplicator * score;
        Debug.Log("Score: " + _score);
    }

    void OnZoneEndedHandler(int index)
    {

        for (int i = 0; i < _zonePositions.Length; i++)
        {
            if (i == index)
            {
                _zonePositions[i] = Vector2.zero;
                _currentZoneIndex = i;
                _nbCurrentZone--;
                UpdateZoneCount();
                break;
            }
        }
    }

    void UpdateZoneCount()
    {
        for (int i = _currentScoreIndex; i < scoreThresholds.Count; i++)
        {
            if (_score >= scoreThresholds[i])
            {
                _nbMaxZone = zoneLimits[i];
                Vector2[] newZonePositions = new Vector2[_nbMaxZone];
                for (int j = 0; j < _zonePositions.Length; j++)
                {
                    newZonePositions[j] = _zonePositions[j];
                }
                _zonePositions = newZonePositions;
                _currentScoreIndex++;
            }
        }     
        for(int i =_nbCurrentZone; i<_nbMaxZone; i++)
        {
            if(_zonePositions[i] == Vector2.zero)
                _currentZoneIndex = i;
            CreateZone();
        }
    }



    List<Vector2> GetAvailableIntervals()
    {
        // Si le tableau est vide ou contient des valeurs par défaut, considérer le cercle entier comme disponible
        if (_zonePositions == null || _zonePositions.Length == 0 || (_zonePositions.Length == 1 && _zonePositions[0] == Vector2.zero))
        {
            return new List<Vector2> { new Vector2(0, 360) };
        }

        List<Vector2> availableIntervals = new List<Vector2>();

        // Copier et trier le tableau par angle de départ (x) pour simplifier le traitement
        Vector2[] sortedZones = (Vector2[])_zonePositions.Clone();
        Array.Sort(sortedZones, (a, b) => a.x.CompareTo(b.x));

        float lastEnd = 0;

        // Trouver les intervalles entre les zones
        foreach (var zone in sortedZones)
        {
            // Vérifier si un espace est disponible entre la fin de la dernière zone et le début de la zone actuelle
            if (zone.x - lastEnd > minZoneSize)
            {
                availableIntervals.Add(new Vector2(lastEnd, zone.x));
            }

            // Mettre à jour `lastEnd` pour suivre la zone en cours
            lastEnd = Mathf.Max(lastEnd, zone.y);
        }

        // Vérifier l'espace entre la dernière zone et le début du cercle
        if (360 - lastEnd + sortedZones[0].x > minZoneSize)
        {
            availableIntervals.Add(new Vector2(lastEnd, sortedZones[0].x));
        }

        return availableIntervals;
    }

    private void OnHitPerformedHandler()
    {
        float playerAngle = _player.GetCurrentAngle();
        int closestZoneIndex = -1; // Index de la zone la plus proche
        float minDistance = float.MaxValue;

        // Trouver la zone la plus proche
        for (int i = 0; i < _zonePositions.Length; i++)
        {
            Vector2 pos = _zonePositions[i];
            float zoneSize = (pos.y >= pos.x) ? (pos.y - pos.x) : 360f - pos.x + (pos.y);
            float zoneCenter = (pos.y -  zoneSize / 2) % 360;
            if (zoneCenter < 0) zoneCenter += 360f;
            float distance = AngleDistance(playerAngle, zoneCenter);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestZoneIndex = i;
            }
        }

        if (closestZoneIndex != -1)
        {
            // Envoyer un événement à la zone correspondante
            OnZoneTargeted?.Invoke(closestZoneIndex);
        }
    }

    private float AngleDistance(float angle1, float angle2)
    {
        float diff = Mathf.Abs(angle1 - angle2) % 360;
        return diff > 180 ? 360 - diff : diff;
    }

    public void UpdateZonePosition(int index, Vector2 pos)
    {
        for (int i = 0; i < _zonePositions.Length; i++)
        {
            if (i == index)
            {
                _zonePositions[i] = pos;
                break;
            }
        }
    }

    public Vector2 ResolveOverlap(int index)
    {
        float originalSize = (_zonePositions[index].y >= _zonePositions[index].x)
        ? _zonePositions[index].y - _zonePositions[index].x
        : (360 - _zonePositions[index].x) + _zonePositions[index].y;

        for (int i = 0; i < _zonePositions.Length; i++)
        {
            if (i == index) // On ignore la zone courante
                continue;

            if (_zonePositions[i] == Vector2.zero) // On ignore les zones non valides
                continue;

            // Gestion normale des chevauchements
            if (_zonePositions[i].x < _zonePositions[index].y && _zonePositions[i].x > _zonePositions[index].x)
            {
                return new Vector2(_zonePositions[i].x - originalSize, _zonePositions[i].x);
            }
            if (_zonePositions[i].y > _zonePositions[index].x && _zonePositions[i].y < _zonePositions[index].y)
            {
                return new Vector2(_zonePositions[i].y, _zonePositions[i].y + originalSize);
            }

            // Gestion des chevauchements avec 0° (zones qui traversent 360°)
            bool indexWraps = _zonePositions[index].y > 360 || _zonePositions[index].x < 0;
            bool otherWraps = _zonePositions[i].y > 360 || _zonePositions[i].x < 0;

            if (indexWraps || otherWraps)
            {
                // Normaliser les positions pour faciliter les calculs
                float start1 = _zonePositions[index].x % 360;
                float end1 = _zonePositions[index].y % 360;
                float start2 = _zonePositions[i].x % 360;
                float end2 = _zonePositions[i].y % 360;

                if (start1 > end1) end1 += 360; // Si la zone traverse 0°, ajustez les limites
                if (start2 > end2) end2 += 360;

                // Vérifiez les chevauchements normalisés
                if ((start1 < end2 && start1 > start2) || (end1 > start2 && end1 < end2))
                {
                    return new Vector2(Mathf.Max(start1, start2), Mathf.Min(end1, end2));
                }
            }
        }
        return Vector2.zero; // Aucun chevauchement trouvé
    }

    private Vector2 NormalizeZone(Vector2 zone)
{
    float start = (zone.x + 360) % 360;
    float end = (zone.y + 360) % 360;
    return new Vector2(start, end);
}
}
