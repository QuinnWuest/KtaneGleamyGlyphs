using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class GleamyGlyphsScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public KMSelectable[] ButtonSels;
    public GameObject[] GlyphObjs;
    public Mesh[] GlyphMeshes;
    public Light[] GlyphLights;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private static readonly int[][] _hexIxs = new int[][]
    {
        new int[] { 00, 01, 05, 06, 07, 12, 13 },
        new int[] { 01, 02, 06, 07, 08, 13, 14 },
        new int[] { 02, 03, 07, 08, 09, 14, 15 },
        new int[] { 03, 04, 08, 09, 10, 15, 16 },
        new int[] { 05, 06, 11, 12, 13, 19, 20 },
        new int[] { 06, 07, 12, 13, 14, 20, 21 },
        new int[] { 07, 08, 13, 14, 15, 21, 22 },
        new int[] { 08, 09, 14, 15, 16, 22, 23 },
        new int[] { 09, 10, 15, 16, 17, 23, 24 },
        new int[] { 11, 12, 18, 19, 20, 27, 28 },
        new int[] { 12, 13, 19, 20, 21, 28, 29 },
        new int[] { 13, 14, 20, 21, 22, 29, 30 },
        new int[] { 14, 15, 21, 22, 23, 30, 31 },
        new int[] { 15, 16, 22, 23, 24, 31, 32 },
        new int[] { 16, 17, 23, 24, 25, 32, 33 },
        new int[] { 18, 19, 26, 27, 28, 35, 36 },
        new int[] { 19, 20, 27, 28, 29, 36, 37 },
        new int[] { 20, 21, 28, 29, 30, 37, 38 },
        new int[] { 21, 22, 29, 30, 31, 38, 39 },
        new int[] { 22, 23, 30, 31, 32, 39, 40 },
        new int[] { 23, 24, 31, 32, 33, 40, 41 },
        new int[] { 24, 25, 32, 33, 34, 41, 42 },
        new int[] { 27, 28, 35, 36, 37, 43, 44 },
        new int[] { 28, 29, 36, 37, 38, 44, 45 },
        new int[] { 29, 30, 37, 38, 39, 45, 46 },
        new int[] { 30, 31, 38, 39, 40, 46, 47 },
        new int[] { 31, 32, 39, 40, 41, 47, 48 },
        new int[] { 32, 33, 40, 41, 42, 48, 49 },
        new int[] { 36, 37, 43, 44, 45, 50, 51 },
        new int[] { 37, 38, 44, 45, 46, 51, 52 },
        new int[] { 38, 39, 45, 46, 47, 52, 53 },
        new int[] { 39, 40, 46, 47, 48, 53, 54 },
        new int[] { 40, 41, 47, 48, 49, 54, 55 },
        new int[] { 44, 45, 50, 51, 52, 56, 57 },
        new int[] { 45, 46, 51, 52, 53, 57, 58 },
        new int[] { 46, 47, 52, 53, 54, 58, 59 },
        new int[] { 47, 48, 53, 54, 55, 59, 60 }
    };
    private List<int> _nums = new List<int>();
    private int _correctPosition;
    private static readonly string[] _positionNames = new string[] { "top-left", "top-right", "middle-left", "middle", "middle-right", "bottom-left", "bottom-right" };
    private readonly bool[] _clicked = new bool[7];

    private void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);

        // Start rule seed.
        var rnd = RuleSeedable.GetRNG();
        _nums = new List<int>();
        for (int i = 0; i < 130; i++)
        {
            _nums.Add(i % 13);
            rnd.Next(0, 4);
            rnd.Next(0, 8);
        }
        bool distinct = false;
        while (!distinct)
        {
            var tempHexes = new List<List<int>>();
            rnd.ShuffleFisherYates(_nums);
            distinct = true;
            for (int i = 0; i < _hexIxs.Length; i++)
            {
                var arr = new List<int>();
                for (int j = 0; j < 7; j++)
                    arr.Add(_nums[_hexIxs[i][j]]);
                if (!IsOkayHexagon(arr))
                {
                    distinct = false;
                    break;
                }
                arr.OrderBy(x => x);
                if (tempHexes.Any(x => arr.SequenceEqual(x)))
                    distinct = false;
                if (!distinct)
                    break;
                tempHexes.Add(arr);
            }
        }
        _nums = _nums.Take(61).ToList();

        // Randomly flip the glyphs on the module.
        for (int i = 0; i < GlyphObjs.Length; i++)
        {
            var sc = 0.07f;
            var x = Rnd.Range(0, 2) == 0 ? -sc : sc;
            var z = Rnd.Range(0, 2) == 0 ? -sc : sc;
            var r = Rnd.Range(0, 2) == 0 ? 45f : 0f;
            GlyphObjs[i].transform.localScale = new Vector3(x, sc, z);
            GlyphObjs[i].transform.localEulerAngles = new Vector3(0, r, 0);
        }

        // Decide on a hexagon.
        tryGetHexagon:
        var randHex = _hexIxs.PickRandom().ToArray();
        var chosenHexagon = randHex.ToArray().Shuffle();
        var match = GetMatchingIxs(randHex, chosenHexagon);
        if (match.Count != 1)
            goto tryGetHexagon;
        _correctPosition = match.First();
        for (int i = 0; i < 7; i++)
            GlyphObjs[i].GetComponent<MeshFilter>().mesh = GlyphMeshes[_nums[chosenHexagon[i]]];

        var lightScale = transform.lossyScale.x;
        foreach (var light in GlyphLights)
            light.range *= lightScale;

        StartCoroutine(GlyphFloatAnimation());
        Debug.LogFormat("[Gleamy Glyphs #{0}] The positions of the hexagon in the grid: {1}", _moduleId, randHex.Select(i => i + 1).Join(", "));
        Debug.LogFormat("[Gleamy Glyphs #{0}] The {1} glyph is in the same position both on the module and in the hexagon.", _moduleId, _positionNames[_correctPosition]);
    }

    private bool IsOkayHexagon(List<int> hex)
    {
        for (int img = 0; img < 14; img++)
        {
            int c = 0;
            for (int ix = 0; ix < hex.Count; ix++)
                if (hex[ix] == img)
                    c++;
            if (c >= 3)
                return false;
        }
        return true;
    }

    private KMSelectable.OnInteractHandler ButtonPress(int i)
    {
        return delegate ()
        {
            ButtonSels[i].AddInteractionPunch(0.35f);
            if (_moduleSolved)
                return false;
            if (i == _correctPosition)
            {
                Debug.LogFormat("[Gleamy Glyphs #{0}] Correctly pressed the {1} glyph. Module solved.", _moduleId, _positionNames[i]);
                StartCoroutine(ColorGlyph(GlyphObjs[i], 2));
                Audio.PlaySoundAtTransform("twinkle", transform);
                _moduleSolved = true;
                Module.HandlePass();
            }
            else
            {

                Debug.LogFormat("[Gleamy Glyphs #{0}] Incorrectly pressed the {1} glyph. Strike.", _moduleId, _positionNames[i]);
                if (!_clicked[i])
                {
                    _clicked[i] = true;
                    StartCoroutine(ColorGlyph(GlyphObjs[i], 1));
                }
                Module.HandleStrike();
            }
            return false;
        };
    }

    private IEnumerator GlyphFloatAnimation()
    {
        while (true)
        {
            var duration = 5f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                for (int i = 0; i < 7; i++)
                    GlyphObjs[i].transform.localPosition = new Vector3(0, 0, Easing.InOutQuad(elapsed, 0.005f, -0.005f, duration));
                yield return null;
                elapsed += Time.deltaTime;
            }
            elapsed = 0f;
            while (elapsed < duration)
            {
                for (int i = 0; i < 7; i++)
                    GlyphObjs[i].transform.localPosition = new Vector3(0, 0, Easing.InOutQuad(elapsed, -0.005f, 0.005f, duration));
                yield return null;
                elapsed += Time.deltaTime;
            }
        }
    }

    private List<int> GetMatchingIxs(int[] a, int[] b)
    {
        var na = a.Select(i => _nums[i]).ToArray();
        var nb = b.Select(i => _nums[i]).ToArray();
        var list = new List<int>();
        for (int i = 0; i < 7; i++)
            if (na[i] == nb[i])
                list.Add(i);
        return list;
    }

    private IEnumerator ColorGlyph(GameObject obj, int color)
    {
        var red = new Color32(255, 0, 60, 220);
        var green = new Color32(0, 255, 60, 220);
        var duration = 0.2f;
        var elapsed = 0f;
        var oldC = new Color32(170, 135, 255, 220);
        var newC = color == 1 ? red : green;
        while (elapsed < duration)
        {
            obj.GetComponent<MeshRenderer>().material.color = new Color32((byte)Mathf.Lerp(oldC.r, newC.r, elapsed / duration), (byte)Mathf.Lerp(oldC.g, newC.g, elapsed / duration), (byte)Mathf.Lerp(oldC.b, newC.b, elapsed / duration), 220);
            yield return null;
            elapsed += Time.deltaTime;
        }
        obj.GetComponent<MeshRenderer>().material.color = newC;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press 1 [Press glyph in position 1 in reading order.] | !{0} press tr [Press glyph in the top-right position. | Reading order positions are 1-7. Directional positions are TL, TR, ML, MM, MR, BL, BR";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*(?:press\s+)?(?<num>\d)?(?<pos>tl|tr|ml|mm?|mr|bl|br)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        if (m.Groups["num"].Success && m.Groups["pos"].Success)
            yield break;
        if (m.Groups["num"].Success)
        {
            var d = int.Parse(m.Groups["num"].Value) - 1;
            if (d < 0 || d > 6)
                yield break;
            yield return null;
            ButtonSels[d].OnInteract();
            yield break;
        }
        if (m.Groups["pos"].Success)
        {
            var a = new[] { "tl", "tr", "ml", "mm", "mr", "bl", "br", null, null, null, "m", null, null, null };
            int ix = Array.IndexOf(a, m.Groups["pos"].Value);
            if (ix == -1)
                yield break;
            ix = ix % 7;
            yield return null;
            ButtonSels[ix].OnInteract();
            yield break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        ButtonSels[_correctPosition].OnInteract();
        yield break;
    }
}