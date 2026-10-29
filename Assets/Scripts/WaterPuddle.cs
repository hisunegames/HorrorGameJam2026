using UnityEngine;

//Water on the floor near live wiring (WireBox). Its area is the BoxCollider2D (set to trigger), or the sprite bounds.
//Safe to stand in when a body lies in it, or you stand on a chair that's in it
public class WaterPuddle : MonoBehaviour
{
    private Collider2D area;
    private SpriteRenderer sr;

    void Awake()
    {
        area = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if(area != null) area.isTrigger = true;
    }

    public bool Contains(Vector2 point)
    {
        if(area != null) return area.OverlapPoint(point);
        return sr != null && sr.bounds.Contains(new Vector3(point.x, point.y, sr.bounds.center.z));
    }

    //Standing here would get you shocked
    public bool IsDangerFor(BodyScript body)
    {
        Vector2 feet = body.transform.position;
        if(!Contains(feet)) return false;
        //Something that won't conduct is lying in the water
        foreach(var other in FindObjectsByType<BodyScript>())
        {
            if(other != body && !other.isCurrentBody && Contains(other.transform.position)) return false;
        }
        //A corpse prop lying in it, or standing on a chair that's in it
        foreach(var prop in DraggableProp.All)
        {
            if(!Contains(prop.transform.position)) continue;
            if(prop.isBody || prop.IsStandingOn(feet)) return false;
        }
        return true;
    }
}
