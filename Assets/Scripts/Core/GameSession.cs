using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

// Une partie, sans dépendance à l'affichage ni à l'audio : l'orbe, les zones, le score et le directeur de vagues.
// Le cercle représente une mesure : 90° = 1 temps. Les zones sont posées sur la grille rythmique,
// frapper leur centre revient donc à frapper sur le temps.
public sealed class GameSession
{
    public const float DegreesPerBeat = 90f;
    const float PhaseGrid = 45f;
    const float PhaseGain = 0.02f;
    const float PhaseMaxCorrection = 0.1f;

    public event Action<GameEvent> OnEvent;

    public readonly GameConfig Config;
    public readonly List<Zone> Zones = new List<Zone>();

    public float OrbAngle { get; private set; }
    public int Direction { get; private set; }
    public bool Playing { get; private set; }
    public bool IsOver { get; private set; }

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public int Lives { get; private set; }
    public int Level { get; private set; } = 1;
    public int HitCount { get; private set; }
    public int PerfectCount { get; private set; }
    public int PerfectStreak { get; private set; }
    public float Fever { get; private set; }
    public float FeverBeatsLeft { get; private set; }

    public bool IsFever => FeverBeatsLeft > 0f;
    public Zone ActiveHold => _hold;
    public float Bpm => Mathf.Min(Config.maxBpm, Config.startBpm + (Level - 1) * Config.bpmPerLevel);
    public float DegreesPerSecond => Bpm / 60f * DegreesPerBeat;
    public float PerfectDegrees => Config.perfectWindow * DegreesPerSecond;

    public int Multiplier
    {
        get
        {
            int multiplier = 1;
            foreach (int step in Config.multiplierSteps)
                if (Combo >= step) multiplier++;
            return multiplier;
        }
    }

    public string Rank
    {
        get
        {
            string[] ranks = { "D", "C", "B", "A", "S" };
            int rank = 0;
            foreach (int threshold in Config.rankThresholds)
                if (Score >= threshold) rank++;
            return ranks[Mathf.Min(rank, ranks.Length - 1)];
        }
    }

    readonly Random _rng;
    readonly List<float> _slots = new List<float>();
    Zone _hold;
    float _graceLeft;
    int _hitsThisLevel;
    long _lastEighth = -1;
    ZoneType? _forced;
    bool _forcedDrift;
    bool _forcedBurst;

    public GameSession(GameConfig config, int seed, float orbAngle = 90f, int direction = -1)
    {
        Config = config;
        _rng = new Random(seed);
        OrbAngle = orbAngle;
        Direction = direction;
        Lives = config.startLives;
    }

    public void Begin()
    {
        Playing = true;
    }

    public void Tick(float dt, double beat)
    {
        MoveOrb(dt, beat);
        if (!Playing || IsOver) return;

        _graceLeft -= dt;
        TickFever(dt);
        TickZones(dt);
        TickHold(dt);
        Zones.RemoveAll(z => z.Done);
        if (!IsOver) Direct(beat);
    }

    // L'orbe reste calé sur la musique : après un Flip ou un changement de tempo,
    // sa vitesse est légèrement corrigée jusqu'à retomber sur la grille.
    void MoveOrb(float dt, double beat)
    {
        float expected = Mathf.Repeat((float)(beat % 8.0) * DegreesPerBeat, PhaseGrid);
        float current = Mathf.Repeat(OrbAngle * Direction, PhaseGrid);
        float error = Mathf.Repeat(expected - current + PhaseGrid / 2f, PhaseGrid) - PhaseGrid / 2f;
        float correction = Mathf.Clamp(error * PhaseGain, -PhaseMaxCorrection, PhaseMaxCorrection);
        OrbAngle = Mathf.Repeat(OrbAngle + Direction * DegreesPerSecond * (1f + correction) * dt, 360f);
    }

