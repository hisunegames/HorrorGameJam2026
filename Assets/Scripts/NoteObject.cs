using System.Collections.Generic;
using UnityEngine;

//A lore note: [F] to read (text, an image, or both). The game keeps running while it's open: you stand still, the monster doesn't.
//Pick Lore Note (1-6) and the text comes from Lore.cs; or leave it on None and write Title / Body yourself.
//Reading all six story papers changes the ending at the exit (ExitDoor)
public class NoteObject : Interactable
{
    [Tooltip("Which story note this is. Fills Title and Body from Lore.cs (only if Body is empty)")]
    public LoreNote lore = LoreNote.None;
    public string title = "Note";
    [TextArea(4, 12)] public string body;
    //Drawing / photo instead of (or as well as) text
    public Sprite image;
    public bool hasBeenRead;

    //Story papers read this run
    public static readonly HashSet<LoreNote> ReadNotes = new HashSet<LoreNote>();
    public const int StoryNoteCount = 6;
    public static bool ReadAllStoryNotes => ReadNotes.Count >= StoryNoteCount;

    //What each story paper said, to read again from the pause menu
    public struct Entry
    {
        public string title;
        public string body;
        public Sprite image;
    }
    public static readonly Dictionary<LoreNote, Entry> ReadEntries = new Dictionary<LoreNote, Entry>();

    private bool reading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ReadNotes.Clear();
        ReadEntries.Clear();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            ReadNotes.Clear();
            ReadEntries.Clear();
        };
    }

    void Reset()
    {
        range = 0.8f;
    }

    void Awake()
    {
        ApplyLore();
        //On a table or desk: keep it (and its outline) in front
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(sr != null) sr.sortingOrder = Mathf.Max(sr.sortingOrder, AboveFurniture);
    }

    void ApplyLore()
    {
        if(lore == LoreNote.None || !string.IsNullOrEmpty(body)) return;
        title = Lore.NoteTitle(lore);
        body = Lore.NoteBody(lore);
    }

    public override string Prompt(BodyScript body)
    {
        return hasBeenRead ? "Read again" : "Read";
    }

    public override void Interact(BodyScript body)
    {
        if(ModalUI.Instance == null) return;
        reading = true;
        hasBeenRead = true;
        SFXManager.Play("note");
        if(lore != LoreNote.None)
        {
            ReadNotes.Add(lore);
            ReadEntries[lore] = new Entry { title = title, body = this.body, image = image };
        }
        //The game keeps going while you read: you stand still, the monster doesn't
        ModalUI.Instance.Open(this, title, this.body, image, "[F] Close", false);
    }

    void Update()
    {
        //The key press that opened it doesn't also close it
        if(!reading || ModalUI.Instance.OpenedFrame == Time.frameCount) return;
        //Caught while reading: get the note out of the way of the death screen
        if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F) || Enemy.PlayerDied)
        {
            reading = false;
            ModalUI.Instance.Close(this);
        }
    }

    //Notes that turn up during play (salvaged from the machine)
    public static NoteObject Spawn(Vector2 position, LoreNote lore, Sprite image, Sprite worldSprite)
    {
        return Spawn(position, Lore.NoteTitle(lore), Lore.NoteBody(lore), image, worldSprite, lore);
    }

    public static NoteObject Spawn(Vector2 position, string title, string body, Sprite image, Sprite worldSprite, LoreNote lore = LoreNote.None)
    {
        GameObject obj = new GameObject("Note: " + title);
        obj.transform.position = position;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = worldSprite;
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        sr.sortingOrder = 5;
        NoteObject note = obj.AddComponent<NoteObject>();
        note.range = 0.8f;
        note.lore = lore;
        note.title = title;
        note.body = body;
        note.image = image;
        return note;
    }
}
