using System.Collections;
using UnityEngine;
using UnityEngine.UI;

//The way out (Exit room). Locked until the generator runs, then [F]: she catches you at the door (Captured video,
//held on its last frame) and you choose. Break free and run: the "defied death" ending, or "it never worked" if all
//six story papers were read (their ending videos are on Game Over Screen). Embrace her: the Reunion video, then
//you're in her body (DaughterTransfer: Karma, or the true ending at the warhead). Missing videos are skipped
public class ExitDoor : Interactable
{
    //Optional: open door
    public Sprite openSprite;

    [Header("Exit cutscene (file names in Assets/StreamingAssets)")]
    public string capturedVideo = "Grabbed_Final.mp4";
    public string reunionVideo = "Embrace_FInal.mp4";
    public string runChoice = "Break free and run";
    [Tooltip("Embrace option before all six papers are read (you don't know what she is yet)")]
    public string embraceItChoice = "Embrace it";
    [Tooltip("Embrace option once all six papers are read")]
    public string embraceHerChoice = "Embrace her";
    string EmbraceChoice => NoteObject.ReadAllStoryNotes ? embraceHerChoice : embraceItChoice;
    [Tooltip("Dark edges round the choice (0 = none, 1 = black corners)")]
    [Range(0f, 1f)] public float choiceVignette = 0.9f;
    //Seconds the vignette takes to close in
    public float choiceVignetteFade = 0.6f;
    //Seconds to fade in from black after the Embrace video (you're in her body by then)
    public float embraceFadeIn = 2f;

    private bool running;

    void Reset()
    {
        range = 1.2f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Generator.OnFixed += Open;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Generator.OnFixed -= Open;
    }

    void Open()
    {
        if(openSprite != null) GetComponent<SpriteRenderer>().sprite = openSprite;
    }

    public override string Prompt(BodyScript body)
    {
        return Generator.IsFixed ? "Escape" : "Exit (no power)";
    }

    public override bool CanInteract(BodyScript body)
    {
        //In her body you won't let her out
        return Generator.IsFixed && !DaughterTransfer.Active && !running;
    }

    protected override string DefaultThought(BodyScript body)
    {
        return DaughterTransfer.Active ? Lore.ExitAsDaughter : "No power to the door. The generator has to be running first.";
    }

    public override void Interact(BodyScript body)
    {
        StartCoroutine(Sequence(body));
    }

    IEnumerator Sequence(BodyScript body)
    {
        running = true;
        float oldScale = Time.timeScale;
        Time.timeScale = 0f;
        //She catches you at the door; the last frame stays up under the choice
        if(!string.IsNullOrEmpty(capturedVideo))
        {
            yield return CutscenePlayer.Play(capturedVideo, CutscenePlayer.AnySkipKey, null, 10f, true);
        }

        bool embrace = false;
        yield return Choose(result => embrace = result);
        CutscenePlayer.Release();

        if(!embrace)
        {
            //Defying death: running out. Read all six papers and the truth comes out instead
            GameOverScreen end = GameManager.Get<GameOverScreen>();
            if(end != null)
            {
                if(NoteObject.ReadAllStoryNotes) end.ShowEnding("IT NEVER WORKED", Lore.EndingNeverWorked);
                else end.ShowEnding("DEFIED DEATH", Lore.EndingDefyDeath);
            }
            yield break;
        }

        if(!string.IsNullOrEmpty(reunionVideo))
        {
            yield return CutscenePlayer.Play(reunionVideo, CutscenePlayer.AnySkipKey, null);
        }
        //Straight to black as the video ends, swap into her behind it, then fade back in
        BlackScreenUI black = GameManager.Get<BlackScreenUI>();
        if(black != null)
        {
            black.SetText("", "");
            yield return black.FadeIn(0f);
        }
        DaughterTransfer transfer = GameManager.Get<DaughterTransfer>();
        if(transfer != null) transfer.Embrace(body);
        if(black != null) yield return black.FadeOut(embraceFadeIn);
        Time.timeScale = oldScale > 0f ? oldScale : 1f;
        running = false;
    }

    //Two options at the bottom of the screen: [<-]/[->] to pick, [F]/[Enter] to confirm. Real time (the game is paused)
    IEnumerator Choose(System.Action<bool> chose)
    {
        GameObject canvasObj = new GameObject("ExitChoice");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        //Over the held video frame
        canvas.sortingOrder = 210;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        //Vignette first so the options draw over it
        RawImage vignette = new GameObject("Vignette").AddComponent<RawImage>();
        vignette.transform.SetParent(canvasObj.transform, false);
        vignette.texture = PlayerHUD.MakeVignetteTexture();
        vignette.raycastTarget = false;
        vignette.color = new Color(0f, 0f, 0f, 0f);
        RectTransform vr = vignette.rectTransform;
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = vr.offsetMax = Vector2.zero;
        float vignetteAge = 0f;
        Text left = ChoiceText(canvasObj.transform, runChoice, new Vector2(-220f, 90f));
        Text right = ChoiceText(canvasObj.transform, EmbraceChoice, new Vector2(220f, 90f));
        Text hint = ChoiceText(canvasObj.transform, "[<- ->] choose   [F] confirm", new Vector2(0f, 40f));
        hint.fontSize = 20;

        bool embrace = false;
        //Wait for the key that skipped / ended the video to come up first
        yield return null;
        while(true)
        {
            vignetteAge += Time.unscaledDeltaTime;
            vignette.color = new Color(0f, 0f, 0f, choiceVignette * Mathf.Clamp01(vignetteAge / Mathf.Max(0.01f, choiceVignetteFade)));
            if(Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) embrace = false;
            if(Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) embrace = true;
            left.text = (!embrace ? "> " : "  ") + runChoice + (!embrace ? " <" : "  ");
            right.text = (embrace ? "> " : "  ") + EmbraceChoice + (embrace ? " <" : "  ");
            left.color = !embrace ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            right.color = embrace ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if(Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) break;
            yield return null;
        }
        Destroy(canvasObj);
        chose(embrace);
    }

    static Text ChoiceText(Transform parent, string text, Vector2 position)
    {
        Text t = new GameObject(text).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = GameFont.Get();
        t.fontSize = 30;
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.anchoredPosition = position;
        r.sizeDelta = new Vector2(420f, 40f);
        return t;
    }
}
