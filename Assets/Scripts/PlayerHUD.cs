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
    //Off: prompts sit in the bottom-right corner (the player's own lines float above their head instead)
    public bool promptAbovePlayer = false;
    //World units above the body's feet (when above the player)
    public float promptHeight = 1.1f;
    //From the bottom-right corner (when not above the player)
    public Vector2 promptCornerOffset = new Vector2(-40f, 40f);
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
    private NoiseIndicator noise;
    private RectTransform noiseFill;
    private Image noiseFillImage;
    private CanvasGroup noiseGroup;

    [Header("Noise Meter")]
    public bool showNoiseMeter = true;
    public Color quietColor = new Color(0.5f, 0.8f, 0.5f);
    public Color loudColor = new Color(0.9f, 0.2f, 0.15f);
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
    [Header("Item Wheel")]
    //From the bottom centre of the screen
    public Vector2 wheelPosition = new Vector2(0f, 80f);
    public float wheelSpacing = 130f;
    [Range(0.3f, 1f)] public float sideScale = 0.65f;
    [Range(0f, 1f)] public float sideAlpha = 0.45f;
    public float wheelSlideTime = 0.15f;

    private class WheelSlot
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image icon;
        public Text label;
    }

    private WheelSlot[] wheelSlots;
    private CanvasGroup wheelGroup;
    private Text wheelLeftHint;
    private Text wheelRightHint;
    private int lastSelected = -1;
    private int lastCount;
    private float slide;
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
        if(font == null || font.name == "LegacyRuntime") font = GameFont.Get();
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
        UpdateNoiseMeter();
        UpdateInventory();
        UpdateNoteCounter();
    }

    //Bottom left: "Bear Trap  > Knife (2) <  [<-  ->]". Placeholder until the real inventory UI
    //Item wheel, bottom centre: previous / current / next with sprite and name. Slides when you switch with the arrows
    void UpdateInventory()
    {
        //Slots = empty hand + every item. Hidden while you carry nothing
        int count = inventory != null ? inventory.SlotCount : 0;
        bool show = inventory != null && inventory.Items.Count > 0 && Time.timeScale > 0f && !DaughterTransfer.Active;
        wheelGroup.alpha = show ? 1f : 0f;
        if(!show)
        {
            lastSelected = -1;
            return;
        }
        int sel = Mathf.Clamp(inventory.Selected, 0, count - 1);
        //Moved one step: start the slide from where the new item was
        if(lastSelected >= 0 && count == lastCount && sel != lastSelected)
        {
            slide = (sel - lastSelected + count) % count == 1 ? 1f : -1f;
        }
        lastSelected = sel;
        lastCount = count;
        slide = Mathf.MoveTowards(slide, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, wheelSlideTime));

        for(int k = -1; k <= 1; k++)
        {
            WheelSlot slot = wheelSlots[k + 1];
            //Two items: just current + next. One: just current
            bool visible = k == 0 || (k == 1 && count > 1) || (k == -1 && count > 2);
            slot.root.gameObject.SetActive(visible);
            if(!visible) continue;
            ItemStack stack = inventory.SlotAt((sel + k + count) % count);
            ItemData item = stack != null ? stack.item : null;
            slot.icon.sprite = item != null ? item.worldSprite : null;
            slot.icon.enabled = slot.icon.sprite != null;
            string name = stack == null ? "Empty hand" : item != null ? item.itemName : "?";
            if(item != null && item.kind == ItemKind.Weapon) name += $" ({stack.charges})";
            slot.label.text = name;

            float x = (k + slide) * wheelSpacing;
            //The middle one is big and bright, the sides small and faded
            float centred = 1f - Mathf.Clamp01(Mathf.Abs(k + slide));
            float scale = Mathf.Lerp(sideScale, 1f, centred);
            slot.root.anchoredPosition = new Vector2(x, 0f);
            slot.root.localScale = new Vector3(scale, scale, 1f);
            slot.group.alpha = Mathf.Lerp(sideAlpha, 1f, centred);
        }
        wheelLeftHint.enabled = count > 2;
        wheelRightHint.enabled = count > 1;
    }

    void UpdatePrompt()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        string prompt = body != null && Time.timeScale > 0f ? BuildPrompt(body) : "";
        promptText.text = prompt;
        //Fixed in the corner: nothing to follow
        if(prompt.Length == 0 || !promptAbovePlayer) return;

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
        //Only the door that has the key this frame (a nearer locker / box wins otherwise)
        RoomDoor door = interactor != null ? interactor.DoorFocus : RoomDoor.NearestAt(body, out _);
        if(door != null)
        {
            AddLine(door.linkedDoor == null ? $"[{door.interactKey}] Door" : door.IsLocked ? "Locked" : $"[{door.interactKey}] Enter");
        }
        if(playDead != null && body.customBodySO != null && body.customBodySO.canPlayDead && EnemyNear(body))
        {
            float wait = playDead.CooldownLeft;
            AddLine(wait > 0f ? $"Play dead ({Mathf.CeilToInt(wait)}s)" : $"[{playDead.playDeadKey}] Play dead");
        }
        //Lockers, notes, pickups, objectives...
        string interact = interactor != null ? interactor.PromptLine(body) : null;
        if(interact != null) AddLine(interact);
        if(body.isHiding) return sb.ToString();
        if(inventory != null && !DaughterTransfer.Active)
        {
            ItemData held = inventory.SelectedItem;
            string key = $"[{inventory.useKey}] ";
            if(held != null && held.IsTrap)
            {
                AddLine(key + (inventory.NearestBait(body) != null ? "Booby trap body" : "Set " + held.itemName));
            }
            else if(held != null && held.kind == ItemKind.Lure)
            {
                AddLine(key + "Set " + held.itemName + " (draws the monster here)");
            }
            else if(held != null && held.kind == ItemKind.Throwable)
            {
                AddLine(key + "Throw " + held.itemName);
            }
            else if(held != null && held.kind == ItemKind.Adrenaline)
            {
                AddLine(key + "Inject " + held.itemName);
            }
            else if(held != null && held.kind == ItemKind.Weapon && held.ranged)
            {
                AddLine(key + "Fire " + held.itemName);
            }
            else if(held != null && held.kind == ItemKind.Weapon)
            {
                string action = inventory.WeaponTarget(body, held, out Component _);
                if(action != null) AddLine(key + action);
            }
        }
        if(bodyDrag != null)
        {
            if(bodyDrag.IsDragging) AddLine($"[{bodyDrag.dragKey}] Drop");
            else
            {
                Component near = bodyDrag.NearestDraggable(body);
                if(near != null) AddLine($"[{bodyDrag.dragKey}] Drag {DragName(near)}");
            }
        }

        //End-game choice: take the daughter's body
        DaughterTransfer transfer = GameManager.Get<DaughterTransfer>();
        if(transfer != null && transfer.Reachable(body) != null && bodyManager.FindNearestBody() == null)
        {
            AddLine($"[{transfer.transferKey}] Take her body");
        }

        BodyScript other = DaughterTransfer.Active ? null : bodyManager.FindNearestBody();
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

    public static Texture2D MakeVignetteTexture()
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
        return bodyManager.CurrentCanUnlockDoors;
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

    //Under the stamina bar: jumps with every noise the monster could hear, drains back to silent
    void UpdateNoiseMeter()
    {
        if(noiseGroup == null) return;
        if(noise == null) noise = GameManager.Get<NoiseIndicator>();
        bool show = showNoiseMeter && noise != null && Time.timeScale > 0f;
        noiseGroup.alpha = show ? 1f : 0f;
        if(!show) return;
        float level = noise.Level01;
        noiseFill.anchorMax = new Vector2(level, 1f);
        noiseFillImage.color = Color.Lerp(quietColor, loudColor, level);
    }

    void BuildWheel(Transform canvas)
    {
        GameObject wheelObj = new GameObject("ItemWheel");
        wheelObj.transform.SetParent(canvas, false);
        wheelGroup = wheelObj.AddComponent<CanvasGroup>();
        wheelGroup.blocksRaycasts = false;
        RectTransform wheel = wheelObj.AddComponent<RectTransform>();
        wheel.anchorMin = wheel.anchorMax = new Vector2(0.5f, 0f);
        wheel.pivot = new Vector2(0.5f, 0.5f);
        wheel.anchoredPosition = wheelPosition;
        wheel.sizeDelta = Vector2.zero;

        wheelSlots = new WheelSlot[3];
        //Sides first so the middle draws on top while sliding
        foreach(int i in new[] { 0, 2, 1 }) wheelSlots[i] = BuildSlot(wheel);
        wheelLeftHint = WheelText(wheel, "<-", new Vector2(-wheelSpacing * 1.55f, 0f));
        wheelRightHint = WheelText(wheel, "->", new Vector2(wheelSpacing * 1.55f, 0f));
    }

    WheelSlot BuildSlot(Transform parent)
    {
        var slot = new WheelSlot();
        GameObject obj = new GameObject("Slot");
        obj.transform.SetParent(parent, false);
        slot.group = obj.AddComponent<CanvasGroup>();
        slot.group.blocksRaycasts = false;
        Image back = obj.AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.55f);
        back.raycastTarget = false;
        slot.root = back.rectTransform;
        slot.root.sizeDelta = new Vector2(84f, 84f);

        slot.icon = new GameObject("Icon").AddComponent<Image>();
        slot.icon.transform.SetParent(obj.transform, false);
        slot.icon.preserveAspect = true;
        slot.icon.raycastTarget = false;
        RectTransform ir = slot.icon.rectTransform;
        ir.anchorMin = Vector2.zero;
        ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(12f, 12f);
        ir.offsetMax = new Vector2(-12f, -12f);

        slot.label = new GameObject("Name").AddComponent<Text>();
        slot.label.transform.SetParent(obj.transform, false);
        slot.label.font = font;
        slot.label.fontSize = Mathf.RoundToInt(promptSize * 0.6f);
        slot.label.fontStyle = FontStyle.Bold;
        slot.label.color = textColor;
        slot.label.alignment = TextAnchor.UpperCenter;
        slot.label.horizontalOverflow = HorizontalWrapMode.Overflow;
        slot.label.raycastTarget = false;
        Outline outline = slot.label.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        RectTransform lr = slot.label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.anchoredPosition = new Vector2(0f, -4f);
        lr.sizeDelta = new Vector2(200f, 30f);
        return slot;
    }

    Text WheelText(Transform parent, string text, Vector2 position)
    {
        Text t = new GameObject(text).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.fontSize = Mathf.RoundToInt(promptSize * 0.7f);
        t.fontStyle = FontStyle.Bold;
        t.color = textColor;
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        Outline outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        t.rectTransform.anchoredPosition = position;
        t.rectTransform.sizeDelta = new Vector2(60f, 40f);
        return t;
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
        promptText.alignment = promptAbovePlayer ? TextAnchor.LowerCenter : TextAnchor.LowerRight;
        promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;
        promptText.raycastTarget = false;
        Outline outline = promptObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        RectTransform promptRect = promptText.rectTransform;
        if(promptAbovePlayer)
        {
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0f);
        }
        else
        {
            //Bottom-right, lines stacking upwards
            promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(1f, 0f);
            promptRect.anchoredPosition = promptCornerOffset;
        }
        promptRect.sizeDelta = new Vector2(600, 40);

        BuildWheel(canvasObj.transform);

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

        //Noise meter under it, always there: "NOISE" and a bar that fills green -> red
        GameObject noiseObj = new GameObject("NoiseMeter");
        noiseObj.transform.SetParent(canvasObj.transform, false);
        noiseGroup = noiseObj.AddComponent<CanvasGroup>();
        noiseGroup.blocksRaycasts = false;
        Image noiseBack = noiseObj.AddComponent<Image>();
        noiseBack.color = new Color(0f, 0f, 0f, 0.6f);
        noiseBack.raycastTarget = false;
        RectTransform noiseRect = noiseBack.rectTransform;
        noiseRect.anchorMin = noiseRect.anchorMax = noiseRect.pivot = new Vector2(0f, 1f);
        noiseRect.anchoredPosition = new Vector2(60 + 90, -50 - barSize.y - 12);
        noiseRect.sizeDelta = new Vector2(barSize.x - 90, barSize.y);

        GameObject noiseFillObj = new GameObject("Fill");
        noiseFillObj.transform.SetParent(noiseObj.transform, false);
        noiseFillImage = noiseFillObj.AddComponent<Image>();
        noiseFillImage.raycastTarget = false;
        noiseFill = noiseFillImage.rectTransform;
        noiseFill.anchorMin = Vector2.zero;
        noiseFill.anchorMax = new Vector2(0f, 1f);
        noiseFill.offsetMin = new Vector2(2, 2);
        noiseFill.offsetMax = new Vector2(-2, -2);

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(noiseObj.transform, false);
        Text label = labelObj.AddComponent<Text>();
        label.font = font;
        label.fontSize = Mathf.RoundToInt(promptSize * 0.6f);
        label.fontStyle = FontStyle.Bold;
        label.color = textColor;
        label.text = "NOISE";
        label.alignment = TextAnchor.MiddleRight;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.raycastTarget = false;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(1f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-8f, 0f);
        labelRect.sizeDelta = new Vector2(90f, 10f);

        //Top right: story papers read, "NOTES 2/6"
        noteCounter = new GameObject("NoteCounter").AddComponent<Text>();
        noteCounter.transform.SetParent(canvasObj.transform, false);
        noteCounter.font = font;
        noteCounter.fontSize = Mathf.RoundToInt(promptSize * 0.8f);
        noteCounter.fontStyle = FontStyle.Bold;
        noteCounter.color = textColor;
        noteCounter.alignment = TextAnchor.UpperRight;
        noteCounter.horizontalOverflow = HorizontalWrapMode.Overflow;
        noteCounter.raycastTarget = false;
        Outline noteOutline = noteCounter.gameObject.AddComponent<Outline>();
        noteOutline.effectColor = Color.black;
        noteOutline.effectDistance = new Vector2(2, -2);
        RectTransform noteRect = noteCounter.rectTransform;
        noteRect.anchorMin = noteRect.anchorMax = noteRect.pivot = new Vector2(1f, 1f);
        noteRect.anchoredPosition = new Vector2(-40f, -40f);
        noteRect.sizeDelta = new Vector2(240f, 40f);
    }

    private Text noteCounter;

    void UpdateNoteCounter()
    {
        if(noteCounter == null) return;
        noteCounter.enabled = Time.timeScale > 0f;
        int read = NoteObject.ReadNotes.Count;
        noteCounter.text = $"NOTES {read}/{NoteObject.StoryNoteCount}";
        //Gold once they're all read (the ending changes)
        noteCounter.color = read >= NoteObject.StoryNoteCount ? new Color(1f, 0.85f, 0.3f) : textColor;
    }
}
