using UnityEngine;

//A trap that's been set (PlayerInventory). Goes off when the monster comes close: stuns it and switches to
//the snapped sprite. One use, can't be picked up again. Set next to a left-behind body it rides along when dragged
public class ArmedTrap : MonoBehaviour
{
    public ItemData item;
    public bool snapped = false;

    private SpriteRenderer sr;
    private Enemy[] enemies = new Enemy[0];
    private float refreshTimer;

    public static ArmedTrap Place(ItemData item, Vector2 position, Transform attachTo)
    {
        GameObject obj = new GameObject(item.itemName + " (set)");
        obj.transform.position = position;
        if(attachTo != null) obj.transform.SetParent(attachTo, true);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = item.worldSprite;
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        ArmedTrap trap = obj.AddComponent<ArmedTrap>();
        trap.item = item;
        trap.sr = sr;
        return trap;
    }

    void Awake()
    {
        if(sr == null) sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if(snapped || item == null) return;
        refreshTimer -= Time.deltaTime;
        if(refreshTimer <= 0f)
        {
            refreshTimer = 1f;
            enemies = FindObjectsByType<Enemy>();
        }
        foreach(var enemy in enemies)
        {
            if(enemy == null || enemy.IsStunned) continue;
            if(Vector2.Distance(enemy.transform.position, transform.position) > item.triggerRadius) continue;
            Snap();
            enemy.Stun(item.stunDuration);
            return;
        }
        //Small monsters die in it
        foreach(var small in SmallMonster.All)
        {
            if(small == null || small.IsDead) continue;
            if(Vector2.Distance(small.transform.position, transform.position) > item.triggerRadius) continue;
            Snap();
            small.Kill();
            return;
        }
    }

    void Snap()
    {
        snapped = true;
        if(sr != null && item.snappedSprite != null) sr.sprite = item.snappedSprite;
        //Stays where it went off, even if the body it was on gets dragged later
        transform.SetParent(null, true);
        if(item.snapNoise > 0f) Noise.Make(transform.position, item.snapNoise);
        Debug.Log($"{item.itemName} snapped", this);
    }
}