    void TickFever(float dt)
    {
        if (!IsFever) return;
        FeverBeatsLeft -= dt * Bpm / 60f;
        if (FeverBeatsLeft > 0f)
        {
            Fever = FeverBeatsLeft / Config.feverBeats;
            return;
        }
        FeverBeatsLeft = 0f;
        Fever = 0f;
        Emit(new GameEvent { Type = GameEventType.FeverEnded, Angle = OrbAngle });
    }

    void TickZones(float dt)
    {
        foreach (Zone zone in Zones)
        {
            if (zone.Done) continue;
            zone.Age += dt;
            if (zone.Drift != 0f) MoveZone(zone, dt);

            if (zone.MaxLife > 0f)
            {
                zone.Life -= dt;
                zone.Half = zone.BaseHalf * Mathf.Lerp(0.45f, 1f, zone.LifeRatio);
                if (zone.Life <= 0f)
                {
                    zone.Done = true;
                    continue;
                }
            }

            if (zone == _hold) continue;

            bool inside = zone.Contains(OrbAngle, EdgeGraceDegrees);
            if (zone.OrbInside && !inside && zone.IsTarget)
            {
                zone.Done = true;
                if (zone.IsMandatory) Punish(GameEventType.ZonePassed, zone.Type, true, zone.Center);
            }
            zone.OrbInside = inside;
            if (IsOver) return;
        }
    }

    void MoveZone(Zone zone, float dt)
    {
        float next = Mathf.Repeat(zone.Center + zone.Drift * dt, 360f);
        foreach (Zone other in Zones)
        {
            if (other == zone || other.Done) continue;
            if (other.DistanceTo(next) < other.Half + zone.Half + Config.zoneGapDegrees / 2f)
            {
                zone.Drift = -zone.Drift;
                return;
            }
        }
        zone.Center = next;
    }

    void TickHold(float dt)
    {
        if (_hold == null) return;

        float span = _hold.Half * 2f;
        _hold.HoldTravelled += DegreesPerSecond * dt;

        while (_hold.HoldTravelled >= _hold.HoldNextTick && _hold.HoldNextTick < span)
        {
            _hold.HoldNextTick += PhaseGrid / 2f;
            Score += Config.holdTickPoints * Multiplier;
            Emit(new GameEvent { Type = GameEventType.HoldTick, Zone = ZoneType.Hold, Angle = OrbAngle });
        }

        if (_hold.HoldTravelled >= span + EdgeGraceDegrees + Config.greatWindow * DegreesPerSecond)
            FinishHold(HitTier.Good, 0f);
    }

    public void Press()
    {
        if (!Playing || IsOver || _hold != null) return;

        Zone target = null;
        float best = float.MaxValue;
        foreach (Zone zone in Zones)
        {
            if (zone.Done || !zone.IsTarget || !zone.Contains(OrbAngle, EdgeGraceDegrees)) continue;
            float distance = AimDistance(zone);
            if (distance < best)
            {
                best = distance;
                target = zone;
            }
        }

        if (target != null)
        {
            HitZone(target, best);
            return;
        }

        foreach (Zone zone in Zones)
        {
            if (zone.Done || zone.Type != ZoneType.Mine || !zone.Contains(OrbAngle, 0f)) continue;
            zone.Done = true;
            Punish(GameEventType.MineHit, ZoneType.Mine, true);
            return;
        }

        Punish(GameEventType.VoidTap, ZoneType.Tap, false);
    }

    public void Release()
    {
        if (_hold == null || IsOver) return;

        float remaining = _hold.Half * 2f - _hold.HoldTravelled;
        if (remaining > Config.greatWindow * DegreesPerSecond + EdgeGraceDegrees)
        {
            _hold.Done = true;
            _hold = null;
            Punish(GameEventType.HoldBroken, ZoneType.Hold, false);
            return;
        }
        float distance = Mathf.Abs(remaining);
        FinishHold(TierFor(distance / DegreesPerSecond), distance);
    }

