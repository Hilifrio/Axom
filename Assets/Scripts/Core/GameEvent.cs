public enum HitTier { Miss, Good, Great, Perfect }

public enum GameEventType
{
    ZoneSpawned,
    Hit,
    HoldStarted,
    HoldTick,
    HoldBroken,
    VoidTap,
    MineHit,
    ZonePassed,
    FeverStarted,
    FeverEnded,
    LevelUp,
    GameOver
}

public struct GameEvent
{
    public GameEventType Type;
    public ZoneType Zone;
    public HitTier Tier;
    public int Points;
    public float Angle;
    public bool LifeLost;
}
