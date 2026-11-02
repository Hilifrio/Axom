using UnityEngine;

// Branche le SynthEngine sur l'AudioListener et traduit les événements de jeu en sons.
[RequireComponent(typeof(AudioListener))]
public class Synth : MonoBehaviour
{
    SynthEngine _engine;
    int _lastRenderCount;
    float _lastRenderTime = float.NegativeInfinity;

    public SynthEngine Engine => _engine;

    // Faux quand la sortie audio est absente : le jeu retombe alors sur son horloge interne.
    public bool IsRunning => Time.unscaledTime - _lastRenderTime < 0.25f;

    void Awake()
    {
        _engine = new SynthEngine(AudioSettings.outputSampleRate);
    }

    void Update()
    {
        int renderCount = _engine.RenderCount;
        if (renderCount == _lastRenderCount) return;
        _lastRenderCount = renderCount;
        _lastRenderTime = Time.unscaledTime;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        _engine?.Render(data, channels);
    }

    public void Handle(GameEvent gameEvent, GameSession session)
    {
        switch (gameEvent.Type)
        {
            case GameEventType.Hit:
            case GameEventType.HoldStarted:
                PlayHit(gameEvent, session);
                break;
            case GameEventType.HoldTick:
                _engine.Play(_engine.ChordFrequency(session.Combo % 12, 1), 0.08f, 0.08f, SynthEngine.Wave.Square);
                break;
            case GameEventType.VoidTap:
                _engine.Play(160f, 0.3f, 0.12f, SynthEngine.Wave.Square, -4f);
                break;
            case GameEventType.HoldBroken:
                _engine.Play(320f, 0.3f, 0.25f, SynthEngine.Wave.Square, -5f);
                break;
            case GameEventType.ZonePassed:
                PlayDamage(gameEvent.LifeLost);
                break;
            case GameEventType.MineHit:
                _engine.Play(0f, 0.6f, 0.4f, SynthEngine.Wave.Noise);
                PlayDamage(gameEvent.LifeLost);
                break;
            case GameEventType.LevelUp:
                for (int i = 0; i < 4; i++)
                    _engine.Play(_engine.ChordFrequency(4 + i * 2), 0.14f, 0.3f + i * 0.08f, SynthEngine.Wave.Square);
                break;
            case GameEventType.FeverStarted:
                _engine.Play(220f, 0.35f, 0.7f, SynthEngine.Wave.Square, 3f);
                _engine.Play(_engine.ChordFrequency(8), 0.3f, 0.8f, SynthEngine.Wave.Sine);
                break;
            case GameEventType.GameOver:
                _engine.Play(440f, 0.45f, 1.2f, SynthEngine.Wave.Square, -3f);
                _engine.Play(0f, 0.5f, 0.7f, SynthEngine.Wave.Noise);
                break;
        }
    }

    // Chaque note du combo monte dans l'accord : une bonne série devient une mélodie ascendante.
    void PlayHit(GameEvent gameEvent, GameSession session)
    {
        float frequency = _engine.ChordFrequency(session.Combo % 12, 1);
        switch (gameEvent.Tier)
        {
            case HitTier.Perfect:
                _engine.Play(frequency, 0.32f, 0.3f, SynthEngine.Wave.Square);
                _engine.Play(frequency * 2f, 0.22f, 0.4f, SynthEngine.Wave.Sine);
                _engine.Play(0f, 0.12f, 0.04f, SynthEngine.Wave.Noise);
                break;
            case HitTier.Great:
                _engine.Play(frequency, 0.26f, 0.2f, SynthEngine.Wave.Square);
                break;
            default:
                _engine.Play(frequency, 0.2f, 0.12f, SynthEngine.Wave.Sine);
                break;
        }
        if (gameEvent.Zone == ZoneType.Gold) _engine.Play(frequency * 3f, 0.2f, 0.6f, SynthEngine.Wave.Sine);
        if (gameEvent.Zone == ZoneType.Flip) _engine.Play(300f, 0.2f, 0.18f, SynthEngine.Wave.Sine, 6f);
    }

    void PlayDamage(bool lifeLost)
    {
        if (lifeLost) _engine.Play(110f, 0.6f, 0.4f, SynthEngine.Wave.Square, -3f);
        else _engine.Play(160f, 0.25f, 0.12f, SynthEngine.Wave.Square, -4f);
    }
}
