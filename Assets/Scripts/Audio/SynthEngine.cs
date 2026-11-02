using System.Threading;
using UnityEngine;

// Synthétiseur et séquenceur temps réel : la musique et les bruitages sont générés sans aucun sample.
// La musique ajoute des couches avec Intensity, et son horloge sert de métronome au gameplay.
// Render tourne sur le thread audio : aucune allocation, les bruitages passent par une file sans verrou.
public sealed class SynthEngine
{
    const int VoiceCount = 16;
    const int QueueSize = 64;
    const float TwoPi = Mathf.PI * 2f;
    const double BeatTicks = 1 << 20;

    public enum Wave { Sine, Square, Noise }

    struct Trigger
    {
        public float Frequency, Amplitude, Decay, Slide;
        public Wave Wave;
    }

    struct Voice
    {
        public bool Active;
        public float Phase, Frequency, Amplitude, DecayFactor, SlideFactor;
        public Wave Wave;
    }

    static readonly int[] Roots = { 45, 41, 48, 43 };           // La, Fa, Do, Sol
    static readonly int[][] Chords =
    {
        new[] { 0, 3, 7, 10 },
        new[] { 0, 4, 7, 11 },
        new[] { 0, 4, 7, 11 },
        new[] { 0, 4, 7, 10 }
    };
    static readonly int[] ArpPattern = { 0, 2, 1, 3, 2, 1, 3, 2 };

    public volatile int Intensity;
    public volatile bool Fever;
    public volatile bool Ducked;
    public volatile float TargetBpm = 100f;
    public float Volume = 0.8f;

    readonly Trigger[] _queue = new Trigger[QueueSize];
    readonly Voice[] _voices = new Voice[VoiceCount];
    readonly float[] _delayLeft;
    readonly float[] _delayRight;
    readonly float _sampleRate;
    readonly float _kickDecay, _kickPitchDecay, _snareDecay, _hatDecay, _bassDecay, _arpDecay;

    int _queueHead;
    int _queueTail;
    int _renderCount;
    long _beatTicks;
    volatile int _chord;

    double _beat;
    float _bpm = 100f;
    long _step = -1;
    uint _noise = 22222;
    int _delayIndex;

    float _kickPhase, _kickEnv, _kickPitch;
    float _snareEnv, _hatEnv, _hatAmp, _lastNoise;
    float _bassPhase, _bassFrequency, _bassEnv, _bassLow;
    float _arpPhase, _arpFrequency, _arpEnv, _arpLow;
    float _filterLeft, _filterRight, _cutoff = 1f;

    public SynthEngine(int sampleRate)
    {
        _sampleRate = sampleRate;
        _delayLeft = new float[sampleRate * 2];
        _delayRight = new float[sampleRate * 2];
        _kickDecay = DecayFactor(0.28f);
        _kickPitchDecay = DecayFactor(0.05f);
        _snareDecay = DecayFactor(0.14f);
        _hatDecay = DecayFactor(0.05f);
        _bassDecay = DecayFactor(0.32f);
        _arpDecay = DecayFactor(0.16f);
    }

    public int RenderCount => Volatile.Read(ref _renderCount);

    // Position musicale en temps, lisible depuis le thread principal.
    public double Beat => Interlocked.Read(ref _beatTicks) / BeatTicks;

    public void Play(float frequency, float amplitude, float decay, Wave wave, float slide = 0f)
    {
        int head = Volatile.Read(ref _queueHead);
        int next = (head + 1) % QueueSize;
        if (next == Volatile.Read(ref _queueTail)) return;
        _queue[head] = new Trigger { Frequency = frequency, Amplitude = amplitude, Decay = decay, Slide = slide, Wave = wave };
        Volatile.Write(ref _queueHead, next);
    }

