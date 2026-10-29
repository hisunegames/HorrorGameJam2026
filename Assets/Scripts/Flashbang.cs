using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

//A thrown flashbang ([Q], Throwable item). Flies a few tiles the way you face (stops at walls), then goes off after
//its fuse: white flash, a loud bang, the Amalgam is stunned and small monsters are killed within Blast Radius
public class Flashbang : MonoBehaviour
{
    public ItemData item;

    //How long it's in the air
    const float FlightTime = 0.35f;

    private Vector3 from;
    private Vector3 to;
    private float age;
    private SpriteRenderer sr;

    public static Flashbang Throw(ItemData item, Vector2 position, Vector2 direction)
    {
        if(direction.sqrMagnitude < 0.01f) direction = Vector2.down;
        direction.Normalize();
        Vector3 start = position;
        Vector3 end = start + (Vector3)(direction * item.throwDistance);
        //Lands against the wall instead of going through it
        if(NavMesh.SamplePosition(start, out NavMeshHit onFloor, 1f, NavMesh.AllAreas) &&
           NavMesh.Raycast(onFloor.position, onFloor.position + (Vector3)(direction * item.throwDistance), out NavMeshHit edge, NavMesh.AllAreas))
        {
            end = start + (Vector3)(direction * Vector2.Distance(onFloor.position, edge.position));
        }

        GameObject obj = new GameObject(item.itemName + " (thrown)");
        obj.transform.position = start;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = item.worldSprite;
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        sr.sortingOrder = 5;
        Flashbang bang = obj.AddComponent<Flashbang>();
        bang.item = item;
        bang.sr = sr;
        bang.from = start;
        bang.to = end;
        return bang;
    }

    void Update()
    {
        age += Time.deltaTime;
        if(age < FlightTime)
        {
            //Little arc on the way
            float t = age / FlightTime;
            transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.4f);
            return;
        }
        transform.position = to;
        //Fizzing: blinks faster as it's about to go
        float left = FlightTime + item.fuseTime - age;
        if(sr != null) sr.color = Mathf.Repeat(age * (left < 0.4f ? 16f : 6f), 1f) < 0.5f ? Color.white : new Color(1f, 0.6f, 0.6f);
        if(left <= 0f) GoOff();
    }

    void GoOff()
    {
        Vector2 at = transform.position;
        foreach(Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if(Vector2.Distance(enemy.transform.position, at) <= item.blastRadius) enemy.Stun(item.stunDuration);
        }
        foreach(SmallMonster small in FindObjectsByType<SmallMonster>(FindObjectsSortMode.None))
        {
            if(!small.IsDead && Vector2.Distance(small.transform.position, at) <= item.blastRadius) small.Kill();
        }
        if(item.blastNoise > 0f) Noise.Make(at, item.blastNoise);

        //Burst of white sparks
        GameObject burst = new GameObject("FlashBurst");
        burst.transform.position = at;
        ParticleSystem ps = Projectile.MakeSystem(burst, Color.white);
        var main = ps.main;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
        main.startSize = 0.1f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        ps.Emit(40);
        Destroy(burst, 0.6f);

        //White-out, stronger the closer the player is
        BodyManager bodies = GameManager.Get<BodyManager>();
        BodyScript player = bodies != null ? bodies.currentBody : null;
        float strength = 0.6f;
        if(player != null)
        {
            float d = Vector2.Distance(player.transform.position, at);
            strength = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(d / (item.blastRadius * 3f)));
        }
        ScreenFlash.Play(strength, item.flashTime);
        Destroy(gameObject);
    }
}

//Full-screen white flash that fades out (under the black screens and menus)
public class ScreenFlash : MonoBehaviour
{
    private Image image;
    private float duration;
    private float strength;
    private float age;

    public static void Play(float strength, float duration)
    {
        GameObject obj = new GameObject("ScreenFlash");
        Canvas canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        Image image = new GameObject("White").AddComponent<Image>();
        image.transform.SetParent(obj.transform, false);
        image.raycastTarget = false;
        RectTransform r = image.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        ScreenFlash flash = obj.AddComponent<ScreenFlash>();
        flash.image = image;
        flash.strength = strength;
        flash.duration = Mathf.Max(0.05f, duration);
        flash.Update();
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = age / duration;
        image.color = new Color(1f, 1f, 1f, strength * (1f - t * t));
        if(t >= 1f) Destroy(gameObject);
    }
}
