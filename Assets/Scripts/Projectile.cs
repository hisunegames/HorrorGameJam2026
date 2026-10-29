using UnityEngine;

//A shot from a ranged weapon (stun gun bolt, shotgun pellet). Flies in a straight line with a particle trail,
//stops at walls, and bursts into sparks on whatever it hits: stuns the Amalgam, kills a small monster.
//Projectile.Fire spawns them (several with spread for the shotgun)
public class Projectile : MonoBehaviour
{
    private ItemData weapon;
    private Vector2 direction;
    private float travelled;
    private ParticleSystem trail;
    private bool done;

    private static Material particleMaterial;
    private static Sprite dotSprite;

    public static void Fire(ItemData weapon, Vector2 from, Vector2 facing)
    {
        int count = Mathf.Max(1, weapon.pellets);
        float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        for(int i = 0; i < count; i++)
        {
            //Spread evenly across the cone, with a little jitter
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = baseAngle + Mathf.Lerp(-weapon.spread * 0.5f, weapon.spread * 0.5f, t) + Random.Range(-2f, 2f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject obj = new GameObject(weapon.itemName + " shot");
            obj.transform.position = from;
            Projectile p = obj.AddComponent<Projectile>();
            p.weapon = weapon;
            p.direction = dir;
            p.Build();
        }
        //Muzzle flash right on the barrel tip
        Burst(from, weapon.projectileColor, 6, 1.5f);
    }

    void Build()
    {
        SpriteRenderer head = gameObject.AddComponent<SpriteRenderer>();
        head.sprite = Dot();
        head.color = Color.Lerp(weapon.projectileColor, Color.white, 0.5f);
        SetSorting(head, 20);
        trail = MakeSystem(gameObject, weapon.projectileColor);
        var emission = trail.emission;
        emission.rateOverDistance = 24f;
        var main = trail.main;
        main.startLifetime = 0.25f;
        main.startSize = 0.07f;
        main.startSpeed = 0f;
        trail.Play();
    }

    void Update()
    {
        if(done) return;
        float step = weapon.projectileSpeed * Time.deltaTime;
        Vector2 from = transform.position;
        Vector2 to = from + direction * step;

        //Wall in the way: stop there
        if(HitsWall(from, to, out Vector2 wallPoint))
        {
            End(wallPoint, false);
            return;
        }
        transform.position = to;
        travelled += step;

        foreach(var enemy in FindObjectsByType<Enemy>())
        {
            if(enemy.IsStunned || Vector2.Distance(enemy.transform.position, to) > 0.6f) continue;
            enemy.Stun(weapon.weaponStun);
            End(to, true);
            return;
        }
        foreach(var small in SmallMonster.All)
        {
            if(small == null || small.IsDead || Vector2.Distance(small.transform.position, to) > 0.35f) continue;
            small.Kill();
            End(to, true);
            return;
        }
        if(travelled >= weapon.range) End(to, false);
    }

    static bool HitsWall(Vector2 from, Vector2 to, out Vector2 point)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(~LayerMask.GetMask("Player", "Body", "Ignore Raycast"));
        RaycastHit2D[] hits = new RaycastHit2D[1];
        if(Physics2D.Linecast(from, to, filter, hits) > 0)
        {
            point = hits[0].point;
            return true;
        }
        point = to;
        return false;
    }

    void End(Vector2 at, bool hitSomething)
    {
        done = true;
        transform.position = at;
        Burst(at, weapon.projectileColor, hitSomething ? 14 : 5, hitSomething ? 3f : 1.5f);
        //Let the trail fade out on its own
        GetComponent<SpriteRenderer>().enabled = false;
        trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, 0.4f);
    }

    //A quick puff of sparks
    static void Burst(Vector2 at, Color color, int count, float speed)
    {
        GameObject obj = new GameObject("Sparks");
        obj.transform.position = at;
        ParticleSystem ps = MakeSystem(obj, color);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        ps.Emit(count);
        Destroy(obj, 0.6f);
    }

    //World-space system with flat pixel squares in one colour, fading out (also used by WireBox for its sparks)
    public static ParticleSystem MakeSystem(GameObject obj, Color color)
    {
        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = color;
        main.gravityModifier = 0f;
        main.playOnAwake = false;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = false;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        ParticleSystemRenderer r = obj.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Material();
        SetSorting(r, 21);
        return ps;
    }

    static void SetSorting(Renderer r, int order)
    {
        int layer = SortingLayer.NameToID("Player&Monster");
        if(layer != 0) r.sortingLayerID = layer;
        r.sortingOrder = order;
    }

    //Jam/SpriteSilhouette draws each particle as a flat square in its colour (crisp, and it's already in builds)
    static Material Material()
    {
        if(particleMaterial == null)
        {
            Shader shader = Shader.Find("Jam/SpriteSilhouette");
            particleMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        }
        return particleMaterial;
    }

    static Sprite Dot()
    {
        if(dotSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            dotSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f);
        }
        return dotSprite;
    }
}
