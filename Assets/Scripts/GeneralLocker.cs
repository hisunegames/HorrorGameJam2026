using UnityEngine;

//The locker the general is hiding in. [F] opens it: Note 4 falls out and he talks (dialogue box, text in Lore.cs).
//Unarmed: the argument is loud enough to bring the daughter over. With a weapon selected: he begs, and you
//[Q] kill him or [F] let him go (he runs off). Use instead of Locker on that one locker
public class GeneralLocker : Interactable
{
    //How loud the argument is (Noise.Make loudness; 1 = sprinting). Big enough to carry across a room or two
    public float argumentNoise = 3f;
    //Note 4 on the floor (Notes sprite)
    public Sprite noteWorldSprite;
    public Vector2 noteDropOffset = new Vector2(0f, -0.8f);

    private enum State { Closed, Choosing, Done }
    private State state = State.Closed;
    private BodyScript opener;

    void Reset()
    {
        range = 1f;
#if UNITY_EDITOR
        noteWorldSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PropNFurniture/Notes.png");
#endif
    }

    public override string Prompt(BodyScript body)
    {
        if(state == State.Closed) return "Open locker";
        if(state == State.Choosing) return $"Let him go   [{KillKey}] Kill him";
        return null;
    }

    static KeyCode KillKey
    {
        get
        {
            PlayerInventory inv = GameManager.Get<PlayerInventory>();
            return inv != null ? inv.useKey : KeyCode.Q;
        }
    }

    static bool HoldingWeapon()
    {
        PlayerInventory inv = GameManager.Get<PlayerInventory>();
        ItemData held = inv != null ? inv.SelectedItem : null;
        return held != null && held.kind == ItemKind.Weapon;
    }

    public override void Interact(BodyScript body)
    {
        if(state == State.Choosing)
        {
            LetGo();
            return;
        }
        opener = body;
        NoteObject.Spawn((Vector2)transform.position + noteDropOffset, LoreNote.Note4_GeneralLocker, null, noteWorldSprite);
        bool armed = HoldingWeapon();
        Talk(armed ? Lore.GeneralTalkArmed : Lore.GeneralTalk);
        if(armed)
        {
            state = State.Choosing;
        }
        else
        {
            //Shouting match: the daughter hears it
            Noise.Make(transform.position, argumentNoise);
            state = State.Done;
        }
    }

    void Update()
    {
        if(state != State.Choosing || Time.timeScale == 0f) return;
        if(opener == null || !opener.isCurrentBody || DistanceFrom(opener) > Range + 1f) return;
        if(Input.GetKeyDown(KillKey) && HoldingWeapon()) Kill();
    }

    void Kill()
    {
        state = State.Done;
        SpeechBubble.Say(opener.transform, Lore.GeneralKilled, 3.5f);
        BodyDrag drag = GameManager.Get<BodyDrag>();
        BloodTrail.Add(transform.position, drag != null ? drag.bloodSprite : null);
        Debug.Log("The general is dead", this);
    }

    void LetGo()
    {
        state = State.Done;
        SpeechBubble.Say(transform, Lore.GeneralSpared, 2.5f);
        Debug.Log("The general ran off", this);
    }

    static void Talk(string[] lines)
    {
        DialogueController dialogue = GameManager.Get<DialogueController>();
        if(dialogue != null) dialogue.StartDialogue(lines);
    }
}
