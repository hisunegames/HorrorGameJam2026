using System.Collections.Generic;
using UnityEngine;

//Something you press the interact key at (locker, note, generator...). The Interactor on the GameManager picks
//the nearest one in range, shows its prompt above the player and calls Interact
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    //How close the player has to be to the edge of this object's sprite (or to Interact Point if set)
    public float range = 1f;
    public Transform interactPoint;
    [Tooltip("What the player mutters when they press the key here without what it needs. Empty = a default line")]
    [TextArea] public string blockedThought;

    public Vector2 Point => interactPoint != null ? (Vector2)interactPoint.position : (Vector2)transform.position;

    private SpriteRenderer sprite;

    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    //From the middle of the player's body to the nearest edge of the sprite, so things on tables or walls
    //work from where you'd naturally stand. With an Interact Point: from the player's feet to that point
    public float DistanceFrom(BodyScript body)
    {
        if(interactPoint != null) return Vector2.Distance(body.transform.position, interactPoint.position);
        Vector2 from = BodyCenter(body);
        if(sprite == null) sprite = GetComponent<SpriteRenderer>();
        if(sprite == null || sprite.sprite == null) return Vector2.Distance(from, transform.position);
        Bounds b = sprite.bounds;
        Vector3 nearest = b.ClosestPoint(new Vector3(from.x, from.y, b.center.z));
        return Vector2.Distance(from, nearest);
    }

    static Vector2 BodyCenter(BodyScript body)
    {
        Collider2D col = body.GetComponent<Collider2D>();
        return col != null && col.enabled ? (Vector2)col.bounds.center : (Vector2)body.transform.position;
    }

    //Text after "[F] " (or a full line like "Locked" when it can't be used). Null = nothing to do here right now
    public abstract string Prompt(BodyScript body);
    public abstract void Interact(BodyScript body);
    //False: pressing the key makes the player say BlockedThought instead
    public virtual bool CanInteract(BodyScript body) { return true; }

    public string BlockedThought(BodyScript body)
    {
        return string.IsNullOrEmpty(blockedThought) ? DefaultThought(body) : blockedThought;
    }

    //Each kind of thing has its own line
    protected virtual string DefaultThought(BodyScript body) { return "I can't use this yet."; }

    protected static PlayerInventory Inventory => GameManager.Get<PlayerInventory>();

    //Select it to see where the player has to be: the middle of their body inside the green box (or circle)
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f);
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(interactPoint == null && sr != null && sr.sprite != null)
        {
            Bounds b = sr.bounds;
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x + range * 2f, b.size.y + range * 2f, 0f));
            return;
        }
        Gizmos.DrawWireSphere(Point, range);
        Gizmos.DrawWireSphere(Point, 0.08f);
        if(interactPoint != null) Gizmos.DrawLine(transform.position, Point);
    }
}
