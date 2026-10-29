using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//The table with the flasks lined up. Each flask has a chemical label, shuffled every run: the right two are copper sulfate
//and zinc sulfate (a copper/zinc cell, Daniell cell), the others are decoys (always H2SO4, battery acid, as the trap). With an empty battery: [F] to start,
//[Left]/[Right] to move between the flasks, [F] to pick / put back. Two picked: the right pair charges the battery,
//a wrong one fizzles (nothing lost). Labels are embossed, so the blind body can read them too.
//Hints for the pair go in notes. Walk away or [Esc] to stop
public class ChemicalChargingStation : Interactable
{
    //The one being used right now (PlayerInventory leaves the arrow keys alone while it's set)
    public static ChemicalChargingStation Active { get; private set; }

    public ItemData emptyBattery;
    public ItemData chargedBattery;
    [Tooltip("The flask sprites on the table. Empty = found automatically (Flasks.png sprites within Flask Search Radius)")]
    public SpriteRenderer[] flasks;
    public float flaskSearchRadius = 2f;
    [Tooltip("The two labels that make the battery work, any order")]
    public string[] correctLabels = { "CuSO4", "ZnSO4" };
    [Tooltip("Decoys that are on the table every run. H2SO4 is battery acid: the obvious guess, and wrong on its own")]
    public string[] alwaysDecoys = { "H2SO4" };
    [Tooltip("Labels for the other flasks, picked at random each run")]
    public string[] decoyLabels = { "NaCl", "H2O", "C2H5OH", "H2O2", "KNO3", "C6H12O6" };
    [TextArea] public string acidLine = "It smokes and eats at the casing. Acid alone won't charge it - it needs the two metals.";
    //Labels show under the flasks when you're this much past the station's range
    public float labelShowDistance = 1f;
    public int labelFontSize = 16;
    //Picked flasks lift a pixel or two
    public float pickedLift = 0.125f;
    public Color pickedTint = new Color(1f, 0.95f, 0.6f);
    [TextArea] public string wrongLine = "The mixture fizzles and goes black.";
    [TextArea] public string rightLine = "The cell hums. It's charged.";

    public bool used;

    private string[] labels = new string[0];
    private bool mixing;
    private int cursor;
    private bool[] picked = new bool[0];
    private Vector3[] restPositions;
    private SpriteRenderer marker;
    private BodyScript user;
    private Canvas labelCanvas;
    private Text[] labelTexts = new Text[0];

    void Reset()
    {
        range = 1.5f;
        emptyBattery = ItemData.Find("EmptyBattery");
        chargedBattery = ItemData.Find("ChargedBattery");
    }

    void Start()
    {
        if(flasks == null || flasks.Length == 0) FindFlasks();
        restPositions = new Vector3[flasks.Length];
        for(int i = 0; i < flasks.Length; i++) restPositions[i] = flasks[i].transform.localPosition;
        picked = new bool[flasks.Length];
        ShuffleLabels();
        BuildMarker();
        BuildLabels();
    }

    //Flask sprites near the table, left to right
    void FindFlasks()
    {
        var found = new List<SpriteRenderer>();
        foreach(var sr in FindObjectsByType<SpriteRenderer>())
        {
            if(sr.sprite == null || !sr.sprite.name.StartsWith("Flasks")) continue;
            if(Vector2.Distance(sr.transform.position, transform.position) <= flaskSearchRadius) found.Add(sr);
        }
        found.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        flasks = found.ToArray();
        if(flasks.Length < 2) Debug.LogWarning("Chemical station: fewer than 2 flasks found nearby, assign Flasks by hand", this);
    }

