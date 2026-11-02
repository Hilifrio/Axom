using UnityEngine;

// Tout ce que le joueur voit : le cercle, les zones, l'interface au centre, et le "juice"
// (particules, secousses, flashs, ralenti). Ne modifie jamais l'état du jeu.
public sealed class GameView
{
    public const float RingRadius = 3.6f;
    const float ViewSize = 4.7f;
    const float BandThickness = 0.26f;
    const float TrailSeconds = 0.2f;
    const int MaxParticles = 512;
    const int MaxPopups = 16;
    const int MaxWaves = 12;
    const int TrailLength = 128;

    static readonly Color TapColor = new Color(0.2f, 0.9f, 1f);
    static readonly Color FlipColor = new Color(0.78f, 0.38f, 1f);
    static readonly Color HoldColor = new Color(0.3f, 1f, 0.55f);
    static readonly Color GoldColor = new Color(1f, 0.8f, 0.2f);
    static readonly Color MineColor = new Color(1f, 0.16f, 0.22f);
    static readonly Color PerfectColor = new Color(1f, 0.95f, 0.6f);
    static readonly Color GreatColor = new Color(0.45f, 1f, 0.7f);
    static readonly Color GoodColor = new Color(0.6f, 0.75f, 1f);
    static readonly float[] LevelHues = { 0.62f, 0.75f, 0.85f, 0.52f, 0.92f, 0.57f, 0.8f };

    struct Particle
    {
        public Vector2 Position, Velocity;
        public float Life, MaxLife, Size;
        public Color Color;
    }

    struct Popup
    {
        public string Text;
        public Vector2 Position, Velocity;
        public float Life, MaxLife, Height;
        public Color Color;
    }

    struct Wave
    {
        public Vector2 Center;
        public float Radius, Speed, Life, MaxLife, Thickness;
        public Color Color;
    }

    readonly NeonRenderer _neon;
    readonly Camera _camera;
    readonly Particle[] _particles = new Particle[MaxParticles];
    readonly Popup[] _popups = new Popup[MaxPopups];
    readonly Wave[] _waves = new Wave[MaxWaves];
    readonly Vector2[] _trail = new Vector2[TrailLength];
    readonly float[] _trailTime = new float[TrailLength];

    int _nextParticle, _nextPopup, _nextWave, _trailHead;
    float _clock, _trauma, _zoomPunch, _scorePunch, _flash, _ringAlpha = 1f;
    float _shownScore, _overTime, _bannerLife, _lifePunch;
    Color _flashColor = Color.white;
    Vector2 _kick;
    string _banner = "", _bannerHint = "";

    public float TimeScale { get; private set; } = 1f;

    public GameView(NeonRenderer neon, Camera camera)
    {
        _neon = neon;
        _camera = camera;
    }

    public void OnRunStarted()
    {
        TimeScale = 1f;
        _ringAlpha = 1f;
        _shownScore = 0f;
        _bannerLife = 0f;
        _flash = 0.25f;
        _flashColor = TapColor;
        AddWave(Vector2.zero, 0.5f, 14f, 0.5f, 0.08f, TapColor);
        ShowBanner("GO!", "TAP WHEN THE ORB HITS THE WHITE MARK");
    }

