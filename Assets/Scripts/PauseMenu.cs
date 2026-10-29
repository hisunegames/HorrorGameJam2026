using System;
using UnityEngine;
using UnityEngine.UI;

//[Esc] during play: pauses the game. Up/Down (or W/S) picks a row, Left/Right (A/D) changes a volume,
//[F]/[Enter] on Resume / Notes / Restart / Quit. Notes lists the six story papers; read ones open again.
//Volumes are GameAudio's (saved). Keyboard only, like the rest of the game
public class PauseMenu : MonoBehaviour
{
    public KeyCode pauseKey = KeyCode.Escape;
    //Each Left/Right press
    public float volumeStep = 0.1f;

    public static bool IsOpen { get; private set; }

    private enum Row { Resume, Notes, Master, Music, Sfx, Cutscene, Restart, Quit }
    static readonly string[] Labels = { "Resume", "Notes", "Master", "Music", "SFX", "Cutscene", "Restart", "Quit" };
    private enum Page { Main, NoteList, Reading }

    private GameObject canvasObj;
    private GameObject mainPage;
    private GameObject notePage;
    private Text[] rows;
    private Text[] noteRows;
    private Page page;
    private int selected;
    private int noteSelected;
    private float oldTimeScale = 1f;
    private int changedFrame = -1;

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
        //The press that opened / switched a page doesn't also act on the new one
        if(Time.frameCount == changedFrame) return;
        if(page == Page.Main) UpdateMain();
        else if(page == Page.NoteList) UpdateNoteList();
        else UpdateReading();
    }

    void UpdateMain()
    {
        if(Input.GetKeyDown(pauseKey))
        {
            Close();
            return;
        }
        selected = Step(selected, rows.Length);
        int change = Sideways();
        Row row = (Row)selected;
        if(change != 0 && VolumeChannel(row, out GameAudio.Channel channel))
        {
            GameAudio.Set(channel, Mathf.Round((GameAudio.Get(channel) + change * volumeStep) * 100f) / 100f);
        }
        if(Confirm())
        {
            if(row == Row.Resume) Close();
            else if(row == Row.Notes) ShowPage(Page.NoteList);
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

    void UpdateNoteList()
    {
        if(Input.GetKeyDown(pauseKey))
        {
            ShowPage(Page.Main);
            return;
        }
        noteSelected = Step(noteSelected, noteRows.Length);
        if(Confirm())
        {
            //Last row is Back
            if(noteSelected == noteRows.Length - 1)
            {
                ShowPage(Page.Main);
                return;
            }
            LoreNote note = NoteAt(noteSelected);
            if(NoteObject.ReadEntries.TryGetValue(note, out NoteObject.Entry entry) && ModalUI.Instance != null)
            {
                //Shown in the note panel (already paused, so it doesn't pause again)
                ModalUI.Instance.Open(this, entry.title, entry.body, entry.image, "[F] Back", false);
                ShowPage(Page.Reading);
                return;
            }
        }
        Refresh();
    }

    void UpdateReading()
    {
        if(Input.GetKeyDown(pauseKey) || Confirm())
        {
            if(ModalUI.Instance != null) ModalUI.Instance.Close(this);
            ShowPage(Page.NoteList);
        }
    }

    static LoreNote NoteAt(int index)
    {
        return (LoreNote)(index + 1);
    }

    static int Step(int current, int count)
    {
        if(Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return (current + count - 1) % count;
        if(Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return (current + 1) % count;
        return current;
    }

    static int Sideways()
    {
        if(Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return -1;
        if(Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return 1;
        return 0;
    }

    static bool Confirm()
    {
        return Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
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
        oldTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        selected = 0;
        canvasObj.SetActive(true);
        ShowPage(Page.Main);
    }

    void Close()
    {
        IsOpen = false;
        Time.timeScale = oldTimeScale > 0f ? oldTimeScale : 1f;
        AudioListener.pause = false;
        canvasObj.SetActive(false);
    }

    void ShowPage(Page next)
    {
        page = next;
        changedFrame = Time.frameCount;
        if(next == Page.NoteList) noteSelected = 0;
        //The note panel draws under this canvas, so the menu steps aside while you read
        canvasObj.SetActive(next != Page.Reading);
        mainPage.SetActive(next == Page.Main);
        notePage.SetActive(next == Page.NoteList);
        Refresh();
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
            string text = Labels[i];
            if(VolumeChannel((Row)i, out GameAudio.Channel channel))
            {
                int steps = Mathf.RoundToInt(GameAudio.Get(channel) * 10f);
                text = $"{Labels[i],-9} <  {new string('|', steps)}{new string('.', 10 - steps)}  >  {steps * 10,3}%";
            }
            else if((Row)i == Row.Notes)
            {
                text = $"Notes ({NoteObject.ReadNotes.Count}/{NoteObject.StoryNoteCount})";
            }
            Highlight(rows[i], text, i == selected, true);
        }
        for(int i = 0; i < noteRows.Length; i++)
        {
            bool back = i == noteRows.Length - 1;
            bool read = !back && NoteObject.ReadEntries.TryGetValue(NoteAt(i), out NoteObject.Entry entry);
            string text = back ? "Back" : read ? $"{i + 1}. {NoteObject.ReadEntries[NoteAt(i)].title}" : $"{i + 1}. ???";
            Highlight(noteRows[i], text, i == noteSelected, back || read);
        }
    }

    static void Highlight(Text t, string text, bool on, bool usable)
    {
        t.text = (on ? "> " : "  ") + text;
        float alpha = on ? 1f : 0.5f;
        if(!usable) alpha *= 0.6f;
        t.color = new Color(1f, 1f, 1f, alpha);
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
        mainPage = Page_(canvasObj.transform, "Main");
        Text title = MakeText(mainPage.transform, font, "PAUSED", 48, new Vector2(0f, 250f));
        title.alignment = TextAnchor.MiddleCenter;
        rows = new Text[Labels.Length];
        for(int i = 0; i < rows.Length; i++)
        {
            //Gaps between Resume/Notes, the volumes and the buttons
            float y = 170f - i * 48f - (i >= (int)Row.Master ? 18f : 0f) - (i >= (int)Row.Restart ? 18f : 0f);
            rows[i] = MakeText(mainPage.transform, font, Labels[i], 28, new Vector2(0f, y));
        }
        Text hint = MakeText(mainPage.transform, font, "[W/S] choose   [A/D] volume   [F] select   [Esc] back", 18, new Vector2(0f, -300f));
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(1f, 1f, 1f, 0.5f);

        notePage = Page_(canvasObj.transform, "Notes");
        Text notesTitle = MakeText(notePage.transform, font, "NOTES", 48, new Vector2(0f, 250f));
        notesTitle.alignment = TextAnchor.MiddleCenter;
        noteRows = new Text[NoteObject.StoryNoteCount + 1];
        for(int i = 0; i < noteRows.Length; i++)
        {
            float y = 150f - i * 50f - (i == noteRows.Length - 1 ? 20f : 0f);
            noteRows[i] = MakeText(notePage.transform, font, "", 28, new Vector2(0f, y));
        }
        Text noteHint = MakeText(notePage.transform, font, "[W/S] choose   [F] read   [Esc] back", 18, new Vector2(0f, -300f));
        noteHint.alignment = TextAnchor.MiddleCenter;
        noteHint.color = new Color(1f, 1f, 1f, 0.5f);
    }

    static GameObject Page_(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        Stretch((RectTransform)obj.transform);
        return obj;
    }

    static Text MakeText(Transform parent, Font font, string text, int size, Vector2 position)
    {
        Text t = new GameObject(text).AddComponent<Text>();
        t.transform.SetParent(parent, false);
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