    float EdgeGraceDegrees => Config.edgeGrace * DegreesPerSecond;

    // Le point à viser : le centre, ou le bord le plus proche pour une zone Hold.
    float AimDistance(Zone zone)
    {
        float distance = zone.DistanceTo(OrbAngle);
        return zone.Type == ZoneType.Hold ? Mathf.Abs(zone.Half - distance) : distance;
    }

    HitTier TierFor(float seconds)
    {
        if (seconds <= Config.perfectWindow) return HitTier.Perfect;
        return seconds <= Config.greatWindow ? HitTier.Great : HitTier.Good;
    }

    void HitZone(Zone zone, float distance)
    {
        HitTier tier = TierFor(distance / DegreesPerSecond);

        if (zone.Type == ZoneType.Hold)
        {
            zone.Holding = true;
            zone.HoldTravelled = 0f;
            zone.HoldNextTick = PhaseGrid / 2f;
            _hold = zone;
            ScoreHit(zone, tier, distance, GameEventType.HoldStarted);
            return;
        }

        zone.Done = true;
        if (zone.Type == ZoneType.Flip) Direction = -Direction;
        if (zone.Type == ZoneType.Gold)
        {
            Lives = Mathf.Min(Lives + 1, Config.maxLives);
            ChargeFever(Config.feverPerGold);
        }
        ScoreHit(zone, tier, distance, GameEventType.Hit);
    }

    void FinishHold(HitTier tier, float distance)
    {
        Zone zone = _hold;
        _hold = null;
        zone.Done = true;
        ScoreHit(zone, tier, distance, GameEventType.Hit);
    }

    void ScoreHit(Zone zone, HitTier tier, float distance, GameEventType type)
    {
        int points = BasePoints(tier, distance, zone);
        PerfectStreak = tier == HitTier.Perfect ? PerfectStreak + 1 : 0;
        if (tier == HitTier.Perfect)
        {
            PerfectCount++;
            points += Mathf.Min((PerfectStreak - 1) * Config.perfectStreakBonus, Config.perfectStreakBonusMax);
            ChargeFever(Config.feverPerPerfect);
        }
        else if (tier == HitTier.Great)
        {
            ChargeFever(Config.feverPerGreat);
        }

        Combo++;
        MaxCombo = Mathf.Max(MaxCombo, Combo);
        HitCount++;

        points *= Multiplier;
        if (IsFever) points *= Config.feverScoreFactor;
        if (zone.Type == ZoneType.Gold) points *= Config.goldFactor;
        Score += points;

        Emit(new GameEvent { Type = type, Zone = zone.Type, Tier = tier, Points = points, Angle = OrbAngle });

        _hitsThisLevel++;
        if (_hitsThisLevel >= Config.hitsPerLevel) LevelUp();
    }

    // Plus l'appui est proche du centre, plus il rapporte, même sous le seuil Perfect.
    int BasePoints(HitTier tier, float distance, Zone zone)
    {
        if (tier == HitTier.Perfect) return Config.perfectPoints;

        float reference = zone.Type == ZoneType.Hold ? Config.greatWindow * 2f * DegreesPerSecond : zone.Half;
        float accuracy = 1f - Mathf.Clamp01(distance / reference);
        float points = tier == HitTier.Great ? Mathf.Lerp(50f, 90f, accuracy) : Mathf.Lerp(10f, 40f, accuracy);
        return Mathf.RoundToInt(points / 5f) * 5;
    }

    void ChargeFever(float amount)
    {
        if (IsFever) return;
        Fever = Mathf.Min(1f, Fever + amount);
        if (Fever < 1f) return;
        FeverBeatsLeft = Config.feverBeats;
        Emit(new GameEvent { Type = GameEventType.FeverStarted, Angle = OrbAngle });
    }

