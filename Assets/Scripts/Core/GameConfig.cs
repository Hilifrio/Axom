using System;
using UnityEngine;

// Tous les réglages de gameplay, éditables dans l'inspecteur du GameManager.
// Les durées sont en secondes plutôt qu'en degrés : la difficulté reste lisible quand l'orbe accélère.
[Serializable]
public class GameConfig
{
    [Header("Run")]
    public int startLives = 3;
    public int maxLives = 3;
    [Tooltip("Après une vie perdue, les zones ratées dans ce délai ne coûtent pas de vie.")]
    public float lifeLossGrace = 0.6f;

    [Header("Tempo (1 tour de cercle = 4 temps)")]
    public float startBpm = 100f;
    public float bpmPerLevel = 6f;
    public float maxBpm = 190f;
    public int hitsPerLevel = 8;

    [Header("Timing")]
    public float perfectWindow = 0.04f;
    public float greatWindow = 0.085f;
    [Tooltip("Tolérance au bord des zones pour pardonner un appui légèrement hors zone.")]
    public float edgeGrace = 0.03f;

    [Header("Zones")]
    [Tooltip("Demi-largeur d'une zone au niveau 1, en secondes de parcours.")]
    public float startHalfSeconds = 0.21f;
    public float minHalfSeconds = 0.075f;
    public int levelsToMinSize = 14;
    [Tooltip("Temps de réaction minimum entre l'apparition d'une zone et l'arrivée de l'orbe.")]
    public float leadSeconds = 0.5f;
    public float zoneGapDegrees = 8f;
    public float driftMin = 0.12f;
    public float driftMax = 0.3f;

    [Header("Niveau d'apparition des mécaniques")]
    public int driftLevel = 2;
    public int mineLevel = 3;
    public int flipLevel = 4;
    public int holdLevel = 5;
    public int burstLevel = 6;
    public int eighthGridLevel = 3;
    public int goldLevel = 2;
    public float goldChance = 0.07f;

    [Header("Fever")]
    public float feverPerPerfect = 0.1f;
    public float feverPerGreat = 0.04f;
    public float feverPerGold = 0.25f;
    public float feverBeats = 16f;
    public int feverScoreFactor = 2;

    [Header("Score")]
    public int perfectPoints = 100;
    public int perfectStreakBonus = 10;
    public int perfectStreakBonusMax = 100;
    public int goldFactor = 3;
    public int holdTickPoints = 5;
    public int[] multiplierSteps = { 5, 15, 30, 50, 80 };
    public int[] rankThresholds = { 3000, 12000, 40000, 100000 };
}