    // Une note de l'accord en cours : les bruitages restent toujours dans la tonalité de la musique.
    public float ChordFrequency(int degree, int octaveOffset = 0)
    {
        int chord = _chord;
        int[] tones = Chords[chord];
        int octave = degree / tones.Length;
        int midi = Roots[chord] + tones[degree % tones.Length] + 12 * (octave + octaveOffset);
        return Midi(midi);
    }

    static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

    public void Render(float[] data, int channels)
    {
        DrainQueue();
        int delaySamples = Mathf.Clamp((int)(_sampleRate * 60f / _bpm * 0.75f), 1, _delayLeft.Length - 1);
        float cutoffTarget = Ducked ? 0.035f : 1f;

        for (int i = 0; i < data.Length; i += channels)
        {
            _beat += _bpm / 60f / _sampleRate;
            long step = (long)(_beat * 4.0);
            if (step != _step)
            {
                _step = step;
                OnStep(step);
            }

            float send;
            float dry = Sample(out send);

            int readIndex = _delayIndex - delaySamples;
            if (readIndex < 0) readIndex += _delayLeft.Length;
            float echoLeft = _delayLeft[readIndex];
            float echoRight = _delayRight[readIndex];
            _delayLeft[_delayIndex] = send * 0.6f + echoRight * 0.42f;
            _delayRight[_delayIndex] = echoLeft * 0.42f;
            _delayIndex = (_delayIndex + 1) % _delayLeft.Length;

            _cutoff += (cutoffTarget - _cutoff) * 0.0003f;
            _filterLeft += (dry + echoLeft - _filterLeft) * _cutoff;
            _filterRight += (dry + echoRight - _filterRight) * _cutoff;

            data[i] += SoftClip(_filterLeft * Volume);
            if (channels > 1) data[i + 1] += SoftClip(_filterRight * Volume);
            for (int c = 2; c < channels; c++) data[i + c] += data[i];
        }

        Interlocked.Exchange(ref _beatTicks, (long)(_beat * BeatTicks));
        Interlocked.Increment(ref _renderCount);
    }

    static float SoftClip(float x)
    {
        x = Mathf.Clamp(x, -3f, 3f);
        return x * (27f + x * x) / (27f + 9f * x * x);
    }

    void DrainQueue()
    {
        int tail = Volatile.Read(ref _queueTail);
        int head = Volatile.Read(ref _queueHead);
        while (tail != head)
        {
            StartVoice(_queue[tail]);
            tail = (tail + 1) % QueueSize;
        }
        Volatile.Write(ref _queueTail, tail);
    }

    void StartVoice(Trigger trigger)
    {
        int slot = 0;
        float quietest = float.MaxValue;
        for (int i = 0; i < VoiceCount; i++)
        {
            float amplitude = _voices[i].Active ? _voices[i].Amplitude : -1f;
            if (amplitude < quietest)
            {
                quietest = amplitude;
                slot = i;
            }
        }
        _voices[slot] = new Voice
        {
            Active = true,
            Frequency = trigger.Frequency,
            Amplitude = trigger.Amplitude,
            DecayFactor = DecayFactor(trigger.Decay),
            SlideFactor = Mathf.Pow(2f, trigger.Slide / _sampleRate),
            Wave = trigger.Wave
        };
    }

    float DecayFactor(float seconds) => Mathf.Exp(-6.9f / (seconds * _sampleRate));