    void Punish(GameEventType type, ZoneType zoneType, bool costsLife, float? angle = null)
    {
        bool shielded = IsFever;
        bool lifeLost = costsLife && !shielded && (type == GameEventType.MineHit || _graceLeft <= 0f);
        if (!shielded)
        {
            Combo = 0;
            PerfectStreak = 0;
        }
        if (lifeLost)
        {
            Lives--;
            _graceLeft = Config.lifeLossGrace;
        }

        Emit(new GameEvent { Type = type, Zone = zoneType, Angle = angle ?? OrbAngle, LifeLost = lifeLost });

        if (Lives > 0) return;
        IsOver = true;
        _hold = null;
        Emit(new GameEvent { Type = GameEventType.GameOver, Angle = OrbAngle });
    }

    void LevelUp()
    {
        Level++;
        _hitsThisLevel = 0;
        if (Level == Config.driftLevel) _forcedDrift = true;
        if (Level == Config.flipLevel) _forced = ZoneType.Flip;
        if (Level == Config.holdLevel) _forced = ZoneType.Hold;
        if (Level == Config.burstLevel) _forcedBurst = true;
        Emit(new GameEvent { Type = GameEventType.LevelUp, Angle = OrbAngle });
    }

    // Directeur de vagues : à chaque croche, complète le cercle pour garder le joueur occupé.
    void Direct(double beat)
    {
        int targets = 0;
        int mines = 0;
        bool hasGold = false;
        foreach (Zone zone in Zones)
        {
            if (zone.IsMandatory) targets++;
            if (zone.Type == ZoneType.Mine) mines++;
            if (zone.Type == ZoneType.Gold) hasGold = true;
        }

        long eighth = (long)Math.Floor(beat * 2.0);
        if (eighth == _lastEighth && targets > 0) return;
        _lastEighth = eighth;

        if (targets < DesiredTargets) SpawnNext();

        if (Level >= Config.goldLevel && !hasGold && _rng.NextDouble() < Config.goldChance / 4f)
            SpawnGold();

        if (Level >= Config.mineLevel && mines < Mathf.Min(3, 1 + (Level - Config.mineLevel) / 3) && _rng.NextDouble() < 0.2)
            SpawnMine();
    }

    int DesiredTargets
    {
        get
        {
            int desired = Mathf.Min(5, 2 + (Level - 1) / 2);
            return IsFever ? Mathf.Min(6, desired + 2) : desired;
        }
    }

    float HalfSize
    {
        get
        {
            float t = Mathf.Clamp01((Level - 1f) / Config.levelsToMinSize);
            return Mathf.Lerp(Config.startHalfSeconds, Config.minHalfSeconds, t) * DegreesPerSecond;
        }
    }

    float GridStep => Level >= Config.eighthGridLevel ? PhaseGrid : PhaseGrid * 2f;

    void SpawnNext()
    {
        if (_forced.HasValue)
        {
            if (Spawn(_forced.Value) != null) _forced = null;
            return;
        }
        if (_forcedBurst)
        {
            if (SpawnBurst()) _forcedBurst = false;
            return;
        }

        double roll = _rng.NextDouble();
        if (Level >= Config.holdLevel && roll < 0.14 && Spawn(ZoneType.Hold) != null) return;
        if (Level >= Config.flipLevel && roll >= 0.14 && roll < 0.3 && Spawn(ZoneType.Flip) != null) return;
        if (Level >= Config.burstLevel && roll >= 0.3 && roll < 0.42 && SpawnBurst()) return;

        Zone zone = Spawn(ZoneType.Tap);
        if (zone == null) return;
        if (_forcedDrift || (Level >= Config.driftLevel && _rng.NextDouble() < 0.3))
        {
            float speed = Mathf.Lerp(Config.driftMin, Config.driftMax, (float)_rng.NextDouble()) * DegreesPerSecond;
            zone.Drift = _rng.Next(2) == 0 ? speed : -speed;
            _forcedDrift = false;
        }
    }

