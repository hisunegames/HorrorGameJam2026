using System.Collections;
using UnityEngine;
using UnityEngine.UI;

//Full screen black overlay with a title and body line. Builds its own canvas so nothing needs wiring
public class BlackScreenUI : MonoBehaviour
{
    [Header("Look")]
    public Font font;
    //Sizes are at 1280x720; the CRT squashes to 320x180, so keep body text big enough (~12+ game pixels)
    public int titleSize = 96;
    public int bodySize = 48;
    public FontStyle fontStyle = FontStyle.Bold;
    public Color textColor = new Color(0.85f, 0.85f, 0.8f);
    public float fadeTime = 0.4f;
    //Draw through the main camera so the CRT effect also distorts this screen
    public bool renderThroughCamera = true;

    public const int CanvasSortingOrder = 32500;

    private CanvasGroup group;
    private Text titleText;
    private Text bodyText;

    void Awake()
    {
        if(font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        BuildCanvas();
        group.alpha = 0f;
    }

    void BuildCanvas()
    {
        GameObject canvasObj = new GameObject("BlackScreenCanvas");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        if(renderThroughCamera && Camera.main != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        //Topmost sorting layer, above the fog; bodies falling during the swap screen go just above this
        SortingLayer[] layers = SortingLayer.layers;
        if(layers.Length > 0) canvas.sortingLayerID = layers[layers.Length - 1].id;
        canvas.sortingOrder = CanvasSortingOrder;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        group = canvasObj.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject bg = new GameObject("Black");
        bg.transform.SetParent(canvasObj.transform, false);
        Image image = bg.AddComponent<Image>();
        image.color = Color.black;
        Stretch(image.rectTransform, 0f, 1f);

        titleText = MakeText(canvasObj.transform, "Title", titleSize, 0.5f, 0.75f);
        bodyText = MakeText(canvasObj.transform, "Body", bodySize, 0.15f, 0.5f);
    }

    Text MakeText(Transform parent, string name, int size, float minY, float maxY)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = fontStyle;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(text.rectTransform, minY, maxY);
        text.rectTransform.offsetMin = new Vector2(80, 0);
        text.rectTransform.offsetMax = new Vector2(-80, 0);
        return text;
    }

    void Stretch(RectTransform rect, float minY, float maxY)
    {
        rect.anchorMin = new Vector2(0f, minY);
        rect.anchorMax = new Vector2(1f, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void SetText(string title, string body)
    {
        titleText.text = title;
        bodyText.text = body;
    }

    //Unscaled so it still runs while the game is paused
    public IEnumerator FadeIn()
    {
        yield return Fade(1f, fadeTime);
    }

    public IEnumerator FadeOut()
    {
        yield return Fade(0f, fadeTime);
    }

    //Custom length, e.g. a quick dip to black between rooms
    public IEnumerator FadeIn(float duration)
    {
        yield return Fade(1f, duration);
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(0f, duration);
    }

    IEnumerator Fade(float targetAlpha, float duration)
    {
        float start = group.alpha;
        float t = 0f;
        while(t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, targetAlpha, t / duration);
            yield return null;
        }
        group.alpha = targetAlpha;
    }
}