    void OnStep(long step)
    {
        int inBar = (int)(step % 16);
        if (inBar == 0)
        {
            _bpm = TargetBpm;
            _chord = (int)(step / 16 % Roots.Length);
        }

        int intensity = Intensity;
        bool fever = Fever;
        int chord = _chord;
        int[] tones = Chords[chord];

        if (inBar % 4 == 0 || (intensity >= 4 && inBar == 14))
        {
            _kickEnv = 1f;
            _kickPitch = 1f;
        }
        if (intensity >= 2 && inBar % 8 == 4) _snareEnv = 0.6f;
        if (intensity >= 1 && inBar % 4 == 2)
        {
            _hatEnv = 1f;
            _hatAmp = 0.22f;
        }
        else if ((intensity >= 3 || fever) && inBar % 2 == 1)
        {
            _hatEnv = 1f;
            _hatAmp = 0.09f;
        }

        if (inBar % 2 == 0 && (inBar % 4 == 2 || intensity >= 2))
        {
            _bassFrequency = Midi(Roots[chord] - 12 + (inBar == 14 ? 12 : 0));
            _bassEnv = 1f;
        }

        if (intensity >= 3 || fever)
        {
            int tone = tones[ArpPattern[inBar % ArpPattern.Length]];
            _arpFrequency = Midi(Roots[chord] + tone + (fever ? 36 : 24));
            _arpEnv = fever ? 0.28f : 0.2f;
        }
    }

    float Sample(out float send)
    {
        float kick = 0f;
        if (_kickEnv > 0.0005f)
        {
            float frequency = 46f + 130f * _kickPitch;
            _kickPhase = Mathf.Repeat(_kickPhase + frequency / _sampleRate, 1f);
            kick = Mathf.Sin(_kickPhase * TwoPi) * _kickEnv * 0.75f;
            _kickEnv *= _kickDecay;
            _kickPitch *= _kickPitchDecay;
        }
        float sidechain = 1f - _kickEnv * 0.65f;

        float noise = NextNoise();

        float snare = 0f;
        if (_snareEnv > 0.0005f)
        {
            snare = noise * _snareEnv * 0.5f;
            _snareEnv *= _snareDecay;
        }

        float hat = 0f;
        if (_hatEnv > 0.0005f)
        {
            hat = (noise - _lastNoise) * 0.5f * _hatEnv * _hatAmp;
            _hatEnv *= _hatDecay;
        }
        _lastNoise = noise;

        float bass = 0f;
        if (_bassEnv > 0.0005f)
        {
            _bassPhase = Mathf.Repeat(_bassPhase + _bassFrequency / _sampleRate, 1f);
            float raw = (_bassPhase * 2f - 1f) * 0.6f + Mathf.Sin(_bassPhase * TwoPi) * 0.6f;
            _bassLow += (raw - _bassLow) * (0.04f + 0.1f * _bassEnv);
            bass = _bassLow * _bassEnv * 0.42f * sidechain;
            _bassEnv *= _bassDecay;
        }

        float arp = 0f;
        if (_arpEnv > 0.0005f)
        {
            _arpPhase = Mathf.Repeat(_arpPhase + _arpFrequency / _sampleRate, 1f);
            float raw = _arpPhase < 0.3f ? 1f : -1f;
            _arpLow += (raw - _arpLow) * 0.22f;
            arp = _arpLow * _arpEnv * sidechain;
            _arpEnv *= _arpDecay;
        }

        float effects = SampleVoices(noise);
        send = arp + effects * 0.8f;
        return kick + snare + hat + bass + arp + effects;
    }

    float SampleVoices(float noise)
    {
        float sum = 0f;
        for (int i = 0; i < VoiceCount; i++)
        {
            ref Voice voice = ref _voices[i];
            if (!voice.Active) continue;

            voice.Phase = Mathf.Repeat(voice.Phase + voice.Frequency / _sampleRate, 1f);
            voice.Frequency *= voice.SlideFactor;
            float value;
            switch (voice.Wave)
            {
                case Wave.Square: value = voice.Phase < 0.5f ? 0.7f : -0.7f; break;
                case Wave.Noise: value = noise; break;
                default: value = Mathf.Sin(voice.Phase * TwoPi); break;
            }
            sum += value * voice.Amplitude;
            voice.Amplitude *= voice.DecayFactor;
            if (voice.Amplitude < 0.0005f) voice.Active = false;
        }
        return sum;
    }

    float NextNoise()
    {
        _noise ^= _noise << 13;
        _noise ^= _noise >> 17;
        _noise ^= _noise << 5;
        return _noise / 2147483648f - 1f;
    }
}
