using UnityEngine;
using UnityEngine.UI;

//[Esc] during play: pauses the game. Up/Down (or W/S) picks a row, Left/Right (A/D) changes a volume,
//[F]/[Enter] on Resume / Restart / Quit. Volumes are GameAudio's (saved). Keyboard only, like the rest of the game
public class PauseMenu : MonoBehaviour
{
    public KeyCode pauseKey = KeyCode.Escape;
    //Each Left/Right press
    public float volumeStep = 0.1f;

    public static bool IsOpen { get; private set; }

    private enum Row { Resume, Master, Music, Sfx, Cutscene, Restart, Quit }
    static readonly string[] Labels = { "Resume", "Master", "Music", "SFX", "Cutscene", "Restart", "Quit" };

    private GameObject canvasObj;
    private Text[] rows;
    private int selected;
    private float oldTimeScale = 1f;
    private int openedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsOpen = false;
    }

    void Awake()
    {
        Build();
        canvasObj.SetActive(false);
    }

    void OnDestroy()
    {
        if(IsOpen)
        {
            IsOpen = false;
            AudioListener.pause = false;
        }
    }

    void Update()
    {
        if(!IsOpen)
        {
            if(Input.GetKeyDown(pauseKey) && CanOpen()) Open();
            return;
        }
        if(Time.frameCount == openedFrame) return;
        if(Input.GetKeyDown(pauseKey))
        {
            Close();
            return;
        }
        if(Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected = (selected + rows.Length - 1) % rows.Length;
        if(Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected = (selected + 1) % rows.Length;
        int change = 0;
        if(Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) change = -1;
        if(Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) change = 1;
        bool confirm = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);

        Row row = (Row)selected;
        if(change != 0 && VolumeChannel(row, out GameAudio.Channel channel))
        {
            GameAudio.Set(channel, Mathf.Round((GameAudio.Get(channel) + change * volumeStep) * 100f) / 100f);
        }
        if(confirm)
        {
            if(row == Row.Resume) Close();
            else if(row == Row.Restart)
            {
                Close();
                GameOverScreen.ReloadScene();
                return;
            }
            else if(row == Row.Quit) Application.Quit();
        }
        Refresh();
    }

    //Only during normal play: not over a note, a cutscene, the swap screen, an ending or the main menu
    bool CanOpen()
    {
        if(Time.timeScale == 0f || ModalUI.IsOpen || Enemy.PlayerDied) return false;
        if(ModalUI.ClosedFrame == Time.frameCount) return false;
        return true;
    }

    void Open()
    {
        IsOpen = true;
        openedFrame = Time.frameCount;
        oldTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        selected = 0;
        canvasObj.SetActive(true);
        Refresh();
    }

    void Close()
    {
        IsOpen = false;
        Time.timeScale = oldTimeScale > 0f ? oldTimeScale : 1f;
        AudioListener.pause = false;
        canvasObj.SetActive(false);
    }

    static bool VolumeChannel(Row row, out GameAudio.Channel channel)
    {
        switch(row)
        {
            case Row.Master: channel = GameAudio.Channel.Master; return true;
            case Row.Music: channel = GameAudio.Channel.Music; return true;
            case Row.Sfx: channel = GameAudio.Channel.Sfx; return true;
            case Row.Cutscene: channel = GameAudio.Channel.Cutscene; return true;
        }
        channel = GameAudio.Channel.Master;
        return false;
    }

    void Refresh()
    {
        for(int i = 0; i < rows.Length; i++)
        {
            bool on = i == selected;
            string text = Labels[i];
            if(VolumeChannel((Row)i, out GameAudio.Channel channel))
            {
                int steps = Mathf.RoundToInt(GameAudio.Get(channel) * 10f);
                text = $"{Labels[i],-9} <  {new string('|', steps)}{new string('.', 10 - steps)}  >  {steps * 10,3}%";
            }
            rows[i].text = (on ? "> " : "  ") + text;
            rows[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        }
    }

    void Build()
    {
        canvasObj = new GameObject("PauseMenu");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        //Over the HUD and notes, under cutscenes
        canvas.sortingOrder = 150;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        Image dim = new GameObject("Dim").AddComponent<Image>();
        dim.transform.SetParent(canvasObj.transform, false);
        dim.color = new Color(0f, 0f, 0f, 0.8f);
        dim.raycastTarget = false;
        Stretch(dim.rectTransform);

        Font font = GameFont.Get();
        Text title = MakeText(font, "PAUSED", 48, new Vector2(0f, 210f));
        title.alignment = TextAnchor.MiddleCenter;
        rows = new Text[Labels.Length];
        for(int i = 0; i < rows.Length; i++)
        {
            //A gap between the volumes and the buttons
            float y = 110f - i * 50f - (i >= (int)Row.Restart ? 25f : 0f) - (i >= (int)Row.Master ? 15f : 0f);
            rows[i] = MakeText(font, Labels[i], 28, new Vector2(0f, y));
        }
        Text hint = MakeText(font, "[W/S] choose   [A/D] volume   [F] select   [Esc] back", 18, new Vector2(0f, -290f));
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(1f, 1f, 1f, 0.5f);
    }

    Text MakeText(Font font, string text, int size, Vector2 position)
    {
        Text t = new GameObject(text).AddComponent<Text>();
        t.transform.SetParent(canvasObj.transform, false);
        t.font = font;
        t.fontSize = size;
        t.text = text;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = position;
        r.sizeDelta = new Vector2(620f, 44f);
        return t;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
