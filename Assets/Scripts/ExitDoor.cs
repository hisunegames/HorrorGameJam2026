using UnityEngine;

//The way out (Exit room). Locked until the generator runs, then [F] to run for it: the "defied death" ending,
//or the "it never worked" one if all six story papers were read (Lore.cs)
public class ExitDoor : Interactable
{
    //Optional: open door
    public Sprite openSprite;

    void Reset()
    {
        range = 1.2f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Generator.OnFixed += Open;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Generator.OnFixed -= Open;
    }

    void Open()
    {
        if(openSprite != null) GetComponent<SpriteRenderer>().sprite = openSprite;
    }

    public override string Prompt(BodyScript body)
    {
        return Generator.IsFixed ? "Escape" : "Exit (no power)";
    }

    public override bool CanInteract(BodyScript body)
    {
        //In her body you won't let her out
        return Generator.IsFixed && !DaughterTransfer.Active;
    }

    protected override string DefaultThought(BodyScript body)
    {
        return DaughterTransfer.Active ? Lore.ExitAsDaughter : "No power to the door. The generator has to be running first.";
    }

    public override void Interact(BodyScript body)
    {
        //Defying death: running out. Read all six papers and the truth comes out instead
        GameOverScreen end = GameManager.Get<GameOverScreen>();
        if(end == null) return;
        if(NoteObject.ReadAllStoryNotes) end.ShowEnding("IT NEVER WORKED", Lore.EndingNeverWorked);
        else end.ShowEnding("DEFIED DEATH", Lore.EndingDefyDeath);
    }
}
