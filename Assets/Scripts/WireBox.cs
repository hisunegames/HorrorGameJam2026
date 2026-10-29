using UnityEngine;

//Electric box with loose wiring: [F] to pull out the wire (generator part). Doing it while standing in a puddle
//kills you, unless the body has rubber boots (Major), a body is lying in the water, or you're on a chair in it
public class WireBox : Interactable
{
    public ItemData wire;
    //Puddles that count; empty = every puddle in the scene
    public WaterPuddle[] puddles;
    public bool taken;
    //Optional: box after the wire's gone
    public Sprite emptySprite;

    [Tooltip("How far from the box the prompt shows (you still have to be in the puddle to pull it)")]
    public float reachFromWater = 2.5f;

    [Header("Live wire sparks")]
    public Color sparkColor = new Color(1f, 0.95f, 0.55f);
    //Random wait between crackles
    public Vector2 sparkInterval = new Vector2(0.25f, 1.1f);

    private ParticleSystem sparks;
    private float sparkTimer;

    void Start()
    {
        sparks = BuildSparks();
    }

    //Crackles every so often while the wire's still in: warns that the water by it is deadly
    void Update()
    {
        if(taken || sparks == null) return;
        sparkTimer -= Time.deltaTime;
        if(sparkTimer > 0f) return;
        sparkTimer = Random.Range(sparkInterval.x, sparkInterval.y);
        //Somewhere on the box's face
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Vector3 at = transform.position;
        if(sr != null && sr.sprite != null)
        {
            Bounds b = sr.bounds;
            at = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), 0f);
        }
        sparks.transform.position = at;
        sparks.Emit(Random.Range(4, 10));
    }

    ParticleSystem BuildSparks()
    {
        GameObject obj = new GameObject("LiveWireSparks");
        obj.transform.SetParent(transform, false);
        ParticleSystem ps = Projectile.MakeSystem(obj, sparkColor);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        //A few fall like real sparks
        main.gravityModifier = 0.6f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        return ps;
    }

    void Reset()
    {
        range = 1.2f;
        wire = ItemData.Find("Wire");
    }

    //Only reachable from the water, so the shock has to be dealt with (Major's boots, a body or chair in the puddle)
    public override string Prompt(BodyScript body)
    {
        if(taken) return null;
        return OnPuddle(body) ? "Pull out the wire" : "Live wire (reach it from the water)";
    }

    public override bool CanInteract(BodyScript body)
    {
        return !taken && OnPuddle(body) && Inventory != null && !Inventory.IsFull;
    }

    //Long enough to see the prompt from the puddle in front of it
    public override float Range => Mathf.Max(range, reachFromWater);

    protected override string DefaultThought(BodyScript body)
    {
        return "I can't reach it from here. I'd have to stand in the water.";
    }

    public override void Interact(BodyScript body)
    {
        bool boots = body.customBodySO != null && body.customBodySO.rubberBoots;
        if(!boots && InDangerousWater(body))
        {
            Enemy.KillPlayer("You pulled the live wire standing in water.");
            return;
        }
        taken = true;
        if(sparks != null) Destroy(sparks.gameObject, 1f);
        if(emptySprite != null) GetComponent<SpriteRenderer>().sprite = emptySprite;
        Inventory.Give(wire, body.transform.position);
    }

    bool OnPuddle(BodyScript body)
    {
        Vector2 feet = body.transform.position;
        foreach(var puddle in Puddles())
        {
            if(puddle != null && puddle.Contains(feet)) return true;
        }
        return false;
    }

    WaterPuddle[] Puddles()
    {
        return puddles != null && puddles.Length > 0 ? puddles : FindObjectsByType<WaterPuddle>();
    }

    bool InDangerousWater(BodyScript body)
    {
        foreach(var puddle in Puddles())
        {
            if(puddle != null && puddle.IsDangerFor(body)) return true;
        }
        return false;
    }
}
