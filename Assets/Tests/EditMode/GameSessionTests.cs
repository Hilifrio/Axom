using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GameSessionTests
{
    GameSession NewSession(float orbAngle = 90f, int direction = -1)
    {
        GameSession session = new GameSession(new GameConfig(), 1, orbAngle, direction);
        session.Begin();
        return session;
    }

    static Zone AddZone(GameSession session, ZoneType type, float center, float half = 30f)
    {
        Zone zone = new Zone { Type = type, Center = center, Half = half, BaseHalf = half };
        session.Zones.Add(zone);
        return zone;
    }

    [Test]
    public void CenterPressIsPerfect()
    {
        GameSession session = NewSession(90f);
        AddZone(session, ZoneType.Tap, 90f);
        List<GameEvent> events = Record(session);

        session.Press();

        Assert.AreEqual(HitTier.Perfect, events[0].Tier);
        Assert.AreEqual(100, session.Score);
        Assert.AreEqual(1, session.Combo);
    }

    [Test]
    public void CloserToCenterScoresMore()
    {
        int previous = int.MaxValue;
        foreach (float offset in new[] { 0f, 12f, 18f, 24f, 29f })
        {
            GameSession session = NewSession(90f + offset);
            AddZone(session, ZoneType.Tap, 90f);
            session.Press();
            Assert.Less(session.Score, previous, "offset " + offset);
            previous = session.Score;
        }
    }

    [Test]
    public void HitWorksAcrossZeroDegrees()
    {
        GameSession session = NewSession(359f);
        AddZone(session, ZoneType.Tap, 2f, 20f);

        session.Press();

        Assert.AreEqual(1, session.HitCount);
    }

    [Test]
    public void EmptyTapResetsComboButKeepsLife()
    {
        GameSession session = NewSession(90f);
        AddZone(session, ZoneType.Tap, 90f);
        session.Press();

        session.Press();

        Assert.AreEqual(0, session.Combo);
        Assert.AreEqual(session.Config.startLives, session.Lives);
    }

    [Test]
    public void MineCostsALife()
    {
        GameSession session = NewSession(90f);
        Zone mine = AddZone(session, ZoneType.Mine, 90f, 15f);

        session.Press();

        Assert.IsTrue(mine.Done);
        Assert.AreEqual(session.Config.startLives - 1, session.Lives);
    }

    [Test]
    public void FlipReversesOrbit()
    {
        GameSession session = NewSession(90f, -1);
        AddZone(session, ZoneType.Flip, 90f);

        session.Press();

        Assert.AreEqual(1, session.Direction);
    }

    [Test]
    public void PassingAZoneCostsALife()
    {
        GameSession session = NewSession(100f, -1);
        AddZone(session, ZoneType.Tap, 90f, 20f);

        Run(session, 0.8f, null);

        Assert.AreEqual(session.Config.startLives - 1, session.Lives);
        Assert.AreEqual(0, session.Combo);
    }

    [Test]
    public void IgnoringEverythingEndsTheRun()
    {
        GameSession session = NewSession();

        Run(session, 60f, null);

        Assert.IsTrue(session.IsOver);
        Assert.AreEqual(0, session.Lives);
    }

    [Test]
    public void HoldPressedAndReleasedOnTimeScoresTwice()
    {
        GameSession session = NewSession(135f, -1);
        Zone hold = AddZone(session, ZoneType.Hold, 90f, 45f);
        List<GameEvent> events = Record(session);

        session.Press();
        Assert.AreSame(hold, session.ActiveHold);
        hold.HoldTravelled = 90f;
        session.Release();

        Assert.IsNull(session.ActiveHold);
        Assert.IsTrue(hold.Done);
        Assert.AreEqual(HitTier.Perfect, events[0].Tier);
        Assert.AreEqual(HitTier.Perfect, events[1].Tier);
        Assert.AreEqual(2, session.Combo);
    }

    [Test]
    public void EarlyHoldReleaseBreaksComboButKeepsLife()
    {
        GameSession session = NewSession(135f, -1);
        Zone hold = AddZone(session, ZoneType.Hold, 90f, 45f);

        session.Press();
        hold.HoldTravelled = 30f;
        session.Release();

        Assert.AreEqual(0, session.Combo);
        Assert.AreEqual(session.Config.startLives, session.Lives);
    }

    [Test]
    public void GoldRestoresALife()
    {
        GameSession session = NewSession(90f);
        AddZone(session, ZoneType.Mine, 90f, 5f);
        session.Press();
        Assert.AreEqual(2, session.Lives);

        AddZone(session, ZoneType.Gold, 90f, 10f);
        session.Press();

        Assert.AreEqual(3, session.Lives);
    }

    [Test]
    public void FeverStartsWhenGaugeIsFullAndProtects()
    {
        GameSession session = NewSession(90f);
        for (int i = 0; i < 10; i++)
        {
            AddZone(session, ZoneType.Tap, 90f);
            session.Press();
            session.Zones.Clear();
        }
        Assert.IsTrue(session.IsFever);
        int combo = session.Combo;

        AddZone(session, ZoneType.Mine, 90f, 5f);
        session.Press();

        Assert.AreEqual(session.Config.startLives, session.Lives);
        Assert.AreEqual(combo, session.Combo);
    }

    // Un joueur parfait ne doit jamais perdre de vie : toute configuration générée est jouable.
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void PerfectBotNeverLosesALife(int seed)
    {
        GameSession session = new GameSession(new GameConfig(), seed);
        session.Begin();
        AutoPlayer bot = new AutoPlayer();
        int livesLost = 0;
        string detail = "";
        session.OnEvent += e =>
        {
            if (!e.LifeLost) return;
            livesLost++;
            detail += $" | {e.Type} {e.Zone} a {e.Angle:0} orbe {session.OrbAngle:0} sens {session.Direction} hold {session.ActiveHold != null} :";
            foreach (Zone z in session.Zones) detail += $" [{z.Type} c{z.Center:0} h{z.Half:0} d{z.Drift:0} done{z.Done}]";
        };

        Run(session, 240f, bot);

        Assert.AreEqual(0, livesLost, "level " + session.Level + " score " + session.Score + detail);
        Assert.GreaterOrEqual(session.Level, 13);
    }

    [Test]
    public void ZonesNeverOverlapWhenSpawned()
    {
        for (int seed = 0; seed < 5; seed++)
        {
            GameSession session = new GameSession(new GameConfig(), seed);
            session.Begin();
            session.OnEvent += e =>
            {
                if (e.Type != GameEventType.ZoneSpawned) return;
                Zone spawned = session.Zones[session.Zones.Count - 1];
                foreach (Zone other in session.Zones)
                    if (other != spawned && !other.Done)
                        Assert.GreaterOrEqual(other.DistanceTo(spawned.Center), other.Half + spawned.Half, spawned.Type + " / " + other.Type);
            };
            Run(session, 120f, new AutoPlayer());
        }
    }

    // Mesure l'équilibrage : affiche la durée et le score d'un joueur plus ou moins précis.
    [TestCase(0.02f)]
    [TestCase(0.04f)]
    [TestCase(0.07f)]
    [TestCase(0.1f)]
    public void HumanLikeBotReport(float errorSeconds)
    {
        const int runs = 40;
        float totalTime = 0f;
        int totalScore = 0, totalLevel = 0, best = 0;
        for (int seed = 0; seed < runs; seed++)
        {
            GameSession session = new GameSession(new GameConfig(), seed);
            session.Begin();
            float time = Run(session, 400f, new AutoPlayer(errorSeconds, seed));
            totalTime += time;
            totalScore += session.Score;
            totalLevel += session.Level;
            best = Mathf.Max(best, session.Score);
        }
        Debug.Log($"[Balance] error {errorSeconds * 1000f:0}ms : {totalTime / runs:0}s, level {(float)totalLevel / runs:0.0}, score moyen {totalScore / runs}, meilleur {best}");
        Assert.Greater(totalTime, 0f);
    }

    static List<GameEvent> Record(GameSession session)
    {
        List<GameEvent> events = new List<GameEvent>();
        session.OnEvent += e =>
        {
            if (e.Type == GameEventType.Hit || e.Type == GameEventType.HoldStarted) events.Add(e);
        };
        return events;
    }

    static float Run(GameSession session, float seconds, AutoPlayer bot)
    {
        const float dt = 1f / 60f;
        double beat = 0.0;
        float time = 0f;
        while (time < seconds && !session.IsOver)
        {
            beat += dt * session.Bpm / 60f;
            session.Tick(dt, beat);
            bot?.Step(session, dt);
            time += dt;
        }
        return time;
    }
}