    public void Handle(GameEvent gameEvent, GameSession session)
    {
        Vector2 position = Polar(gameEvent.Angle, RingRadius);
        switch (gameEvent.Type)
        {
            case GameEventType.Hit:
            case GameEventType.HoldStarted:
                OnHit(gameEvent, session, position);
                break;
            case GameEventType.HoldTick:
                Burst(position, 3, 2.5f, HoldColor, 0.3f);
                _scorePunch = Mathf.Max(_scorePunch, 0.08f);
                break;
            case GameEventType.HoldBroken:
                AddPopup("BROKEN", gameEvent.Angle, 0.28f, MineColor);
                Burst(position, 14, 4f, HoldColor, 0.4f);
                _trauma = Mathf.Max(_trauma, 0.35f);
                break;
            case GameEventType.VoidTap:
                AddPopup("MISS", gameEvent.Angle, 0.26f, new Color(0.6f, 0.65f, 0.8f));
                Burst(position, 6, 2f, Color.gray, 0.3f);
                _trauma = Mathf.Max(_trauma, 0.25f);
                break;
            case GameEventType.MineHit:
                AddPopup("BOOM", gameEvent.Angle, 0.42f, MineColor);
                Burst(position, 50, 8f, MineColor, 0.7f);
                Hurt(1f, gameEvent.LifeLost);
                break;
            case GameEventType.ZonePassed:
                AddPopup("TOO LATE", gameEvent.Angle, 0.26f, MineColor);
                Burst(Polar(gameEvent.Angle, RingRadius), 20, 3.5f, ZoneColor(gameEvent.Zone), 0.5f);
                Hurt(0.6f, gameEvent.LifeLost);
                break;
            case GameEventType.FeverStarted:
                ShowBanner("FEVER!", "DOUBLE SCORE - NO DAMAGE");
                Flash(Color.white, 0.5f);
                _trauma = Mathf.Max(_trauma, 0.45f);
                for (int i = 0; i < 3; i++) AddWave(Vector2.zero, 1f + i * 0.6f, 9f, 0.7f, 0.1f, Rainbow(i * 0.2f));
                break;
            case GameEventType.LevelUp:
                ShowBanner("LEVEL " + session.Level, Hint(session));
                AddWave(Vector2.zero, RingRadius, 5f, 0.6f, 0.07f, LevelColor(session.Level));
                _zoomPunch = Mathf.Max(_zoomPunch, 1f);
                break;
            case GameEventType.GameOver:
                OnGameOver(session);
                break;
        }
    }

    string Hint(GameSession session)
    {
        GameConfig config = session.Config;
        if (session.Level == config.driftLevel) return "ZONES NOW MOVE";
        if (session.Level == config.mineLevel) return "RED MINES - DO NOT TAP";
        if (session.Level == config.flipLevel) return "VIOLET FLIPS YOUR ORBIT";
        if (session.Level == config.holdLevel) return "GREEN - HOLD THEN RELEASE";
        if (session.Level == config.burstLevel) return "TRIPLE BURSTS";
        return "SPEED UP";
    }

    void OnHit(GameEvent gameEvent, GameSession session, Vector2 position)
    {
        Color color = TierColor(gameEvent.Tier);
        string label = gameEvent.Tier == HitTier.Perfect ? "PERFECT" : gameEvent.Tier == HitTier.Great ? "GREAT" : "GOOD";
        AddPopup(label + " +" + gameEvent.Points, gameEvent.Angle, 0.2f + (int)gameEvent.Tier * 0.03f, color);

        int count = 10 + (int)gameEvent.Tier * 10 + Mathf.Min(session.Multiplier * 3, 20);
        Burst(position, count, 3f + (int)gameEvent.Tier * 1.2f, gameEvent.Tier == HitTier.Perfect ? Color.white : ZoneColor(gameEvent.Zone), 0.5f);
        Burst(position, count / 2, 2.5f, ZoneColor(gameEvent.Zone), 0.6f);
        AddWave(position, 0.1f, 5f, 0.3f, 0.05f, color);

        _scorePunch = gameEvent.Tier == HitTier.Perfect ? 0.35f : 0.18f;
        _kick = position.normalized * (gameEvent.Tier == HitTier.Perfect ? 0.1f : 0.04f);
        if (gameEvent.Tier == HitTier.Perfect)
        {
            _zoomPunch = Mathf.Max(_zoomPunch, 0.6f);
            Flash(Color.white, 0.06f);
        }
        if (gameEvent.Zone == ZoneType.Gold)
        {
            ShowBanner("GOLD!", "+1 LIFE");
            Flash(GoldColor, 0.25f);
        }
        if (gameEvent.Zone == ZoneType.Flip) AddWave(position, 0.1f, 8f, 0.4f, 0.08f, FlipColor);
    }