    Zone Spawn(ZoneType type)
    {
        float half = type == ZoneType.Hold ? (_rng.Next(2) == 0 ? 45f : 90f) : HalfSize;
        float grid = type == ZoneType.Hold ? PhaseGrid * 2f : GridStep;
        float center;
        if (!FindSlot(half, grid, out center)) return null;
        return AddZone(type, center, half);
    }

    void SpawnGold()
    {
        float half = HalfSize * 0.7f;
        float center;
        if (!FindSlot(half, PhaseGrid, out center)) return;
        Zone zone = AddZone(ZoneType.Gold, center, half);
        zone.MaxLife = zone.Life = 8f * 60f / Bpm;
    }

    // Une rafale : trois petites zones sur des croches consécutives.
    bool SpawnBurst()
    {
        float half = Mathf.Min(HalfSize, PhaseGrid / 2f - 4f);
        float center;
        if (!FindSlot(half, PhaseGrid, out center)) return false;

        float second = Mathf.Repeat(center + Direction * PhaseGrid, 360f);
        float third = Mathf.Repeat(center + Direction * PhaseGrid * 2f, 360f);
        if (!IsFree(second, half, 2f) || !IsFree(third, half, 2f)) return false;

        AddZone(ZoneType.Tap, center, half);
        AddZone(ZoneType.Tap, second, half);
        AddZone(ZoneType.Tap, third, half);
        return true;
    }

    // Les mines se placent juste après une cible : c'est l'appui tardif qui est puni.
    void SpawnMine()
    {
        Zone next = null;
        float nearest = float.MaxValue;
        foreach (Zone zone in Zones)
        {
            if (!zone.IsMandatory || zone.Type == ZoneType.Hold || zone.Drift != 0f) continue;
            float ahead = Ahead(zone.Center);
            if (ahead < nearest)
            {
                nearest = ahead;
                next = zone;
            }
        }
        if (next == null) return;

        float half = Mathf.Lerp(10f, 16f, (float)_rng.NextDouble());
        float center = Mathf.Repeat(next.Center + Direction * (next.Half + half + 3f), 360f);
        if (!IsFree(center, half, 2f) || Ahead(center) < half + 30f) return;

        Zone mine = AddZone(ZoneType.Mine, center, half);
        mine.MaxLife = mine.Life = 8f * 60f / Bpm;
    }

    Zone AddZone(ZoneType type, float center, float half)
    {
        Zone zone = new Zone { Type = type, Center = center, Half = half, BaseHalf = half };
        Zones.Add(zone);
        Emit(new GameEvent { Type = GameEventType.ZoneSpawned, Zone = type, Angle = center });
        return zone;
    }

    // Distance à parcourir par l'orbe pour atteindre un angle.
    float Ahead(float angle) => Mathf.Repeat((angle - OrbAngle) * Direction, 360f);

    bool FindSlot(float half, float grid, out float center)
    {
        _slots.Clear();
        float lead = Config.leadSeconds * DegreesPerSecond + half;
        float travel = Mathf.Repeat(OrbAngle * Direction, 360f);
        float first = Mathf.Ceil(travel / grid) * grid;
        for (float step = first; step - travel <= 360f - lead; step += grid)
        {
            if (step - travel < lead) continue;
            float angle = Mathf.Repeat(step * Direction, 360f);
            if (IsFree(angle, half, Config.zoneGapDegrees)) _slots.Add(angle);
        }

        center = 0f;
        if (_slots.Count == 0) return false;
        center = _slots[_rng.Next(Mathf.Min(_slots.Count, 3))];
        return true;
    }

    bool IsFree(float angle, float half, float gap)
    {
        foreach (Zone zone in Zones)
            if (!zone.Done && zone.DistanceTo(angle) < zone.Half + half + gap)
                return false;
        return true;
    }

    void Emit(GameEvent gameEvent)
    {
        OnEvent?.Invoke(gameEvent);
    }
}
