using System.Collections.Generic;
using UnityEngine;

//Finds the nearest Interactable to the current body and uses it on the interact key. Put it on the GameManager.
//Everything usable in range gets a yellow outline (the one the key will use, solid). Pressing the key at something
//you can't use yet makes the player mutter why (dialogue box), e.g. "I'd need tools for this"
public class Interactor : MonoBehaviour
{
    public BodyManager bodyManager;
    public KeyCode interactKey = KeyCode.F;

    [Header("Outline")]
    public Color focusColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color inRangeColor = new Color(1f, 0.85f, 0.2f, 0.45f);
    //Jam/SpriteSilhouette (filled in automatically in the editor)
    public Shader outlineShader;

    public Interactable Current { get; private set; }
    //The door the key goes to this frame (null when something nearer has it, or no door's in reach)
    public RoomDoor DoorFocus { get; private set; }

    private Material outlineMaterial;
    private readonly Dictionary<Interactable, SpriteRenderer[]> outlines = new Dictionary<Interactable, SpriteRenderer[]>();
    private readonly HashSet<Interactable> lit = new HashSet<Interactable>();
    private static readonly Vector2[] Offsets = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    void OnValidate()
    {
        if(outlineShader == null) outlineShader = Shader.Find("Jam/SpriteSilhouette");
    }

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        if(outlineShader == null) outlineShader = Shader.Find("Jam/SpriteSilhouette");
        if(outlineShader != null) outlineMaterial = new Material(outlineShader);
    }

    void Update()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        Current = null;
        DoorFocus = null;
        lit.Clear();
        if(body != null && Time.timeScale > 0f && !ModalUI.IsOpen)
        {
            //Hiding: only the locker you're in
            if(body.isHiding)
            {
                Current = Locker.Occupied;
            }
            else if(!body.isPlayingDead)
            {
                float best = float.MaxValue;
                foreach(var it in Interactable.All)
                {
                    if(it == null || !it.isActiveAndEnabled) continue;
                    //In her body: nothing to pick up, read or inspect, only the warhead (doors still work, RoomDoor)
                    if(DaughterTransfer.Active && !(it is NukeTerminal)) continue;
                    float d = it.DistanceFrom(body);
                    if(d > it.Range || it.Prompt(body) == null) continue;
                    lit.Add(it);
                    if(d >= best) continue;
                    best = d;
                    Current = it;
                }
                //Standing in a doorway: whichever is nearer gets the key, the door (RoomDoor handles it) or this
                RoomDoor door = RoomDoor.NearestAt(body, out float doorDist);
                if(door != null && (Current == null || doorDist <= best))
                {
                    Current = null;
                    DoorFocus = door;
                }
            }
            //Not on the same press that just closed a note / panel
            if(Current != null && Input.GetKeyDown(interactKey) && ModalUI.ClosedFrame != Time.frameCount)
            {
                if(Current.CanInteract(body)) Current.Interact(body);
                else Mutter(Current.BlockedThought(body));
            }
        }
        UpdateOutlines();
    }

    //"[F] Hide", "[F] Generator needs: wire"
    public string PromptLine(BodyScript body)
    {
        if(Current == null) return null;
        string text = Current.Prompt(body);
        if(string.IsNullOrEmpty(text)) return null;
        return $"[{interactKey}] {text}";
    }

    //The player talking to themselves: a short line floating over their head (the dialogue box was too much)
    void Mutter(string thought)
    {
        if(string.IsNullOrEmpty(thought) || bodyManager == null || bodyManager.currentBody == null) return;
        SpeechBubble.Say(bodyManager.currentBody.transform, thought, 3f);
    }

    void UpdateOutlines()
    {
        //Hide outlines of things no longer in range (and forget destroyed ones)
        var gone = new List<Interactable>();
        foreach(var pair in outlines)
        {
            if(pair.Key == null)
            {
                gone.Add(pair.Key);
                continue;
            }
            if(lit.Contains(pair.Key)) continue;
            foreach(var sr in pair.Value) if(sr != null) sr.enabled = false;
        }
        foreach(var key in gone) outlines.Remove(key);
        if(outlineMaterial == null) return;

        foreach(var it in lit)
        {
            SpriteRenderer target = TargetSprite(it);
            if(target == null || target.sprite == null) continue;
            if(!outlines.TryGetValue(it, out SpriteRenderer[] parts) || parts[0] == null)
            {
                parts = BuildOutline(target);
                outlines[it] = parts;
            }
            Color color = it == Current ? focusColor : inRangeColor;
            //One sprite pixel, whatever the object's scale
            float pixel = 1f / target.sprite.pixelsPerUnit;
            Vector3 scale = target.transform.lossyScale;
            for(int i = 0; i < parts.Length; i++)
            {
                SpriteRenderer sr = parts[i];
                sr.enabled = target.enabled;
                sr.sprite = target.sprite;
                sr.flipX = target.flipX;
                sr.flipY = target.flipY;
                sr.color = color;
                sr.sortingLayerID = target.sortingLayerID;
                sr.sortingOrder = target.sortingOrder - 1;
                Vector2 o = Offsets[i] * pixel;
                sr.transform.localPosition = new Vector3(o.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), o.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 0f);
            }
        }
    }

    static SpriteRenderer TargetSprite(Interactable it)
    {
        SpriteRenderer sr = it.GetComponent<SpriteRenderer>();
        return sr != null ? sr : it.GetComponentInChildren<SpriteRenderer>();
    }

    //Four flat-colour copies of the sprite, one pixel up/down/left/right, behind it
    SpriteRenderer[] BuildOutline(SpriteRenderer target)
    {
        var parts = new SpriteRenderer[Offsets.Length];
        for(int i = 0; i < parts.Length; i++)
        {
            GameObject obj = new GameObject("Outline");
            obj.transform.SetParent(target.transform, false);
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = outlineMaterial;
            parts[i] = sr;
        }
        return parts;
    }
}
