using UnityEngine;
using Utils;

// Point d'entrée : relie les entrées, la partie en cours, l'horloge musicale et l'affichage.
public class GameManager : MonoSingleton<GameManager>
{
    public enum Screen { Title, Playing, Over }

    public const float RetryDelay = 0.45f;
    const string BestScoreKey = "axom.best";
    const float ClockCorrection = 0.08f;

    [SerializeField] GameConfig _config = new GameConfig();

    public GameSession Session { get; private set; }
    public Screen CurrentScreen { get; private set; } = Screen.Title;
    public double Beat => _beat;
    public SynthEngine Audio => _synth.Engine;
    public bool AudioRunning => _synth.IsRunning;

    // Désactivé par les tests qui font tourner le jeu plus vite que le temps réel.
    public bool UseAudioClock { get; set; } = true;

    GameView _view;
    Synth _synth;
    double _beat;
    double _audioLatencyBeats;
    float _overTime;
    int _bestScore;
    bool _newBest;

    protected override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = Mathf.Max(60, Mathf.RoundToInt((float)UnityEngine.Screen.currentResolution.refreshRateRatio.value));

        Camera mainCamera = Camera.main;
        _synth = mainCamera.gameObject.AddComponent<Synth>();
        NeonRenderer neon = new GameObject("Neon").AddComponent<NeonRenderer>();
        _view = new GameView(neon, mainCamera);

        _bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        Session = new GameSession(_config, System.Environment.TickCount);
    }

    void OnEnable()
    {
        Player.OnHitPressed += Press;
        Player.OnHitReleased += Release;
    }

    void OnDisable()
    {
        Player.OnHitPressed -= Press;
        Player.OnHitReleased -= Release;
    }

    public void Press()
    {
        switch (CurrentScreen)
        {
            case Screen.Playing:
                Session.Press();
                break;
            case Screen.Over:
                if (_overTime >= RetryDelay) StartRun();
                break;
            default:
                StartRun();
                break;
        }
    }

    public void Release()
    {
        if (CurrentScreen == Screen.Playing) Session.Release();
    }

    void StartRun()
    {
        Session.OnEvent -= OnSessionEvent;
        Session = new GameSession(_config, System.Environment.TickCount, Session.OrbAngle, Session.Direction);
        Session.OnEvent += OnSessionEvent;
        Session.Begin();
        CurrentScreen = Screen.Playing;
        _newBest = false;
        _view.OnRunStarted();
    }

    void OnSessionEvent(GameEvent gameEvent)
    {
        if (gameEvent.Type == GameEventType.GameOver) OnGameOver();
        _view.Handle(gameEvent, Session);
        _synth.Handle(gameEvent, Session);
    }

    void OnGameOver()
    {
        CurrentScreen = Screen.Over;
        _overTime = 0f;
        _newBest = Session.Score > _bestScore;
        if (!_newBest) return;
        _bestScore = Session.Score;
        PlayerPrefs.SetInt(BestScoreKey, _bestScore);
        PlayerPrefs.Save();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _overTime += dt;

        UpdateClock(dt);
        Session.Tick(dt * _view.TimeScale, _beat);
        UpdateMusic();
    }

    // L'horloge suit la musique en douceur : la position audio avance par blocs, l'orbe doit rester fluide.
    void UpdateClock(float dt)
    {
        _beat += dt * Session.Bpm / 60f;
        if (!UseAudioClock || !_synth.IsRunning) return;

        double target = _synth.Engine.Beat - AudioLatencyBeats;
        double error = target - _beat;
        _beat = System.Math.Abs(error) > 1.0 ? target : _beat + error * ClockCorrection;
    }

    double AudioLatencyBeats
    {
        get
        {
            int length, count;
            AudioSettings.GetDSPBufferSize(out length, out count);
            return (double)length * count / AudioSettings.outputSampleRate * Session.Bpm / 60.0;
        }
    }

    void UpdateMusic()
    {
        SynthEngine engine = _synth.Engine;
        engine.TargetBpm = Session.Bpm;
        engine.Intensity = CurrentScreen == Screen.Title ? 0 : Intensity(Session.Level);
        engine.Fever = Session.IsFever;
        engine.Ducked = CurrentScreen == Screen.Over;
    }

    static int Intensity(int level)
    {
        if (level >= 7) return 4;
        if (level >= 5) return 3;
        if (level >= 3) return 2;
        return level >= 2 ? 1 : 0;
    }

    void LateUpdate()
    {
        _view.Draw(Session, CurrentScreen, Time.deltaTime, _beat, _bestScore, _newBest);
    }
}
