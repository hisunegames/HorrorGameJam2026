using UnityEngine;

//A lore note: [F] to read (text, an image, or both). The game pauses while it's open
public class NoteObject : Interactable
{
    public string title = "Note";
    [TextArea(4, 12)] public string body;
    //Drawing / photo instead of (or as well as) text
    public Sprite image;
    public bool hasBeenRead;

    private bool reading;

    void Reset()
    {
        range = 0.8f;
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
        ModalUI.Instance.Open(this, title, this.body, image);
    }

    void Update()
    {
        //The key press that opened it doesn't also close it
        if(!reading || ModalUI.Instance.OpenedFrame == Time.frameCount) return;
        if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))
        {
            reading = false;
            ModalUI.Instance.Close(this);
        }
    }

    //Notes that turn up during play (salvaged from the machine)
    public static NoteObject Spawn(Vector2 position, string title, string body, Sprite image, Sprite worldSprite)
    {
        GameObject obj = new GameObject("Note: " + title);
        obj.transform.position = position;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = worldSprite;
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        sr.sortingOrder = 5;
        NoteObject note = obj.AddComponent<NoteObject>();
        note.range = 0.8f;
        note.title = title;
        note.body = body;
        note.image = image;
        return note;
    }
}