    void Hurt(float strength, bool lifeLost)
    {
        _trauma = Mathf.Max(_trauma, strength);
        if (!lifeLost) return;
        Flash(MineColor, 0.3f);
        _lifePunch = 1f;
    }

    void OnGameOver(GameSession session)
    {
        _trauma = 1f;
        TimeScale = 0.2f;
        _overTime = 0f;
        _shownScore = 0f;
        Flash(Color.white, 0.5f);
        for (int i = 0; i < 180; i++)
        {
            Vector2 position = Polar(i * 2f, RingRadius);
            Spawn(position, position.normalized * Random.Range(1f, 7f) + Random.insideUnitCircle * 1.5f,
                Random.Range(0.5f, 1.3f), Random.Range(0.03f, 0.07f), LevelColor(session.Level));
        }
        AddWave(Vector2.zero, RingRadius, 10f, 0.6f, 0.12f, Color.white);
    }

    public void Draw(GameSession session, GameManager.Screen screen, float dt, double beat, int best, bool newBest)
    {
        _clock += dt;
        float scaled = dt * TimeScale;
        float beatPulse = Mathf.Exp(-(float)(beat - System.Math.Floor(beat)) * 5f);

        UpdateTimers(dt, screen);
        UpdateCamera(session, screen, dt, beatPulse);

        _neon.Clear();
        DrawBackground(session, beat, beatPulse);
        DrawRing(session, screen, beatPulse);

        if (screen != GameManager.Screen.Title)
            foreach (Zone zone in session.Zones)
                DrawZone(session, zone, beatPulse, screen == GameManager.Screen.Over ? 0.3f : 1f);

        DrawOrb(session, screen, dt);
        DrawWaves(scaled);
        DrawParticles(scaled);
        DrawPopups(dt);

        switch (screen)
        {
            case GameManager.Screen.Title: DrawTitle(best); break;
            case GameManager.Screen.Playing: DrawHud(session, dt, beatPulse); break;
            default: DrawGameOver(session, best, newBest, dt); break;
        }

        if (_flash > 0.001f)
        {
            float height = _camera.orthographicSize + 2f;
            float width = height * _camera.aspect + 2f;
            _neon.Fill(new Rect(-width, -height, width * 2f, height * 2f), Dim(_flashColor, _flash));
        }

        _neon.Apply();
    }

    void UpdateTimers(float dt, GameManager.Screen screen)
    {
        _trauma = Mathf.Max(0f, _trauma - dt * 2.2f);
        _zoomPunch = Mathf.Max(0f, _zoomPunch - dt * 4f);
        _scorePunch = Mathf.Max(0f, _scorePunch - dt * 2.5f);
        _lifePunch = Mathf.Max(0f, _lifePunch - dt * 3f);
        _flash *= Mathf.Exp(-dt * 9f);
        _kick *= Mathf.Exp(-dt * 12f);
        _bannerLife -= dt;

        if (screen == GameManager.Screen.Over)
        {
            _overTime += dt;
            TimeScale = Mathf.Min(0.6f, TimeScale + dt * 0.2f);
            _ringAlpha = Mathf.Max(0.18f, _ringAlpha - dt * 2f);
        }
    }

