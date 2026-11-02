using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Vérifie que les périphériques atteignent bien le jeu, à l'appui comme au relâchement.
public class InputTests : InputTestFixture
{
    string _log = "";

    void CountPress() => _log += "P";
    void CountRelease() => _log += "R";

    // Le fixture remplace l'Input System : aucun Player d'une scène précédente ne doit lui survivre.
    public override void Setup()
    {
        DestroyPlayers();
        base.Setup();
        _log = "";
    }

    public override void TearDown()
    {
        Player.OnHitPressed -= CountPress;
        Player.OnHitReleased -= CountRelease;
        DestroyPlayers();
        base.TearDown();
    }

    static void DestroyPlayers()
    {
        foreach (Player player in Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
            Object.DestroyImmediate(player.gameObject);
    }

    IEnumerator LoadGame()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        Player.OnHitPressed += CountPress;
        Player.OnHitReleased += CountRelease;
    }

    [UnityTest]
    public IEnumerator KeyboardStartsAndReleases()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        yield return LoadGame();

        Press(keyboard.spaceKey);
        yield return null;
        Assert.AreEqual("P", _log);
        Assert.AreEqual(GameManager.Screen.Playing, GameManager.Instance.CurrentScreen);

        Release(keyboard.spaceKey);
        yield return null;
        Assert.AreEqual("PR", _log);
    }

    [UnityTest]
    public IEnumerator TouchFiresOnContactNotOnLift()
    {
        Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
        yield return LoadGame();

        BeginTouch(1, Vector2.one * 100f, screen: touchscreen);
        yield return null;
        Assert.AreEqual("P", _log);

        EndTouch(1, Vector2.one * 100f, screen: touchscreen);
        yield return null;
        Assert.AreEqual("PR", _log);
    }

    [UnityTest]
    public IEnumerator MouseFiresOnPressAndRelease()
    {
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        yield return LoadGame();

        Press(mouse.leftButton);
        yield return null;
        Assert.AreEqual("P", _log);

        Release(mouse.leftButton);
        yield return null;
        Assert.AreEqual("PR", _log);
    }
}