    //The right pair plus random decoys, dealt onto the flasks in a random order
    void ShuffleLabels()
    {
        var pool = new List<string>(correctLabels);
        foreach(var d in alwaysDecoys) if(pool.Count < flasks.Length && !string.IsNullOrEmpty(d)) pool.Add(d);
        var decoys = new List<string>(decoyLabels);
        decoys.RemoveAll(d => pool.Contains(d));
        while(pool.Count < flasks.Length && decoys.Count > 0)
        {
            int d = Random.Range(0, decoys.Count);
            pool.Add(decoys[d]);
            decoys.RemoveAt(d);
        }
        while(pool.Count < flasks.Length) pool.Add("?");
        for(int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        labels = pool.GetRange(0, flasks.Length).ToArray();
    }

    public override string Prompt(BodyScript body)
    {
        if(used || flasks.Length < 2) return null;
        //Controls spelled out while mixing (the arrow keys and Esc aren't obvious)
        if(mixing)
        {
            string action = picked[cursor] ? "Put back" : "Pick";
            return $"{action} {labels[cursor]} ({PickedCount()}/2)\n[<- ->] Other flask   [Esc] Stop";
        }
        return Inventory != null && Inventory.Has(emptyBattery) ? "Mix chemicals (pick 2 flasks)" : "Chemical station (needs an empty battery)";
    }

    public override bool CanInteract(BodyScript body)
    {
        return !used && (mixing || (Inventory != null && Inventory.Has(emptyBattery)));
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "A charging rig. I need an empty battery to put in it.";
    }

    public override void Interact(BodyScript body)
    {
        if(!mixing)
        {
            mixing = true;
            user = body;
            Active = this;
            cursor = 0;
            ClearPicks();
            SpeechBubble.Say(body.transform, Lore.StartMixing, 2.5f);
            return;
        }
        picked[cursor] = !picked[cursor];
        ApplyLook();
        if(PickedCount() == 2) Check();
    }

    void Update()
    {
        if(!mixing) return;
        //Walked off, swapped or gave up
        if(user == null || !user.isCurrentBody || Input.GetKeyDown(KeyCode.Escape) || DistanceFrom(user) > range + 0.5f)
        {
            StopMixing();
            return;
        }
        int step = (Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0);
        if(step != 0) cursor = (cursor + step + flasks.Length) % flasks.Length;
        //Little arrow above the flask you're on
        marker.enabled = true;
        marker.transform.position = flasks[cursor].bounds.center + Vector3.up * (flasks[cursor].bounds.extents.y + 0.3f);
    }

    //Labels under the flasks while the player is near the table
    void LateUpdate()
    {
        BodyManager bm = GameManager.Get<BodyManager>();
        BodyScript body = bm != null ? bm.currentBody : null;
        Camera cam = Camera.main;
        bool show = body != null && cam != null && Time.timeScale > 0f && DistanceFrom(body) <= range + labelShowDistance;
        for(int i = 0; i < labelTexts.Length; i++)
        {
            labelTexts[i].enabled = show;
            if(!show) continue;
            Bounds b = flasks[i].bounds;
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(b.center.x, b.min.y - 0.05f, b.center.z));
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)labelCanvas.transform, screen, null, out Vector2 local))
            {
                labelTexts[i].rectTransform.anchoredPosition = local;
            }
            labelTexts[i].color = picked.Length > i && picked[i] ? pickedTint : new Color(0.9f, 0.9f, 0.85f);
        }
    }

    void Check()
    {
        var chosen = new List<string>();
        for(int i = 0; i < picked.Length; i++) if(picked[i]) chosen.Add(labels[i]);
        bool right = chosen.Count == 2 && correctLabels.Length == 2 &&
                     chosen.Contains(correctLabels[0]) && chosen.Contains(correctLabels[1]);
        if(!right)
        {
            //Picking the battery acid gets its own hint
            bool acid = false;
            foreach(var d in alwaysDecoys) acid |= chosen.Contains(d);
            SpeechBubble.Say(transform, acid ? acidLine : wrongLine, acid ? 3.5f : 2.5f);
            ClearPicks();
            return;
        }
        used = true;
        StopMixing();
        SpeechBubble.Say(transform, rightLine, 3f);
        PlayerInventory inv = Inventory;
        inv.Remove(emptyBattery);
        inv.Give(chargedBattery, transform.position);
    }

    void StopMixing()
    {
        mixing = false;
        user = null;
        if(Active == this) Active = null;
        if(marker != null) marker.enabled = false;
        ClearPicks();
        SFXManager.Instance.PlaySFX("chem");
    }

    void ClearPicks()
    {
        for(int i = 0; i < picked.Length; i++) picked[i] = false;
        ApplyLook();
    }

    void ApplyLook()
    {
        for(int i = 0; i < flasks.Length; i++)
        {
            flasks[i].transform.localPosition = restPositions[i] + (picked[i] ? Vector3.up * pickedLift : Vector3.zero);
            flasks[i].color = picked[i] ? pickedTint : Color.white;
        }
    }

    int PickedCount()
    {
        int n = 0;
        foreach(var p in picked) if(p) n++;
        return n;
    }

    //Small down arrow
    void BuildMarker()
    {
        const int w = 5, h = 3;
        Texture2D tex = new Texture2D(w, h);
        tex.filterMode = FilterMode.Point;
        for(int y = 0; y < h; y++)
        {
            for(int x = 0; x < w; x++) tex.SetPixel(x, y, Mathf.Abs(x - 2) <= y ? Color.white : Color.clear);
        }
        tex.Apply();
        GameObject obj = new GameObject("FlaskCursor");
        marker = obj.AddComponent<SpriteRenderer>();
        marker.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        SortingLayer[] layers = SortingLayer.layers;
        if(layers.Length > 0) marker.sortingLayerID = layers[layers.Length - 1].id;
        marker.enabled = false;
    }

    void BuildLabels()
    {
        GameObject canvasObj = new GameObject("FlaskLabels");
        labelCanvas = canvasObj.AddComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        //Under the speech lines (45) and HUD (50)
        labelCanvas.sortingOrder = 44;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        Font font = GameFont.Get();

        labelTexts = new Text[flasks.Length];
        for(int i = 0; i < flasks.Length; i++)
        {
            GameObject obj = new GameObject("Label " + labels[i]);
            obj.transform.SetParent(canvasObj.transform, false);
            Text t = obj.AddComponent<Text>();
            t.font = font;
            t.fontSize = labelFontSize;
            t.fontStyle = FontStyle.Bold;
            t.text = labels[i];
            t.alignment = TextAnchor.UpperCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            Outline outline = obj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(120, 24);
            t.enabled = false;
            labelTexts[i] = t;
        }
    }
}
