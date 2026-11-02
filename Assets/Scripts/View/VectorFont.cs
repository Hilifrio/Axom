using System.Collections.Generic;
using UnityEngine;

// Police vectorielle façon borne d'arcade : chaque glyphe est une suite de traits sur une grille 4x6.
public static class VectorFont
{
    public const float GlyphHeight = 6f;
    public const float Advance = 5.4f;
    const float GlyphWidth = 4f;

    const string Round = "1,0 3,0 4,1 4,5 3,6 1,6 0,5 0,1 1,0";
    const string Bowl = "0,0 0,6 3,6 4,5 4,4 3,3 0,3";

    static readonly Dictionary<char, string> Definitions = new Dictionary<char, string>
    {
        { '0', Round + "|1,1 3,5" },
        { '1', "1,5 2,6 2,0|1,0 3,0" },
        { '2', "0,5 1,6 3,6 4,5 4,4 0,0 4,0" },
        { '3', "0,5 1,6 3,6 4,5 4,4 3,3 1,3|3,3 4,2 4,1 3,0 1,0 0,1" },
        { '4', "3,0 3,6 0,2 4,2" },
        { '5', "4,6 0,6 0,3 3,3 4,2 4,1 3,0 1,0 0,1" },
        { '6', "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2 3,3 0,3" },
        { '7', "0,6 4,6 1,0" },
        { '8', "1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3" },
        { '9', "0,1 1,0 3,0 4,1 4,5 3,6 1,6 0,5 0,4 1,3 4,3" },
        { 'A', "0,0 0,4 2,6 4,4 4,0|0,2 4,2" },
        { 'B', "0,0 0,6 3,6 4,5 4,4 3,3 0,3|3,3 4,2 4,1 3,0 0,0" },
        { 'C', "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1" },
        { 'D', "0,0 0,6 2,6 4,4 4,2 2,0 0,0" },
        { 'E', "4,6 0,6 0,0 4,0|0,3 3,3" },
        { 'F', "4,6 0,6 0,0|0,3 3,3" },
        { 'G', "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2,3" },
        { 'H', "0,0 0,6|4,0 4,6|0,3 4,3" },
        { 'I', "1,0 3,0|2,0 2,6|1,6 3,6" },
        { 'J', "4,6 4,1 3,0 1,0 0,1" },
        { 'K', "0,0 0,6|4,6 0,3 4,0" },
        { 'L', "0,6 0,0 4,0" },
        { 'M', "0,0 0,6 2,3 4,6 4,0" },
        { 'N', "0,0 0,6 4,0 4,6" },
        { 'O', Round },
        { 'P', Bowl },
        { 'Q', Round + "|2,2 4,0" },
        { 'R', Bowl + "|2,3 4,0" },
        { 'S', "4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1" },
        { 'T', "0,6 4,6|2,6 2,0" },
        { 'U', "0,6 0,1 1,0 3,0 4,1 4,6" },
        { 'V', "0,6 2,0 4,6" },
        { 'W', "0,6 1,0 2,3 3,0 4,6" },
        { 'X', "0,0 4,6|0,6 4,0" },
        { 'Y', "0,6 2,3 4,6|2,3 2,0" },
        { 'Z', "0,6 4,6 0,0 4,0" },
        { '+', "2,1 2,5|0,3 4,3" },
        { '-', "0,3 4,3" },
        { '!', "2,6 2,2|2,0.4 2,0" },
        { '.', "2,0.4 2,0" },
        { ':', "2,1.2 2,0.8|2,4.2 2,3.8" }
    };

    static readonly Dictionary<char, Vector2[][]> Glyphs = Parse();

    public static Vector2[][] Strokes(char character)
    {
        Vector2[][] strokes;
        return Glyphs.TryGetValue(character, out strokes) ? strokes : null;
    }

    public static float Measure(string text)
    {
        return text.Length == 0 ? 0f : (text.Length - 1) * Advance + GlyphWidth;
    }

    static Dictionary<char, Vector2[][]> Parse()
    {
        Dictionary<char, Vector2[][]> glyphs = new Dictionary<char, Vector2[][]>();
        foreach (KeyValuePair<char, string> definition in Definitions)
        {
            string[] strokes = definition.Value.Split('|');
            Vector2[][] parsed = new Vector2[strokes.Length][];
            for (int s = 0; s < strokes.Length; s++)
            {
                string[] points = strokes[s].Split(' ');
                parsed[s] = new Vector2[points.Length];
                for (int p = 0; p < points.Length; p++)
                {
                    string[] xy = points[p].Split(',');
                    parsed[s][p] = new Vector2(
                        float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            glyphs[definition.Key] = parsed;
        }
        return glyphs;
    }
}
