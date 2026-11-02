using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Fait tourner la vraie scène avec un joueur automatique.
// Si AXOM_CAPTURE pointe vers un dossier, des captures d'écran y sont enregistrées.
public class GameLoopTests
{
    const float FrameTime = 1f / 60f;

    [UnityTest]
    public IEnumerator FullRunFromTitleToRetry()
    {
        PlayerPrefs.DeleteKey("axom.best");
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        Time.captureFramerate = 60;
        GameManager game = GameManager.Instance;
        game.UseAudioClock = false;
        string folder = Environment.GetEnvironmentVariable("AXOM_CAPTURE");

        Assert.AreEqual(GameManager.Screen.Title, game.CurrentScreen);
        for (int i = 0; i < 30; i++) yield return null;
        Capture(folder, "01_title");

        game.Press();
        Assert.AreEqual(GameManager.Screen.Playing, game.CurrentScreen);

        AutoPlayer bot = new AutoPlayer(0.018f, 7);
        int nextCaptureFrame = 600;
        for (int frame = 0; frame < 60 * 75; frame++)
        {
            bot.Step(game.Session, FrameTime);
            yield return null;
            if (frame >= nextCaptureFrame)
            {
                Capture(folder, $"02_play_{frame:00000}_lv{game.Session.Level}");
                nextCaptureFrame += 420;
            }
            if (folder != null && game.Session.IsFever && frame % 300 == 0) Capture(folder, $"03_fever_{frame:00000}");
            if (game.CurrentScreen != GameManager.Screen.Playing) break;
        }
        Debug.Log($"[Loop] niveau {game.Session.Level}, score {game.Session.Score}, combo max {game.Session.MaxCombo}");
        Assert.Greater(game.Session.Level, 4);

        // Sans joueur, les vies tombent et la partie se termine.
        int lastLifeFrames = 0;
        for (int frame = 0; frame < 60 * 60 && game.CurrentScreen == GameManager.Screen.Playing; frame++)
        {
            yield return null;
            if (game.Session.Lives == 1 && lastLifeFrames++ == 20) Capture(folder, "04_lastlife");
        }
        Assert.AreEqual(GameManager.Screen.Over, game.CurrentScreen);
        int score = game.Session.Score;

        game.Press();
        Assert.AreEqual(GameManager.Screen.Over, game.CurrentScreen, "le retry ignore les appuis juste après la mort");

        for (int i = 0; i < 40; i++) yield return null;
        Capture(folder, "05_gameover");

        game.Press();
        Assert.AreEqual(GameManager.Screen.Playing, game.CurrentScreen);
        Assert.AreEqual(0, game.Session.Score);
        Assert.AreEqual(score, PlayerPrefs.GetInt("axom.best"));

        for (int i = 0; i < 20; i++) yield return null;
        Capture(folder, "06_retry");

        Camera.main.aspect = 9f / 16f;
        AutoPlayer portraitBot = new AutoPlayer();
        for (int i = 0; i < 90; i++)
        {
            portraitBot.Step(game.Session, FrameTime);
            yield return null;
        }
        Capture(folder, "07_portrait", 720, 1280);
        Camera.main.ResetAspect();

        Time.captureFramerate = 0;
        PlayerPrefs.DeleteKey("axom.best");
    }

    // En temps réel, l'orbe doit rester calée sur l'horloge de la musique.
    [UnityTest]
    public IEnumerator ClockStaysLockedToTheMusic()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        GameManager game = GameManager.Instance;
        game.Press();

        float end = Time.realtimeSinceStartup + 6f;
        double worst = 0.0;
        float settle = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
            if (Time.realtimeSinceStartup > settle) worst = Math.Max(worst, Math.Abs(game.Audio.Beat - game.Beat));
        }

        Debug.Log($"[Clock] audio actif {game.AudioRunning}, rendus {game.Audio.RenderCount}, écart max {worst:0.000} temps");
        if (!game.AudioRunning) Assert.Ignore("pas de sortie audio dans cet environnement");
        Assert.Less(worst, 0.25);
    }

    static void Capture(string folder, string name, int width = 1280, int height = 720)
    {
        if (string.IsNullOrEmpty(folder)) return;

        Camera camera = Camera.main;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 16);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = previous;

        RenderTexture.active = target;
        Texture2D texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(target);

        File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);
    }
}
