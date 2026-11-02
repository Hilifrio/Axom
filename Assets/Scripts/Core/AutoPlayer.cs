using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

// Joueur automatique pour les tests : ErrorSeconds simule l'imprécision d'un humain.
public sealed class AutoPlayer
{
    readonly Random _rng;
    readonly float _errorSeconds;
    readonly Dictionary<Zone, float> _offsets = new Dictionary<Zone, float>();
    float _releaseOffset;
    bool _released;

    public AutoPlayer(float errorSeconds = 0f, int seed = 1)
    {
        _errorSeconds = errorSeconds;
        _rng = new Random(seed);
    }

    public bool Step(GameSession session, float dt)
    {
        float reach = session.DegreesPerSecond * dt;
        Zone hold = session.ActiveHold;
        if (hold != null)
        {
            float remaining = hold.Half * 2f - hold.HoldTravelled;
            if (remaining <= _releaseOffset + reach * 0.5f)
            {
                session.Release();
                _released = true;
            }
            return false;
        }

        foreach (Zone zone in session.Zones)
        {
            if (!zone.IsTarget || zone.Done) continue;

            float aim = zone.Type == ZoneType.Hold ? zone.Center - session.Direction * zone.Half : zone.Center;
            float ahead = Mathf.DeltaAngle(session.OrbAngle, aim) * session.Direction;
            if (ahead > 80f || ahead < -zone.Half) continue;

            float offset;
            if (!_offsets.TryGetValue(zone, out offset))
            {
                offset = Gaussian() * _errorSeconds * session.DegreesPerSecond;
                _offsets[zone] = offset;
            }

            if (ahead <= offset + reach * 0.5f)
            {
                session.Press();
                _releaseOffset = Gaussian() * _errorSeconds * session.DegreesPerSecond;
                return true;
            }
        }
        return false;
    }

    float Gaussian()
    {
        double u = 1.0 - _rng.NextDouble();
        double v = _rng.NextDouble();
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u)) * System.Math.Cos(2.0 * System.Math.PI * v));
    }
}
