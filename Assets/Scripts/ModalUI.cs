using UnityEngine;
using UnityEngine.UI;

//Shared full-screen panel for notes, the flask puzzle and the nuke QTE (pauses the game while open),
//plus the red alarm flash and countdown. The script that opened it reads the keys itself (check Owner).
//Put it on the GameManager; builds its own canvas
public class ModalUI : MonoBehaviour
{
    public static ModalUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.Owner != null;

    public Font font;
    public Color panelColor = new Color(0.05f, 0.05f, 0.06f, 0.92f);
    public Color textColor = new Color(0.9f, 0.9f, 0.85f);
    public Color alarmColor = new Color(0.8f, 0f, 0f, 0.35f);

    public Object Owner { get; private set; }
    //Frame it opened / closed: the key press that did it shouldn't count again that frame
    public int OpenedFrame { get; private set; } = -1;
    public static int ClosedFrame { get; private set; } = -1;

    private GameObject panel;
    private Text titleText;
    private Text bodyText;
    private Text footerText;
    private Image picture;
    private Image alarm;
    private Text countdownText;
    private bool alarmOn;
    private float pausedTimeScale = 1f;
    private bool pausedByMe;

    void Awake()
    {
        Instance = this;
        if(font == null || font.name == "LegacyRuntime") font = GameFont.Get();
        Build();
    }

    void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

    void Update()
    {
        if(alarmOn)
        {
            Color c = alarmColor;
            c.a *= 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            alarm.color = c;
        }
    }

    //pause: false keeps the game running behind it (notes: the monster doesn't wait while you read)
    public void Open(Object owner, string title, string body, Sprite image = null, string footer = "[F] Close", bool pause = true)
    {
        if(Owner == null && pause)
        {
            pausedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByMe = true;
        }
        Owner = owner;
        OpenedFrame = Time.frameCount;
        panel.SetActive(true);
        SetText(title, body, footer);
        picture.sprite = image;
        picture.enabled = image != null;
        if(image != null) picture.preserveAspect = true;
        //Text goes under the picture when there is one
        bodyText.rectTransform.anchorMax = new Vector2(0.93f, image != null ? 0.43f : 0.84f);
        SetChoices(null);
    }

    public void SetText(string title, string body, string footer)
    {
        titleText.text = title;
        bodyText.text = body;
        footerText.text = footer;
    }

    public void Close(Object owner)
    {
        if(Owner != owner) return;
        Owner = null;
        ClosedFrame = Time.frameCount;
        panel.SetActive(false);
        if(pausedByMe) Time.timeScale = pausedTimeScale > 0f ? pausedTimeScale : 1f;
        pausedByMe = false;
    }

    //Row of pictures to pick from (flasks). Cursor gets a white frame, picked ones a yellow one. Null hides the row
    public void SetChoices(Sprite[] sprites, int cursor = -1, bool[] picked = null)
    {
        for(int i = 0; i < choiceSlots.Length; i++)
        {
            bool show = sprites != null && i < sprites.Length;
            choiceSlots[i].gameObject.SetActive(show);
            if(!show) continue;
            choiceImages[i].sprite = sprites[i];
            bool isPicked = picked != null && i < picked.Length && picked[i];
            choiceSlots[i].color = i == cursor ? new Color(1f, 1f, 1f, 0.9f) : isPicked ? new Color(0.95f, 0.8f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.08f);
            //Picked ones keep the yellow showing round the cursor frame
            choiceImages[i].color = isPicked ? new Color(1f, 0.95f, 0.6f) : Color.white;
        }
        //Text goes under the row when it's showing
        if(sprites != null) bodyText.rectTransform.anchorMax = new Vector2(0.93f, 0.4f);
    }

    public void SetAlarm(bool on, string countdown = "")
    {
        alarmOn = on;
        alarm.enabled = on;
        countdownText.text = on ? countdown : "";
    }

    void Build()
    {
        GameObject canvasObj = new GameObject("ModalCanvas");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        alarm = MakeImage(canvasObj.transform, "Alarm", Vector2.zero, Vector2.one);
        alarm.enabled = false;
        countdownText = MakeText(canvasObj.transform, "Countdown", 96, new Vector2(0f, 0.75f), new Vector2(1f, 0.95f), TextAnchor.MiddleCenter);
        countdownText.color = new Color(1f, 0.2f, 0.2f);

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = panelColor;
        RectTransform pr = bg.rectTransform;
        pr.anchorMin = new Vector2(0.15f, 0.1f);
        pr.anchorMax = new Vector2(0.85f, 0.9f);
        pr.offsetMin = pr.offsetMax = Vector2.zero;

        titleText = MakeText(panel.transform, "Title", 40, new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleCenter);
        picture = MakeImage(panel.transform, "Picture", new Vector2(0.25f, 0.45f), new Vector2(0.75f, 0.84f));
        picture.color = Color.white;
        bodyText = MakeText(panel.transform, "Body", 26, new Vector2(0.07f, 0.12f), new Vector2(0.93f, 0.84f), TextAnchor.UpperLeft);
        //Long notes shrink to fit the panel instead of running off the bottom
        bodyText.resizeTextForBestFit = true;
        bodyText.resizeTextMinSize = 12;
        bodyText.resizeTextMaxSize = 26;
        bodyText.verticalOverflow = VerticalWrapMode.Truncate;
        footerText = MakeText(panel.transform, "Footer", 22, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.1f), TextAnchor.MiddleCenter);
        footerText.color = new Color(0.6f, 0.6f, 0.55f);

        //Choice row (up to 6), hidden until SetChoices
        choiceSlots = new Image[6];
        choiceImages = new Image[6];
        for(int i = 0; i < choiceSlots.Length; i++)
        {
            float x0 = 0.08f + i * 0.145f;
            choiceSlots[i] = MakeImage(panel.transform, "Choice" + i, new Vector2(x0, 0.45f), new Vector2(x0 + 0.125f, 0.8f));
            choiceImages[i] = MakeImage(choiceSlots[i].transform, "Picture", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
            choiceImages[i].preserveAspect = true;
            choiceSlots[i].gameObject.SetActive(false);
        }
        panel.SetActive(false);
    }

    private Image[] choiceSlots;
    private Image[] choiceImages;

    Text MakeText(Transform parent, string name, int size, Vector2 min, Vector2 max, TextAnchor anchor)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = textColor;
        t.alignment = anchor;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return t;
    }

    Image MakeImage(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return img;
    }
}
