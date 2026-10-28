using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

//Interact prompt above the current body ("[F] Enter", "[E] Swap"...) and the sprint stamina bar.
//Builds its own canvas so nothing needs wiring
public class PlayerHUD : MonoBehaviour
{
    public BodyManager bodyManager;
    public PlayerSprint sprint;

    [Header("Look")]
    public Font font;
    public int promptSize = 32;
    public Color textColor = new Color(0.9f, 0.9f, 0.85f);
    //World units above the body's feet
    public float promptHeight = 1.1f;
    //Off keeps the HUD crisp on top of the CRT effect
    public bool renderThroughCamera = false;

    [Header("Stamina Bar")]
    public Vector2 barSize = new Vector2(240, 14);
    public Color barColor = new Color(0.85f, 0.85f, 0.8f);
    public Color exhaustedColor = new Color(0.7f, 0.1f, 0.1f);
    //Hide the bar when stamina is full
    public bool hideWhenFull = true;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Text promptText;
    private CanvasGroup barGroup;
    private RectTransform barFill;
    private Image barFillImage;
    private LockedDoor[] lockedDoors;
    private StringBuilder sb = new StringBuilder();

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        if(sprint == null) sprint = GameManager.Get<PlayerSprint>();
        if(font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
    }

    void Start()
    {
        lockedDoors = FindObjectsByType<LockedDoor>();
    }

    void LateUpdate()
    {
        UpdatePrompt();
        UpdateStaminaBar();
    }

    void UpdatePrompt()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        string prompt = body != null && Time.timeScale > 0f ? BuildPrompt(body) : "";
        promptText.text = prompt;
        if(prompt.Length == 0) return;

        Camera cam = Camera.main;
        if(cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(body.transform.position + Vector3.up * promptHeight);
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local))
        {
            promptText.rectTransform.anchoredPosition = local;
        }
    }

    string BuildPrompt(BodyScript body)
    {
        sb.Clear();
        foreach(var door in RoomDoor.All)
        {
            if(door.IsUsableBy(body))
            {
                AddLine($"[{door.interactKey}] Enter");
                break;
            }
        }

        BodyScript other = bodyManager.FindNearestBody();
        if(other != null)
        {
            AddLine("[E] Swap");
            DialogueData dialogue = other.GetComponent<DialogueData>();
            if(dialogue != null && dialogue.dialogueLines != null && dialogue.dialogueLines.Length > 0)
            {
                AddLine("[F] Talk");
            }
        }

        if(lockedDoors != null)
        {
            foreach(var locked in lockedDoors)
            {
                if(locked == null || !locked.isDoorClosed || !locked.gameObject.activeInHierarchy) continue;
                if(Vector2.Distance(locked.transform.position, body.transform.position) > locked.interactrange) continue;
                AddLine(CanUnlock() ? "[F] Unlock" : "Locked");
                break;
            }
        }
        return sb.ToString();
    }

    void AddLine(string line)
    {
        if(sb.Length > 0) sb.Append('\n');
        sb.Append(line);
    }

    //Same check LockedDoor does
    bool CanUnlock()
    {
        foreach(var ability in bodyManager.activeAbilities)
        {
            if(ability != null && ability.canUnlockDoors) return true;
        }
        return false;
    }

    void UpdateStaminaBar()
    {
        if(sprint == null)
        {
            barGroup.alpha = 0f;
            return;
        }
        float fill = sprint.Stamina01;
        barFill.anchorMax = new Vector2(fill, 1f);
        barFillImage.color = sprint.IsExhausted ? exhaustedColor : barColor;

        float targetAlpha = hideWhenFull && fill >= 0.999f ? 0f : 1f;
        barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 3f);
    }

    void BuildCanvas()
    {
        GameObject canvasObj = new GameObject("PlayerHUDCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.AddComponent<Canvas>();
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
        //Under BlackScreenUI (100) so fades cover it
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasRect = (RectTransform)canvasObj.transform;

        //Prompt, moved above the player every frame
        GameObject promptObj = new GameObject("Prompt");
        promptObj.transform.SetParent(canvasObj.transform, false);
        promptText = promptObj.AddComponent<Text>();
        promptText.font = font;
        promptText.fontSize = promptSize;
        promptText.fontStyle = FontStyle.Bold;
        promptText.color = textColor;
        promptText.alignment = TextAnchor.LowerCenter;
        promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;
        promptText.raycastTarget = false;
        Outline outline = promptObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        RectTransform promptRect = promptText.rectTransform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.sizeDelta = new Vector2(400, 40);

        //Stamina bar, top left
        GameObject barObj = new GameObject("StaminaBar");
        barObj.transform.SetParent(canvasObj.transform, false);
        barGroup = barObj.AddComponent<CanvasGroup>();
        barGroup.blocksRaycasts = false;
        Image back = barObj.AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.6f);
        back.raycastTarget = false;
        RectTransform barRect = back.rectTransform;
        barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(60, -50);
        barRect.sizeDelta = barSize;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barObj.transform, false);
        barFillImage = fillObj.AddComponent<Image>();
        barFillImage.raycastTarget = false;
        barFill = barFillImage.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = Vector2.one;
        barFill.offsetMin = new Vector2(2, 2);
        barFill.offsetMax = new Vector2(-2, -2);
    }
}
