using UnityEngine;

public enum ZoneType
{
    Tap,    // à frapper, éventuellement mobile
    Flip,   // à frapper, inverse le sens de l'orbe
    Hold,   // appuyer sur un bord, relâcher sur l'autre
    Gold,   // bonus éphémère : 1 vie et de la Fever, sans pénalité si raté
    Mine    // ne jamais appuyer dedans
}

public class Zone
{
    public ZoneType Type;
    public float Center;
    public float Half;
    public float BaseHalf;
    public float Drift;         // degrés par seconde
    public float Age;
    public float Life = -1f;    // < 0 : pas de limite de temps
    public float MaxLife = -1f;

    public bool OrbInside;
    public bool Done;

    public bool Holding;
    public float HoldTravelled;
    public float HoldNextTick;

    public bool IsTarget => Type != ZoneType.Mine;
    public bool IsMandatory => Type == ZoneType.Tap || Type == ZoneType.Flip || Type == ZoneType.Hold;
    public float LifeRatio => MaxLife > 0f ? Mathf.Clamp01(Life / MaxLife) : 1f;

    public float DistanceTo(float angle) => Mathf.Abs(Mathf.DeltaAngle(angle, Center));

    public bool Contains(float angle, float margin) => DistanceTo(angle) <= Half + margin;
}
