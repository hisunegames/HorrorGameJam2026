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
    //Adrenaline
    public Color boostedColor = new Color(0.95f, 0.8f, 0.2f);
    public Color crashColor = new Color(0.4f, 0.4f, 0.45f);
    //Hide the bar when stamina is full
    public bool hideWhenFull = true;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Text promptText;
    private CanvasGroup barGroup;
    private RectTransform barFill;
    private Image barFillImage;
    [Header("Play Dead")]
    //Show "[C] Play dead" when the monster is this close (bodies that can play dead only)
    public float playDeadHintRange = 8f;
    public Color vignetteColor = new Color(0f, 0f, 0f, 0.85f);
    public float vignetteFadeSpeed = 3f;
    //How much the vignette breathes while held, 0 = steady
    [Range(0f, 0.5f)] public float vignettePulse = 0.15f;

    private LockedDoor[] lockedDoors;
    private Enemy[] enemies;
    private PlayDead playDead;
    private PlayerInventory inventory;
    private Interactor interactor;
    private BodyDrag bodyDrag;
    private Text inventoryText;
    private RawImage vignette;
    private float vignetteAlpha;
    private StringBuilder sb = new StringBuilder();

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        if(sprint == null) sprint = GameManager.Get<PlayerSprint>();
        playDead = GameManager.Get<PlayDead>();
        inventory = GameManager.Get<PlayerInventory>();
        interactor = GameManager.Get<Interactor>();
        bodyDrag = GameManager.Get<BodyDrag>();
        if(font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
    }

    void Start()
    {
        lockedDoors = FindObjectsByType<LockedDoor>();
        enemies = FindObjectsByType<Enemy>();
    }

    void LateUpdate()
    {
        UpdateVignette();
        UpdatePrompt();
        UpdateStaminaBar();
        UpdateInventory();
    }

    //Bottom left: "Bear Trap  > Knife (2) <  [<-  ->]". Placeholder until the real inventory UI
    void UpdateInventory()
    {
        if(inventory == null || inventory.Items.Count == 0)
        {
            inventoryText.text = "";
            return;
        }
        sb.Clear();
        for(int i = 0; i < inventory.Items.Count; i++)
        {
            if(i > 0) sb.Append("   ");
            ItemStack stack = inventory.Items[i];
            string name = stack != null && stack.item != null ? stack.item.itemName : "?";
            //Weapons show uses left
            if(stack != null && stack.item != null && stack.item.kind == ItemKind.Weapon) name += $" ({stack.charges})";
            sb.Append(i == inventory.Selected ? $"> {name} <" : name);
        }
        if(inventory.Items.Count > 1) sb.Append("   [<-  ->]");
        inventoryText.text = sb.ToString();
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
        //Lying still holding your breath, nothing else to do
        if(body.isPlayingDead) return "";
        foreach(var door in RoomDoor.All)
        {
            if(door.IsUsableBy(body))
            {
                AddLine(door.IsLocked ? "Locked" : $"[{door.interactKey}] Enter");
                break;
            }
        }
        if(playDead != null && body.customBodySO != null && body.customBodySO.canPlayDead && EnemyNear(body))
        {
            AddLine($"[{playDead.playDeadKey}] Play dead");
        }
        //Lockers, notes, pickups, objectives...
        string interact = interactor != null ? interactor.PromptLine(body) : null;
        if(interact != null) AddLine(interact);
        if(body.isHiding) return sb.ToString();
        if(inventory != null)
        {
            ItemData held = inventory.SelectedItem;
            string key = $"[{inventory.useKey}] ";
            if(held != null && held.IsTrap)
            {
                AddLine(key + (inventory.NearestBait(body) != null ? "Booby trap body" : "Set " + held.itemName));
            }
            else if(held != null && held.kind == ItemKind.Adrenaline)
            {
                AddLine(key + "Inject " + held.itemName);
            }
            else if(held != null && held.kind == ItemKind.Weapon)
            {
                string action = inventory.WeaponTarget(body, held, out Component _);
                if(action != null) AddLine(key + action);
            }
        }
        if(bodyDrag != null)
        {
            if(bodyDrag.IsDragging) AddLine($"Dragging (let go of {bodyDrag.dragKey} to drop)");
            else
            {
                Component near = bodyDrag.NearestDraggable(body);
                if(near != null) AddLine($"[Hold {bodyDrag.dragKey}] Drag {DragName(near)}");
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

    static string DragName(Component thing)
    {
        if(thing is BodyScript b) return b.isConsumed ? "corpse" : "body";
        if(thing is DraggableProp p && p.isBody) return "body";
        return thing.name.ToLower().Contains("chair") ? "chair" : thing.name;
    }

    bool EnemyNear(BodyScript body)
    {
        if(enemies == null) return false;
        foreach(var enemy in enemies)
        {
            if(enemy != null && Vector2.Distance(enemy.transform.position, body.transform.position) <= playDeadHintRange) return true;
        }
        return false;
    }

    //Dark edges that close in while playing dead (holding your breath), with a slow breathing pulse
    void UpdateVignette()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        bool holding = body != null && body.isPlayingDead;
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, holding ? 1f : 0f, Time.unscaledDeltaTime * vignetteFadeSpeed);
        float pulse = holding ? 1f - vignettePulse * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f)) : 1f;
        Color c = vignetteColor;
        c.a *= vignetteAlpha * pulse;
        vignette.color = c;
        vignette.enabled = c.a > 0.001f;
    }

    static Texture2D MakeVignetteTexture()
    {
        const int w = 64, h = 36;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[w * h];
        for(int y = 0; y < h; y++)
        {
            for(int x = 0; x < w; x++)
            {
                //0 in the middle, 1 at the corners of an ellipse fitted to the screen
                float dx = (x + 0.5f) / w * 2f - 1f;
                float dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, d));
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
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
        barFillImage.color = sprint.IsBoosted ? boostedColor : sprint.IsCrashing ? crashColor : sprint.IsExhausted ? exhaustedColor : barColor;

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

        //Vignette first so the prompt and stamina bar draw over it
        GameObject vignetteObj = new GameObject("Vignette");
        vignetteObj.transform.SetParent(canvasObj.transform, false);
        vignette = vignetteObj.AddComponent<RawImage>();
        vignette.texture = MakeVignetteTexture();
        vignette.raycastTarget = false;
        vignette.enabled = false;
        RectTransform vRect = vignette.rectTransform;
        vRect.anchorMin = Vector2.zero;
        vRect.anchorMax = Vector2.one;
        vRect.offsetMin = Vector2.zero;
        vRect.offsetMax = Vector2.zero;

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

        //Carried items, bottom left
        GameObject invObj = new GameObject("Inventory");
        invObj.transform.SetParent(canvasObj.transform, false);
        inventoryText = invObj.AddComponent<Text>();
        inventoryText.font = font;
        inventoryText.fontSize = Mathf.RoundToInt(promptSize * 0.8f);
        inventoryText.fontStyle = FontStyle.Bold;
        inventoryText.color = textColor;
        inventoryText.alignment = TextAnchor.LowerLeft;
        inventoryText.horizontalOverflow = HorizontalWrapMode.Overflow;
        inventoryText.raycastTarget = false;
        Outline invOutline = invObj.AddComponent<Outline>();
        invOutline.effectColor = Color.black;
        invOutline.effectDistance = new Vector2(2, -2);
        RectTransform invRect = inventoryText.rectTransform;
        invRect.anchorMin = invRect.anchorMax = invRect.pivot = Vector2.zero;
        invRect.anchoredPosition = new Vector2(60, 40);
        invRect.sizeDelta = new Vector2(900, 40);

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
