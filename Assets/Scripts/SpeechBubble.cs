using Febucci.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//A line of text floating above a character for a few seconds (Researcher F's "stay away!").
//SpeechBubble.Say(transform, "...") - adds itself to the speaker, builds one shared canvas
public class SpeechBubble : MonoBehaviour
{
    public float height = 1.2f;
    public Color color = new Color(0.95f, 0.9f, 0.8f);

    private static Canvas canvas;
    private TextMeshProUGUI text;
    private TypewriterByCharacter typewriter;
    private float timer;

    //height: world units above the speaker (default 1.2, about a head above a person). Lower for things on walls
    public static void Say(Transform speaker, string line, float seconds = 3f, float height = -1f)
    {
        if(speaker == null || string.IsNullOrEmpty(line)) return;
        SpeechBubble bubble = speaker.GetComponent<SpeechBubble>();
        if(bubble == null) bubble = speaker.gameObject.AddComponent<SpeechBubble>();
        if(height >= 0f) bubble.height = height;
        bubble.Show(line, seconds);
    }

    void Show(string line, float seconds)
    {
        if(text == null) Build();
        text.enabled = true;
        //Types in (Text Animator); a long line gets the time it takes to type on top
        typewriter.ShowText(line);
        timer = seconds + line.Length * typewriter.waitForNormalChars;
    }

    void LateUpdate()
    {
        if(text == null || !text.enabled) return;
        timer -= Time.deltaTime;
        Camera cam = Camera.main;
        if(timer <= 0f || cam == null)
        {
            text.enabled = false;
            return;
        }
        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * height);
        RectTransform canvasRect = (RectTransform)canvas.transform;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            text.rectTransform.anchoredPosition = local;
        }
    }

    void OnDisable()
    {
        if(text != null) text.enabled = false;
    }

    void OnDestroy()
    {
        if(text != null) Destroy(text.gameObject);
    }

    void Build()
    {
        if(canvas == null)
        {
            GameObject canvasObj = new GameObject("SpeechCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            //Under the HUD prompt (50)
            canvas.sortingOrder = 45;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
        }
        GameObject obj = new GameObject(name + " speech");
        obj.transform.SetParent(canvas.transform, false);
        text = obj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = GameFont.GetTMP();
        if(font != null) text.font = font;
        text.fontSize = 26;
        text.fontStyle = FontStyles.Italic;
        text.color = color;
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        //Black edge so it reads over any floor
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        typewriter = GameFont.Animate(text);
        RectTransform r = text.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(500, 40);
    }
}
