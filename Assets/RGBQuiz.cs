using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class RGBQuiz : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable GoSel;
    public KMSelectable[] ButtonSels;
    public GameObject[] CellObjs;
    public GameObject ScreenColor;
    public GameObject[] QuestionMarks;
    public TextMesh TimerText;

    public AudioSource[] Music;

    private int moduleId;
    private static int moduleIdCounter = 1;
    private bool moduleSolved;

    private int currentStage = 0;
    private int goIx = 0;

    private int[][] possibleColors = new int[3][]
    {
        new int[3] {2, 6, 18},
        new int[6] {2, 6, 8, 18, 20, 24},
        Enumerable.Range(1, 25).ToArray()
    };
    private int[][] gridColors = new int[3][] { new int[15], new int[15], new int[15] };
    private int[] screenColors = new int[3];
    private bool[][] solutions = new bool[3][] { new bool[15], new bool[15], new bool[15] };
    private bool[] input = new bool[15];
    private int startingColor;

    private Coroutine timer;
    private const int timeDuration = 120;
    private bool autoSolving;

    private static readonly string[] solveMessages = new string[] { "NICE JOB", "NOT BAD", "GGWP", "VERY GOOD", "CONGRATS", "SOLVED", "CORRECT", ":)" };
    private static readonly string[] strikeMessages = new string[] { "TOO BAD", "NICE TRY", "VERY BAD", "BAD LUCK", "WRONG", "COME ON", ":(" };

    private void Start()
    {
        moduleId = moduleIdCounter++;
        for (int btn = 0; btn < ButtonSels.Length; btn++)
            ButtonSels[btn].OnInteract += ButtonPress(btn);
        GoSel.OnInteract += GoPress;
        GenerateColors();
        SetWhite();
        SetQuestionMarks(false);
    }

    private bool GoPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, GoSel.transform);
        GoSel.AddInteractionPunch(0.5f);
        if (moduleSolved)
            return false;
        switch (goIx)
        {
            case 0:
                goIx = 1;
                Music[0].Play();
                Music[1].Play();
                Music[0].volume = 0.25f;
                Music[1].volume = 0f;
                timer = StartCoroutine(Timer());
                TimerText.gameObject.transform.localScale = new Vector3(0.0025f, 0.0025f, 10000);
                SetColors(currentStage);
                break;
            case 1:
            case 3:
            case 5:
                goIx++;
                ScreenColor.GetComponent<MeshRenderer>().material.color = GetColor(screenColors[currentStage]);
                SetWhite();
                SetQuestionMarks(true);
                Music[0].volume = 0f;
                Music[1].volume = 0.25f;
                break;
            case 2:
            case 4:
            case 6:
                SetQuestionMarks(false);
                ScreenColor.GetComponent<MeshRenderer>().material.color = new Color(0, 0, 0);
                if (!CheckAnswer())
                {
                    Debug.LogFormat("[RGB Quiz #{0}] Incorrectly submitted {1}. Strike.", moduleId, input.Select(i => i ? "#" : "x").ToArray().Join(""));
                    goIx = 0;
                    input = input.Select(i => false).ToArray();
                    Module.HandleStrike();
                    SetWhite();
                    Music[0].Stop();
                    Music[1].Stop();
                    GenerateColors();
                    currentStage = 0;
                    if (timer != null)
                        StopCoroutine(timer);
                    TimerText.text = strikeMessages[Rnd.Range(0, strikeMessages.Length)];
                    TimerText.gameObject.transform.localScale = new Vector3(0.0015f, 0.0015f, 10000);
                }
                else
                {
                    Debug.LogFormat("[RGB Quiz #{0}] Correctly submitted {1}.", moduleId, input.Select(i => i ? "#" : "x").ToArray().Join(""));
                    input = input.Select(i => false).ToArray();
                    Music[0].volume = 0.25f;
                    Music[1].volume = 0f;
                    goIx++;
                    currentStage++;
                    if (currentStage != 3)
                        SetColors(currentStage);
                    else
                    {
                        Music[0].Stop();
                        Music[1].Stop();
                        moduleSolved = true;
                        Module.HandlePass();
                        Debug.LogFormat("[RGB Quiz #{0}] Module solved.", moduleId);
                        SetWhite();
                        if (timer != null)
                            StopCoroutine(timer);
                        TimerText.text = solveMessages[Rnd.Range(0, solveMessages.Length)];
                        TimerText.gameObject.transform.localScale = new Vector3(0.0015f, 0.0015f, 10000);
                    }
                }
                break;
        }
        return false;
    }

    private KMSelectable.OnInteractHandler ButtonPress(int btn)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, ButtonSels[btn].transform);
            ButtonSels[btn].AddInteractionPunch(0.2f);
            if (moduleSolved)
                return false;
            switch (goIx)
            {
                case 2:
                case 4:
                case 6:
                    input[btn] = !input[btn];
                    CellObjs[btn].GetComponent<MeshRenderer>().material.color = !input[btn] ? new Color(1, 1, 1) : new Color(0.5f, 1, 0.5f);
                    break;
                default:
                    break;
            }
            return false;
        };
    }

    private void GenerateColors()
    {
        startOver:
        int sc = 0;
        for (int st = 0; st < 3; st++)
        {
            screenColors[st] = possibleColors[st][Rnd.Range(0, possibleColors[st].Length)];
            for (int i = 0; i < gridColors[st].Length; i++)
                gridColors[st][i] = possibleColors[st][Rnd.Range(0, possibleColors[st].Length)];
            if (st == 0)
            {
                int red = 0;
                int green = 0;
                int blue = 0;
                for (int i = 0; i < gridColors[st].Length; i++)
                {
                    if (gridColors[st][i] == 18)
                        red++;
                    if (gridColors[st][i] == 6)
                        green++;
                    if (gridColors[st][i] == 2)
                        blue++;
                }
                if (new[] { red, green, blue }.Distinct().Count() != 3)
                    goto startOver;
                startingColor = red > green && red > blue ? 0 : green > red && green > blue ? 1 : 2;
                sc = startingColor;
            }
            Debug.LogFormat("[RGB Quiz #{0}] Stage {1}, color index: {2}", moduleId, st + 1, sc == 0 ? "red" : sc == 1 ? "green" : "blue");
            for (int i = 0; i < solutions[st].Length; i++)
                solutions[st][i] = GetColorNums(gridColors[st][i])[sc] == GetColorNums(screenColors[st])[sc];
            sc = (sc + 1) % 3;
            Debug.LogFormat("[RGB Quiz #{0}] Stage {1}, Grid:", moduleId, st + 1);
            Debug.LogFormat("[RGB Quiz #{0}] {1} {2} {3} {4} {5}", moduleId,
                GetColorNums(gridColors[st][0]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][1]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][2]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][3]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][4]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join("")
                );
            Debug.LogFormat("[RGB Quiz #{0}] {1} {2} {3} {4} {5}", moduleId,
                GetColorNums(gridColors[st][5]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][6]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][7]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][8]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][9]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join("")
                );
            Debug.LogFormat("[RGB Quiz #{0}] {1} {2} {3} {4} {5}", moduleId,
                GetColorNums(gridColors[st][10]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][11]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][12]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][13]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""),
                GetColorNums(gridColors[st][14]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join("")
                );
            Debug.LogFormat("[RGB Quiz #{0}] Stage {1}, Screen color: {2}", moduleId, st + 1, GetColorNums(screenColors[st]).Select(i => i * 2 == 2 ? "+" : i * 2 == 1 ? "0" : "-").ToArray().Join(""));
            Debug.LogFormat("[RGB Quiz #{0}] Stage {1}, Solution:", moduleId, st + 1);
            Debug.LogFormat("[RGB Quiz #{0}] {1} {2} {3} {4} {5}", moduleId,
                solutions[st][0] ? "#" : "x",
                solutions[st][1] ? "#" : "x",
                solutions[st][2] ? "#" : "x",
                solutions[st][3] ? "#" : "x",
                solutions[st][4] ? "#" : "x"
                );
            Debug.LogFormat("[RGB Quiz #{0}] {1} {2} {3} {4} {5}", moduleId,
                solutions[st][5] ? "#" : "x",
                solutions[st][6] ? "#" : "x",
                solutions[st][7] ? "#" : "x",
                solutions[st][8] ? "#" : "x",
                solutions[st][9] ? "#" : "x"
                );
            Debug.LogFormat("[RGB Quiz #{0}] {1} {2} {3} {4} {5}", moduleId,
                solutions[st][10] ? "#" : "x",
                solutions[st][11] ? "#" : "x",
                solutions[st][12] ? "#" : "x",
                solutions[st][13] ? "#" : "x",
                solutions[st][14] ? "#" : "x"
                );
        }
    }

    private void SetColors(int st)
    {
        for (int i = 0; i < gridColors[st].Length; i++)
            CellObjs[i].GetComponent<MeshRenderer>().material.color = GetColor(gridColors[st][i]);
    }

    private void SetWhite()
    {
        for (int i = 0; i < 15; i++)
            CellObjs[i].GetComponent<MeshRenderer>().material.color = new Color(1, 1, 1);
    }

    private void SetQuestionMarks(bool active)
    {
        for (int i = 0; i < 15; i++)
            QuestionMarks[i].SetActive(active);
    }

    private Color GetColor(int num)
    {
        return new Color(GetColorNums(num)[0], GetColorNums(num)[1], GetColorNums(num)[2]);
    }

    private float[] GetColorNums(int num)
    {
        return (new int[3] { num / 9, num % 9 / 3, num % 3 }).Select(i => (float)i / 2).ToArray();
    }

    private bool CheckAnswer()
    {
        for (int i = 0; i < 15; i++)
            if (input[i] != solutions[currentStage][i])
                return false;
        return true;
    }

    private IEnumerator Timer()
    {
        for (int i = timeDuration; i >= 0; i--)
        {
            TimerText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }
        if (autoSolving) // In the case the module is force solved when there's not enough time on the timer.
            yield break;
        Debug.LogFormat("[RGB Quiz #{0}] Ran out of time. Strike.", moduleId);
        goIx = 0;
        input = input.Select(i => false).ToArray();
        Module.HandleStrike();
        SetWhite();
        Music[0].Stop();
        Music[1].Stop();
        GenerateColors();
        currentStage = 0;
        TimerText.text = strikeMessages[Rnd.Range(0, strikeMessages.Length)];
        TimerText.gameObject.transform.localScale = new Vector3(0.0015f, 0.0015f, 10000);
        yield break;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "Press the 'GO' button with !{0} go. Submit a group of cells with !{0} submit a1 b2 c3 d1 e2.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var parameters = command.Split(' ');
        if (goIx == 2 || goIx == 4 || goIx == 6)
        {
            if (parameters[0] == "go")
            {
                yield return "sendtochaterror You must use 'submit' to submit a group of cells to press!";
                yield break;
            }
            if (parameters[0] != "submit")
                yield break;
            var list = new List<int>();
            for (int i = 1; i < parameters.Length; i++)
            {
                int ix = GetNumFromCoord(parameters[i]);
                if (ix == -1)
                    yield break;
                list.Add(ix);
            }
            if (list.Distinct().Count() != list.Count())
            {
                yield return "sendtochaterror You have duplicate inputs!";
                yield break;
            }
            yield return null;
            for (int i = 0; i < list.Count; i++)
            {
                ButtonSels[list[i]].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            GoSel.OnInteract();
        }
        else
        {
            if (parameters[0] == "submit")
            {
                yield return "sendtochaterror You cannot submit a group of cells in this phase!";
                yield break;
            }
            if (parameters[0] != "go")
                yield break;
            yield return null;
            GoSel.OnInteract();
        }
        yield break;
    }

    private int GetNumFromCoord(string s)
    {
        return Array.IndexOf(new[] { "a1", "b1", "c1", "d1", "e1", "a2", "b2", "c2", "d2", "e2", "a3", "b3", "c3", "d3", "e3" }, s);
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        autoSolving = true;
        while (!moduleSolved)
        {
            if (goIx == 2 || goIx == 4 || goIx == 6)
            {
                for (int i = 0; i < 15; i++)
                {
                    if (input[i] != solutions[currentStage][i])
                    {
                        ButtonSels[i].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }
            GoSel.OnInteract();
            if (!moduleSolved)
                yield return new WaitForSeconds(0.1f);
        }
    }
}
