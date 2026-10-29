using System;
using System.Collections;
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
    [Header("Detonation")]
    //Seconds of fire and white-out before the ending / death screen
    public float explosionTime = 1.6f;
    //Seconds the explosion sound takes to fade away once the ending starts
    public float explosionFadeOut = 1f;
    public Color fireColor = new Color(1f, 0.55f, 0.15f);
    public Color coreColor = new Color(1f, 0.95f, 0.8f);

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
        if(state != State.Idle || done) return null;
        return CanInteract(body) ? "Use the warhead console" : "Warhead console";
    }

    //In her body you only know the arming codes if you read all six papers (the true ending)
    public override bool CanInteract(BodyScript body)
    {
        return !DaughterTransfer.Active || NoteObject.ReadAllStoryNotes;
    }

    protected override string DefaultThought(BodyScript body)
    {
        return Lore.NukeNoCodes;
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
        SFXManager.Alarm(true);
        OnArmed?.Invoke();
    }

    void UpdateArmed()
    {
        //Caught during the countdown: no more alarm over the death screen
        if(Enemy.PlayerDied)
        {
            state = State.Idle;
            ModalUI.Instance.SetAlarm(false);
            SFXManager.Alarm(false);
            return;
        }
        timer -= Time.deltaTime;
        if(Time.timeScale > 0f) ModalUI.Instance.SetAlarm(true, $"DETONATION IN {Mathf.CeilToInt(Mathf.Max(0f, timer))}");
        if(timer > 0f) return;
        state = State.Idle;
        ModalUI.Instance.SetAlarm(false);
        SFXManager.Alarm(false);
        StartCoroutine(Detonate());
    }

    //Boom: fire and sparks from the warhead, the screen whites out, then the ending
    IEnumerator Detonate()
    {
        SFXManager.Explosion();
        Burst(coreColor, 60, 4f, 14f, 0.5f, 0.25f);
        Burst(fireColor, 90, 2f, 9f, 1.1f, 0.35f);
        Burst(new Color(0.25f, 0.2f, 0.2f), 50, 0.5f, 3f, 1.6f, 0.5f);
        //Not fully white, so the fire still shows through
        ScreenFlash.Play(0.75f, explosionTime + 0.4f);
        yield return new WaitForSecondsRealtime(explosionTime);
        //In her body, sealing her in with you: the true ending. Any other time it's just a death
        GameOverScreen end = GameManager.Get<GameOverScreen>();
        if(DaughterTransfer.Active && end != null) end.ShowEnding("TRUE ENDING", Lore.EndingTrue);
        else Enemy.KillPlayer(deathText);
        //Gone by the time the black screen and its text are up
        SFXManager.FadeOutExplosion(explosionFadeOut);
    }

    //One ring of square particles flying out from the warhead
    void Burst(Color color, int count, float minSpeed, float maxSpeed, float life, float size)
    {
        GameObject obj = new GameObject("Explosion");
        obj.transform.position = transform.position;
        ParticleSystem ps = Projectile.MakeSystem(obj, color);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.maxParticles = count;
        //Unscaled: still flies if anything pauses
        main.useUnscaledTime = true;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        //Over everything in the world
        if(renderer != null) renderer.sortingOrder = 500;
        ps.Emit(count);
        Destroy(obj, life + 0.5f);
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
