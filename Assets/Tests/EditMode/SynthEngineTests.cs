using System;
using System.IO;
using NUnit.Framework;

public class SynthEngineTests
{
    const int SampleRate = 48000;

    [TestCase(0, false)]
    [TestCase(2, false)]
    [TestCase(4, true)]
    public void OutputIsAudibleAndNeverClips(int intensity, bool fever)
    {
        SynthEngine engine = new SynthEngine(SampleRate) { Intensity = intensity, Fever = fever, TargetBpm = 140f };
        float[] block = new float[1024];
        float peak = 0f;
        double energy = 0.0;
        int samples = 0;

        for (int i = 0; i < 2000; i++)
        {
            if (i % 8 == 0) engine.Play(engine.ChordFrequency(i % 12, 1), 0.32f, 0.3f, SynthEngine.Wave.Square);
            Array.Clear(block, 0, block.Length);
            engine.Render(block, 2);
            foreach (float sample in block)
            {
                Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                peak = Math.Max(peak, Math.Abs(sample));
                energy += sample * sample;
                samples++;
            }
        }

        double rms = Math.Sqrt(energy / samples);
        UnityEngine.Debug.Log($"[Synth] intensity {intensity} fever {fever} : peak {peak:0.00}, rms {rms:0.000}");
        Assert.LessOrEqual(peak, 1f);
        Assert.Greater(rms, 0.05);
    }

    [Test]
    public void BeatClockFollowsTempo()
    {
        SynthEngine engine = new SynthEngine(SampleRate) { TargetBpm = 120f };
        float[] block = new float[1024];
        for (int i = 0; i < SampleRate * 10 / 512; i++) engine.Render(block, 2);
        Assert.AreEqual(20.0, engine.Beat, 0.5);
    }

    // Permet d'écouter le rendu hors du jeu : export WAV si AXOM_WAV pointe vers un fichier.
    [Test]
    public void ExportWavWhenRequested()
    {
        string path = Environment.GetEnvironmentVariable("AXOM_WAV");
        if (string.IsNullOrEmpty(path)) Assert.Ignore("AXOM_WAV non défini");

        SynthEngine engine = new SynthEngine(SampleRate) { Intensity = 4, TargetBpm = 130f };
        float[] block = new float[2048];
        using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
        {
            const int blocks = 1000;
            int bytes = blocks * block.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + bytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)2);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 4);
            writer.Write((short)4);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(bytes);
            for (int i = 0; i < blocks; i++)
            {
                Array.Clear(block, 0, block.Length);
                engine.Render(block, 2);
                foreach (float sample in block) writer.Write((short)(sample * 32767f));
            }
        }
    }
}
