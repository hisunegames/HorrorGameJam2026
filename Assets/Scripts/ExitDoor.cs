using UnityEngine;

//The way out (Exit room). Locked until the generator runs, then [F] to escape (ending screen, R restarts)
public class ExitDoor : Interactable
{
    public string endTitle = "ESCAPED";
    [TextArea] public string endText = "You made it out. Something behind you is still crying.\n\nPress R to play again";
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
        return Generator.IsFixed;
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "No power to the door. The generator has to be running first.";
    }

    public override void Interact(BodyScript body)
    {
        GameOverScreen end = GameManager.Get<GameOverScreen>();
        if(end != null) end.ShowEnd(endTitle, endText);
    }
}
