using UnityEngine;

//The table with the flasks lined up. With an empty battery: [F] to start, [Left]/[Right] to move between the flasks
//on the table, [F] to pick / put back. Two picked: the right pair charges the battery, a wrong one fizzles.
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
    //Flasks.png slice order: _0 Red, _1 Cyan, _2 Green, _3 Orange
    public string[] colorNames = { "Red", "Cyan", "Green", "Orange" };
    [Tooltip("The two colours that work (indexes into Color Names), any order")]
    public int[] correctPair = { 1, 3 };
    //Picked flasks lift a pixel or two
    public float pickedLift = 0.125f;
    public Color pickedTint = new Color(1f, 0.95f, 0.6f);
    [TextArea] public string wrongLine = "The mixture fizzles and goes black.";
    [TextArea] public string rightLine = "The cell hums. It's charged.";

    public bool used;

    private bool mixing;
    private int cursor;
    private bool[] picked = new bool[0];
    private Vector3[] restPositions;
    private SpriteRenderer marker;
    private BodyScript user;

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
        BuildMarker();
    }

    //Flask sprites near the table, left to right
    void FindFlasks()
    {
        var found = new System.Collections.Generic.List<SpriteRenderer>();
        foreach(var sr in FindObjectsByType<SpriteRenderer>())
        {
            if(sr.sprite == null || !sr.sprite.name.StartsWith("Flasks")) continue;
            if(Vector2.Distance(sr.transform.position, transform.position) <= flaskSearchRadius) found.Add(sr);
        }
        found.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        flasks = found.ToArray();
        if(flasks.Length < 2) Debug.LogWarning("Chemical station: fewer than 2 flasks found nearby, assign Flasks by hand", this);
    }

    public override string Prompt(BodyScript body)
    {
        if(used || flasks.Length < 2) return null;
        if(mixing) return $"Pick {ColorOf(cursor)} flask";
        return Inventory != null && Inventory.Has(emptyBattery) ? "Mix chemicals" : "Chemical station (needs an empty battery)";
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

    void Check()
    {
        int a = -1, b = -1;
        for(int i = 0; i < picked.Length; i++)
        {
            if(!picked[i]) continue;
            if(a < 0) a = i;
            else b = i;
        }
        int ca = ColorIndex(a), cb = ColorIndex(b);
        bool right = correctPair.Length == 2 &&
                     ((ca == correctPair[0] && cb == correctPair[1]) || (ca == correctPair[1] && cb == correctPair[0]));
        if(!right)
        {
            SpeechBubble.Say(transform, wrongLine, 2.5f);
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

    //Which colour a flask is: from its sprite name (Flasks_2 = Green), so the order on the table doesn't matter
    int ColorIndex(int flask)
    {
        string n = flasks[flask].sprite != null ? flasks[flask].sprite.name : "";
        int us = n.LastIndexOf('_');
        if(us >= 0 && int.TryParse(n.Substring(us + 1), out int idx)) return idx;
        return flask;
    }

    string ColorOf(int flask)
    {
        int c = ColorIndex(flask);
        return c >= 0 && c < colorNames.Length ? colorNames[c] : "the";
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
}