    void UpdateCamera(GameSession session, GameManager.Screen screen, float dt, float beatPulse)
    {
        float baseSize = ViewSize / Mathf.Min(1f, _camera.aspect);
        float zoom = 1f - _zoomPunch * 0.035f - beatPulse * 0.006f;
        _camera.orthographicSize = baseSize * zoom;

        float shake = _trauma * _trauma * 0.4f;
        float x = (Mathf.PerlinNoise(_clock * 28f, 3f) - 0.5f) * 2f * shake;
        float y = (Mathf.PerlinNoise(11f, _clock * 28f) - 0.5f) * 2f * shake;
        _camera.transform.position = new Vector3(x + _kick.x, y + _kick.y, -10f);
        _camera.transform.rotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(_clock * 22f, 7f) - 0.5f) * 8f * _trauma * _trauma);

        float hue = session.IsFever ? Mathf.Lerp(0.55f, 0.95f, Mathf.PingPong(_clock * 0.5f, 1f)) : LevelHue(session.Level);
        Color target = Color.HSVToRGB(hue, 0.8f, session.IsFever ? 0.17f : 0.09f);
        _camera.backgroundColor = Color.Lerp(_camera.backgroundColor, target, 1f - Mathf.Exp(-dt * 5f));
    }

    void DrawBackground(GameSession session, double beat, float beatPulse)
    {
        Color color = session.IsFever ? Rainbow(0.5f) : LevelColor(session.Level);
        float intensity = session.IsFever ? 2f : 1f;

        const int rings = 6;
        for (int i = 0; i < rings; i++)
        {
            float phase = Mathf.Repeat((float)(beat % 64.0) / 4f + (float)i / rings, 1f);
            float radius = Mathf.Lerp(1f, 12f, phase * phase);
            _neon.Arc(Vector2.zero, radius, 0f, 360f, 0.03f, Dim(color, (1f - phase) * 0.16f * intensity));
        }

        const int spokes = 16;
        float rotation = (float)(beat % 256.0) * 2.5f;
        for (int i = 0; i < spokes; i++)
        {
            Vector2 direction = Polar(rotation + i * 360f / spokes, 1f);
            _neon.Line(direction * (RingRadius + 0.5f), direction * 14f, 0.02f, Dim(color, 0.05f + beatPulse * 0.04f));
        }

        _neon.Disc(Vector2.zero, 1.3f + beatPulse * 0.25f, Dim(color, 0.05f + beatPulse * 0.06f), 2.2f);

        if (session.Playing && !session.IsOver && session.Lives == 1)
        {
            float pulse = 0.2f + 0.2f * beatPulse;
            _neon.Arc(Vector2.zero, 8.4f, 0f, 360f, 0.1f, Dim(MineColor, pulse), 80f);
        }
    }

    void DrawRing(GameSession session, GameManager.Screen screen, float beatPulse)
    {
        Color color = session.IsFever ? Rainbow(0f) : Color.Lerp(LevelColor(session.Level), Color.white, 0.35f);
        float alpha = (0.45f + beatPulse * 0.4f) * _ringAlpha;
        _neon.Arc(Vector2.zero, RingRadius, 0f, 360f, 0.045f + beatPulse * 0.03f, Dim(color, alpha));

        for (int i = 0; i < 8; i++)
        {
            Vector2 direction = Polar(i * 45f, 1f);
            float length = i % 2 == 0 ? 0.26f : 0.14f;
            _neon.Line(direction * (RingRadius - 0.26f - length), direction * (RingRadius - 0.26f), 0.03f, Dim(color, (0.3f + beatPulse * 0.3f) * _ringAlpha));
        }
    }

    void DrawZone(GameSession session, Zone zone, float beatPulse, float fade)
    {
        float appear = EaseOutBack(Mathf.Clamp01(zone.Age / 0.16f));
        float half = zone.Half * appear;
        float from = zone.Center - half;
        float to = zone.Center + half;
        float proximity = 1f - Mathf.Clamp01((zone.DistanceTo(session.OrbAngle) - zone.Half) / 70f);
        float alpha = Mathf.Lerp(0.6f, 1f, proximity) * fade;
        Color color = session.IsFever && zone.Type == ZoneType.Tap ? Rainbow(zone.Center / 360f) : ZoneColor(zone.Type);

        switch (zone.Type)
        {
            case ZoneType.Hold:
                DrawHold(session, zone, color, alpha, from, to);
                return;

            case ZoneType.Mine:
                _neon.Arc(Vector2.zero, RingRadius, from, to, BandThickness, Dim(color, alpha * 0.22f), 1.2f);
                DrawZigzag(from, to, Dim(color, alpha * (0.7f + beatPulse * 0.3f)));
                return;
        }

        _neon.Arc(Vector2.zero, RingRadius, from, to, BandThickness, Dim(color, alpha * 0.85f), 1.9f);

        float sweet = Mathf.Min(session.PerfectDegrees, half);
        _neon.Arc(Vector2.zero, RingRadius, zone.Center - sweet, zone.Center + sweet, BandThickness * 1.25f, Dim(Color.white, alpha), 1.5f);

        if (zone.Type == ZoneType.Flip)
        {
            int back = -session.Direction;
            DrawChevron(zone.Center + back * 3f, RingRadius + 0.42f, back, Dim(color, alpha));
            DrawChevron(zone.Center - back * 5f, RingRadius + 0.42f, back, Dim(color, alpha));
        }
        else if (zone.Type == ZoneType.Gold)
        {
            float twinkle = 0.6f + 0.4f * Mathf.Sin(_clock * 18f);
            _neon.Disc(Polar(zone.Center, RingRadius + 0.42f), 0.07f, Dim(color, alpha * twinkle));
        }

        if (zone.Drift != 0f)
            DrawChevron(zone.Center, RingRadius - 0.34f, zone.Drift > 0f ? 1 : -1, Dim(color, alpha));
    }

    void DrawHold(GameSession session, Zone zone, Color color, float alpha, float from, float to)
    {
        if (zone.Holding)
        {
            float remaining = Mathf.Max(0f, zone.Half * 2f - zone.HoldTravelled);
            float end = session.OrbAngle + session.Direction * remaining;
            _neon.Arc(Vector2.zero, RingRadius, session.OrbAngle, end, BandThickness, Dim(color, alpha), 1.9f);
            DrawEdge(end, Color.white);
            return;
        }

        _neon.Arc(Vector2.zero, RingRadius, from, to, BandThickness * 0.7f, Dim(color, alpha * 0.75f), 1.9f);
        DrawEdge(from, Dim(Color.white, alpha));
        DrawEdge(to, Dim(Color.white, alpha));
    }

    void DrawEdge(float angle, Color color)
    {
        Vector2 direction = Polar(angle, 1f);
        _neon.Line(direction * (RingRadius - 0.28f), direction * (RingRadius + 0.28f), 0.07f, color);
    }

    void DrawZigzag(float from, float to, Color color)
    {
        const float step = 3.5f;
        int teeth = Mathf.Max(2, Mathf.RoundToInt((to - from) / step));
        Vector2 previous = Polar(from, RingRadius);
        for (int i = 1; i <= teeth; i++)
        {
            float radius = i == teeth ? RingRadius : RingRadius + (i % 2 == 0 ? -0.2f : 0.2f);
            Vector2 point = Polar(Mathf.Lerp(from, to, (float)i / teeth), radius);
            _neon.Line(previous, point, 0.06f, color);
            previous = point;
        }
    }

    void DrawChevron(float angle, float radius, int direction, Color color)
    {
        Vector2 tip = Polar(angle + direction * 2.5f, radius);
        _neon.Line(Polar(angle - direction * 2.5f, radius - 0.12f), tip, 0.05f, color);
        _neon.Line(Polar(angle - direction * 2.5f, radius + 0.12f), tip, 0.05f, color);
    }

    void DrawOrb(GameSession session, GameManager.Screen screen, float dt)
    {
        Vector2 position = Polar(session.OrbAngle, RingRadius);

        _trailHead = (_trailHead + 1) % TrailLength;
        _trail[_trailHead] = position;
        _trailTime[_trailHead] = _clock;

        Color color = session.IsFever ? Rainbow(0.2f) : Color.Lerp(LevelColor(session.Level), Color.white, 0.5f);
        float trailSeconds = session.IsFever ? TrailSeconds * 2f : TrailSeconds;
        Vector2 previous = position;
        for (int i = 1; i < TrailLength; i++)
        {
            int index = (_trailHead - i + TrailLength) % TrailLength;
            float age = (_clock - _trailTime[index]) / trailSeconds;
            if (age >= 1f || _trailTime[index] <= 0f) break;
            _neon.Line(previous, _trail[index], Mathf.Lerp(0.16f, 0.01f, age), Dim(color, (1f - age) * 0.6f), 2f);
            previous = _trail[index];
        }

        float radius = session.ActiveHold != null ? 0.25f : 0.2f;
        if (screen == GameManager.Screen.Over) radius *= 0.6f;
        _neon.Disc(position, radius, Dim(color, 1f), 3.4f);
        _neon.Disc(position, radius * 0.7f, Color.white, 1.4f);

        if (session.ActiveHold != null && Random.value < dt * 60f)
            Spawn(position, Random.insideUnitCircle * 3f, 0.35f, 0.05f, HoldColor);
    }

    void DrawWaves(float dt)
    {
        for (int i = 0; i < MaxWaves; i++)
        {
            ref Wave wave = ref _waves[i];
            if (wave.Life <= 0f) continue;
            wave.Life -= dt;
            wave.Radius += wave.Speed * dt;
            float t = Mathf.Clamp01(wave.Life / wave.MaxLife);
            _neon.Arc(wave.Center, wave.Radius, 0f, 360f, wave.Thickness * t, Dim(wave.Color, t * 0.8f));
        }
    }

    void DrawParticles(float dt)
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            ref Particle particle = ref _particles[i];
            if (particle.Life <= 0f) continue;
            particle.Life -= dt;
            particle.Position += particle.Velocity * dt;
            particle.Velocity *= Mathf.Exp(-dt * 3f);
            float t = Mathf.Clamp01(particle.Life / particle.MaxLife);
            Vector2 tail = particle.Position - particle.Velocity * 0.03f;
            _neon.Line(tail, particle.Position, particle.Size * t, Dim(particle.Color, t), 2.6f);
        }
    }

    void DrawPopups(float dt)
    {
        for (int i = 0; i < MaxPopups; i++)
        {
            ref Popup popup = ref _popups[i];
            if (popup.Life <= 0f) continue;
            popup.Life -= dt;
            popup.Position += popup.Velocity * dt;
            float t = Mathf.Clamp01(popup.Life / popup.MaxLife);
            float pop = 1f + Mathf.Max(0f, t - 0.75f) * 2f;
            _neon.Text(popup.Text, popup.Position, popup.Height * pop, Dim(popup.Color, Mathf.Clamp01(t * 2.5f)));
        }
    }

    void DrawHud(GameSession session, float dt, float beatPulse)
    {
        _shownScore = Mathf.Lerp(_shownScore, session.Score, 1f - Mathf.Exp(-dt * 16f));
        if (session.Score - _shownScore < 1f) _shownScore = session.Score;

        string score = Mathf.RoundToInt(_shownScore).ToString();
        float height = Mathf.Min(0.95f, 4.3f / (score.Length * 0.9f)) * (1f + _scorePunch);
        Color scoreColor = session.IsFever ? Rainbow(0.1f) : Color.white;
        _neon.Text(score, new Vector2(0f, 0.25f), height, Dim(scoreColor, 0.95f), 0.09f);

        int multiplier = session.Multiplier;
        if (multiplier > 1 || session.IsFever)
        {
            string text = "X" + (multiplier * (session.IsFever ? session.Config.feverScoreFactor : 1));
            Color color = Color.Lerp(TapColor, PerfectColor, multiplier / 6f);
            _neon.Text(text, new Vector2(0f, -0.72f), 0.42f + beatPulse * 0.04f, color, 0.12f);
        }
        if (session.Combo >= 3)
            _neon.Text(session.Combo + " COMBO", new Vector2(0f, -1.25f), 0.18f, Dim(Color.white, 0.75f));

        DrawLives(session);

        float fever = session.Fever;
        float radius = RingRadius * 0.8f;
        _neon.Arc(Vector2.zero, radius, 0f, 360f, 0.02f, Dim(Color.white, 0.12f));
        if (fever > 0f)
        {
            Color color = session.IsFever ? Rainbow(0.3f) : Color.Lerp(GoodColor, GoldColor, fever);
            _neon.Arc(Vector2.zero, radius, 90f, 90f - 360f * fever, 0.07f, Dim(color, fever >= 0.8f ? 0.8f + beatPulse * 0.2f : 0.7f));
        }

        if (_bannerLife > 0f)
        {
            float alpha = Mathf.Clamp01(_bannerLife * 3f);
            _neon.Text(_banner, new Vector2(0f, 1.95f), 0.36f + Mathf.Max(0f, _bannerLife - 1.05f) * 2f, Dim(Color.white, alpha), 0.12f);
            _neon.Text(_bannerHint, new Vector2(0f, 1.5f), 0.16f, Dim(GoldColor, alpha));
        }
        else
        {
            _neon.Text("LEVEL " + session.Level, new Vector2(0f, 1.5f), 0.16f, Dim(Color.white, 0.4f));
        }
    }

    void DrawLives(GameSession session)
    {
        int total = session.Config.maxLives;
        for (int i = 0; i < total; i++)
        {
            Vector2 position = new Vector2((i - (total - 1) * 0.5f) * 0.42f, -1.75f);
            if (i < session.Lives)
            {
                Color color = session.Lives == 1 ? MineColor : TapColor;
                _neon.Disc(position, 0.1f * (1f + _lifePunch * 0.4f), Dim(color, 0.9f));
            }
            else
            {
                _neon.Arc(position, 0.1f, 0f, 360f, 0.02f, Dim(Color.white, 0.25f));
            }
        }
    }

    void DrawTitle(int best)
    {
        float pulse = 1f + Mathf.Sin(_clock * 3f) * 0.015f;
        _neon.Text("AXOM", new Vector2(0f, 0.75f), 1.15f * pulse, TapColor, 0.1f);
        _neon.Text("HIT THE ZONES ON THE BEAT", new Vector2(0f, -0.2f), 0.17f, Dim(Color.white, 0.6f));
        if (Mathf.Repeat(_clock, 1f) < 0.65f)
            _neon.Text("TAP TO START", new Vector2(0f, -0.95f), 0.3f, PerfectColor);
        if (best > 0)
            _neon.Text("BEST " + best, new Vector2(0f, -1.6f), 0.2f, Dim(GoldColor, 0.85f));
    }

    void DrawGameOver(GameSession session, int best, bool newBest, float dt)
    {
        float t = Mathf.Clamp01(_overTime / 0.45f);
        int score = Mathf.RoundToInt(session.Score * (1f - (1f - t) * (1f - t)));

        _neon.Text("GAME OVER", new Vector2(0f, 1.75f), 0.34f, MineColor, 0.12f);

        float height = Mathf.Min(1f, 4.4f / (score.ToString().Length * 0.9f));
        _neon.Text(score.ToString(), new Vector2(0f, 0.75f), height, Color.white, 0.09f);

        if (newBest)
        {
            float pulse = 1f + Mathf.Sin(_clock * 9f) * 0.05f;
            _neon.Text("NEW BEST!", new Vector2(0f, -0.2f), 0.32f * pulse, GoldColor, 0.12f);
        }
        else if (best - session.Score <= best / 4)
        {
            _neon.Text("ONLY " + (best - session.Score) + " TO BEAT BEST", new Vector2(0f, -0.2f), 0.19f, GoldColor);
        }
        else
        {
            _neon.Text("BEST " + best, new Vector2(0f, -0.2f), 0.22f, Dim(GoldColor, 0.85f));
        }

        _neon.Text("RANK " + session.Rank, new Vector2(0f, -0.8f), 0.3f, TapColor, 0.12f);
        _neon.Text("LEVEL " + session.Level + "   COMBO " + session.MaxCombo + "   PERFECT " + session.PerfectCount,
            new Vector2(0f, -1.3f), 0.14f, Dim(Color.white, 0.65f));

        if (_overTime >= GameManager.RetryDelay && Mathf.Repeat(_overTime, 0.6f) < 0.42f)
            _neon.Text("TAP TO RETRY", new Vector2(0f, -1.9f), 0.26f, PerfectColor);
    }

    void ShowBanner(string text, string hint)
    {
        _banner = text;
        _bannerHint = hint;
        _bannerLife = 1.3f;
    }

    void Flash(Color color, float strength)
    {
        _flashColor = color;
        _flash = Mathf.Max(_flash, strength);
    }

    void Burst(Vector2 position, int count, float speed, Color color, float life)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Random.insideUnitCircle.normalized * Random.Range(speed * 0.3f, speed);
            Spawn(position, velocity, Random.Range(life * 0.5f, life), Random.Range(0.03f, 0.08f), color);
        }
    }

    void Spawn(Vector2 position, Vector2 velocity, float life, float size, Color color)
    {
        _particles[_nextParticle] = new Particle
        {
            Position = position,
            Velocity = velocity,
            Life = life,
            MaxLife = life,
            Size = size,
            Color = color
        };
        _nextParticle = (_nextParticle + 1) % MaxParticles;
    }

    void AddPopup(string text, float angle, float height, Color color)
    {
        for (int i = 0; i < MaxPopups; i++) _popups[i].Life = Mathf.Min(_popups[i].Life, 0.1f);

        // Deux emplacements fixes dans le cercle : le texte reste lisible et ne recouvre jamais le score.
        Vector2 position = new Vector2(0f, Mathf.Sin(angle * Mathf.Deg2Rad) >= 0f ? 2.38f : -2.38f);
        _popups[_nextPopup] = new Popup
        {
            Text = text,
            Position = position,
            Velocity = Vector2.up * 0.15f,
            Life = 0.65f,
            MaxLife = 0.65f,
            Height = height,
            Color = color
        };
        _nextPopup = (_nextPopup + 1) % MaxPopups;
    }

    void AddWave(Vector2 center, float radius, float speed, float life, float thickness, Color color)
    {
        _waves[_nextWave] = new Wave
        {
            Center = center,
            Radius = radius,
            Speed = speed,
            Life = life,
            MaxLife = life,
            Thickness = thickness,
            Color = color
        };
        _nextWave = (_nextWave + 1) % MaxWaves;
    }

    static Vector2 Polar(float degrees, float radius)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }

    static Color Dim(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    static float EaseOutBack(float t)
    {
        float u = t - 1f;
        return 1f + 2.7f * u * u * u + 1.7f * u * u;
    }

    Color Rainbow(float offset) => Color.HSVToRGB(Mathf.Repeat(_clock * 0.8f + offset, 1f), 0.75f, 1f);

    static float LevelHue(int level) => LevelHues[(level - 1) % LevelHues.Length];

    static Color LevelColor(int level) => Color.HSVToRGB(LevelHue(level), 0.65f, 1f);

    static Color ZoneColor(ZoneType type)
    {
        switch (type)
        {
            case ZoneType.Flip: return FlipColor;
            case ZoneType.Hold: return HoldColor;
            case ZoneType.Gold: return GoldColor;
            case ZoneType.Mine: return MineColor;
            default: return TapColor;
        }
    }

    static Color TierColor(HitTier tier)
    {
        switch (tier)
        {
            case HitTier.Perfect: return PerfectColor;
            case HitTier.Great: return GreatColor;
            default: return GoodColor;
        }
    }
}
