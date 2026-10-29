using System;
using System.Text;
using UnityEngine;

//The warhead. Looks like an objective but isn't: [F] starts an arrow-key sequence; get it right and it arms itself,
//red lights flash, 10 seconds later it's game over. Wrong key = access denied, try again. OnArmed is the hook for the true ending
public class NukeTerminal : Interactable
{
    public static event Action OnArmed;

    public int sequenceLength = 6;
    //Seconds to enter the whole sequence (real time, the game is paused)
    public float timeLimit = 6f;
    public float countdown = 10f;
    [TextArea] public string deathText = "The warhead detonated.";

    private enum State { Idle, Entering, Armed }
    private State state = State.Idle;
    private KeyCode[] sequence;
    private int progress;
    private float timer;
    private bool done;

    static readonly KeyCode[] Arrows = { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };
    static readonly string[] ArrowGlyphs = { "UP", "DOWN", "LEFT", "RIGHT" };

    void Reset()
    {
        range = 1.8f;
    }

    public override string Prompt(BodyScript body)
    {
        return state == State.Idle && !done ? "Use the warhead console" : null;
    }

    public override void Interact(BodyScript body)
    {
        sequence = new KeyCode[sequenceLength];
        for(int i = 0; i < sequence.Length; i++) sequence[i] = Arrows[UnityEngine.Random.Range(0, Arrows.Length)];
        progress = 0;
        timer = timeLimit;
        state = State.Entering;
        ModalUI.Instance.Open(this, "WARHEAD CONTROL", SequenceText(), null, "Arrow keys");
    }

    void Update()
    {
        if(state == State.Entering) UpdateEntering();
        else if(state == State.Armed) UpdateArmed();
    }

    void UpdateEntering()
    {
        timer -= Time.unscaledDeltaTime;
        if(timer <= 0f)
        {
            Fail("TIMED OUT");
            return;
        }
        for(int i = 0; i < Arrows.Length; i++)
        {
            if(!Input.GetKeyDown(Arrows[i])) continue;
            if(Arrows[i] != sequence[progress])
            {
                Fail("ACCESS DENIED");
                return;
            }
            progress++;
            if(progress >= sequence.Length)
            {
                Arm();
                return;
            }
        }
        ModalUI.Instance.SetText("WARHEAD CONTROL", SequenceText(), $"Arrow keys   {timer:0.0}s");
    }

    void Fail(string reason)
    {
        state = State.Idle;
        ModalUI.Instance.Close(this);
        Debug.Log("Warhead console: " + reason, this);
    }

    void Arm()
    {
        state = State.Armed;
        done = true;
        timer = countdown;
        ModalUI.Instance.Close(this);
        OnArmed?.Invoke();
    }

    void UpdateArmed()
    {
        timer -= Time.deltaTime;
        if(Time.timeScale > 0f) ModalUI.Instance.SetAlarm(true, $"DETONATION IN {Mathf.CeilToInt(Mathf.Max(0f, timer))}");
        if(timer > 0f) return;
        state = State.Idle;
        ModalUI.Instance.SetAlarm(false);
        Enemy.KillPlayer(deathText);
    }

    //Entered ones dimmed, the next one bracketed
    string SequenceText()
    {
        var sb = new StringBuilder("ARMING SEQUENCE\n\n");
        for(int i = 0; i < sequence.Length; i++)
        {
            string glyph = ArrowGlyphs[Array.IndexOf(Arrows, sequence[i])];
            if(i < progress) sb.Append($"<color=#555555>{glyph}</color>  ");
            else if(i == progress) sb.Append($"<b>[{glyph}]</b>  ");
            else sb.Append(glyph).Append("  ");
        }
        return sb.ToString();
    }
}
