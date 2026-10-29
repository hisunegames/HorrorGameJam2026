using UnityEngine;

//[F] to hide inside, [F] again to come out. Hidden: invisible, can't move, the monster doesn't see you and can't
//grab you. Doesn't work if the monster is chasing you and can see you get in
public class Locker : Interactable
{
    public static Locker Occupied { get; private set; }

    private BodyScript hidden;

    void Reset()
    {
        range = 1f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if(hidden != null) Leave();
    }

    public override string Prompt(BodyScript body)
    {
        if(hidden == body) return "Get out";
        if(Occupied != null) return null;
        return SeenByMonster(body) ? "Hide (it's watching you)" : "Hide";
    }

    public override bool CanInteract(BodyScript body)
    {
        return hidden == body || (Occupied == null && !SeenByMonster(body));
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "Not now. It's looking right at me.";
    }

    public override void Interact(BodyScript body)
    {
        if(hidden == body) Leave();
        else Enter(body);
    }

    static bool SeenByMonster(BodyScript body)
    {
        foreach(var enemy in FindObjectsByType<Enemy>())
        {
            if(enemy.IsChasing(body.transform)) return true;
        }
        return false;
    }

    void Enter(BodyScript body)
    {
        hidden = body;
        Occupied = this;
        body.isHiding = true;
        //Stays where they stood in front of it (moving them into the locker could put them inside the wall)
        body.rb.linearVelocity = Vector2.zero;
        SetVisible(body, false);
    }

    void Leave()
    {
        BodyScript body = hidden;
        hidden = null;
        if(Occupied == this) Occupied = null;
        if(body == null) return;
        body.isHiding = false;
        //Back out right where they went in
        SetVisible(body, true);
    }

    static void SetVisible(BodyScript body, bool visible)
    {
        foreach(var sr in body.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = visible;
        foreach(var col in body.GetComponents<Collider2D>()) col.enabled = visible;
    }
}
