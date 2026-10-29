using System.Collections.Generic;
using UnityEngine;

//When the current body senses vibrations (CustomBodySO.sensesVibrations), the monster's footsteps show as
//ripples at its position, drawn above the fog. Off screen, a marker sits at the screen edge pointing at it.
//Put it on the GameManager. Builds its own sprites so nothing needs wiring
public class VibrationSense : MonoBehaviour
{
    public BodyManager bodyManager;

    [Header("Ripples")]
    public Color rippleColor = new Color(0.75f, 0.8f, 0.75f, 0.9f);
    //Seconds between ripples while it walks / chases (faster = closer together)
    public float stepInterval = 0.6f;
    public float chaseStepInterval = 0.3f;
    public float rippleLifetime = 1f;
    //World size the ripple grows to
    public float rippleSize = 2.5f;
    //Ripples further than this are fainter, and gone past maxRange (0 = felt anywhere)
    public float fullStrengthRange = 6f;
    public float maxRange = 0f;

    [Header("Edge Marker")]
    public bool showEdgeMarker = true;
    public Color markerColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    //Fully visible when the monster is this close, fades out towards markerFarDistance
    public float markerNearDistance = 6f;
    public float markerFarDistance = 25f;
    [Range(0f, 1f)] public float markerMinAlpha = 0.1f;
    //World units in from the screen edge
    public float edgeInset = 0.4f;

    [Header("Sorting")]
    //Above the fog (32000), below the black screen (32500). Uses the topmost sorting layer
    public int sortingOrder = 32100;

    private class Ripple
    {
        public SpriteRenderer sr;
        public float age;
        public float strength;
    }

    private readonly List<Ripple> ripples = new List<Ripple>();
    private Enemy[] enemies = new Enemy[0];
    private readonly Dictionary<Enemy, Vector3> lastPositions = new Dictionary<Enemy, Vector3>();
    private readonly Dictionary<Enemy, float> stepTimers = new Dictionary<Enemy, float>();
    private Sprite ringSprite;
    private Sprite arrowSprite;
    private SpriteRenderer marker;
    private int sortingLayerID;
    private float refreshTimer;

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        SortingLayer[] layers = SortingLayer.layers;
        sortingLayerID = layers.Length > 0 ? layers[layers.Length - 1].id : 0;
        ringSprite = MakeRing();
        arrowSprite = MakeArrow();

        GameObject markerObj = new GameObject("VibrationMarker");
        markerObj.transform.SetParent(transform, false);
        marker = markerObj.AddComponent<SpriteRenderer>();
        marker.sprite = arrowSprite;
        marker.sortingLayerID = sortingLayerID;
        marker.sortingOrder = sortingOrder;
        marker.enabled = false;
    }

    void Update()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        bool active = body != null && body.customBodySO != null && body.customBodySO.sensesVibrations;

        refreshTimer -= Time.deltaTime;
        if(refreshTimer <= 0f)
        {
            refreshTimer = 2f;
            enemies = FindObjectsByType<Enemy>();
        }

        Enemy nearest = null;
        float nearestDist = float.MaxValue;
        foreach(var enemy in enemies)
        {
            if(enemy == null) continue;
            Vector3 pos = enemy.transform.position;
            bool moved = lastPositions.TryGetValue(enemy, out Vector3 last) && (pos - last).sqrMagnitude > 0.0001f;
            lastPositions[enemy] = pos;
            if(!active) continue;

            float dist = Vector2.Distance(pos, body.transform.position);
            if(maxRange > 0f && dist > maxRange) continue;
            if(dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
            if(!moved) continue;

            stepTimers.TryGetValue(enemy, out float timer);
            timer -= Time.deltaTime;
            if(timer <= 0f)
            {
                bool chasing = enemy.currentState == Enemy.EnemyState.Chase || enemy.currentState == Enemy.EnemyState.FollowDoor;
                timer = chasing ? chaseStepInterval : stepInterval;
                float strength = dist <= fullStrengthRange ? 1f : Mathf.Clamp(fullStrengthRange / dist, 0.25f, 1f);
                SpawnRipple(pos, strength);
            }
            stepTimers[enemy] = timer;
        }

        UpdateRipples();
        UpdateMarker(active ? nearest : null, nearestDist);
    }

    void SpawnRipple(Vector3 pos, float strength)
    {
        Ripple ripple = null;
        foreach(var r in ripples)
        {
            if(!r.sr.enabled)
            {
                ripple = r;
                break;
            }
        }
        if(ripple == null)
        {
            GameObject obj = new GameObject("Ripple");
            obj.transform.SetParent(transform, false);
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.sortingLayerID = sortingLayerID;
            sr.sortingOrder = sortingOrder;
            ripple = new Ripple { sr = sr };
            ripples.Add(ripple);
        }
        ripple.age = 0f;
        ripple.strength = strength;
        ripple.sr.transform.position = new Vector3(pos.x, pos.y, 0f);
        ripple.sr.enabled = true;
    }

    void UpdateRipples()
    {
        foreach(var r in ripples)
        {
            if(!r.sr.enabled) continue;
            r.age += Time.deltaTime;
            float t = r.age / rippleLifetime;
            if(t >= 1f)
            {
                r.sr.enabled = false;
                continue;
            }
            //Ring sprite is 1 unit across
            float size = Mathf.Lerp(0.3f, rippleSize, t) * Mathf.Lerp(0.6f, 1f, r.strength);
            r.sr.transform.localScale = new Vector3(size, size, 1f);
            Color c = rippleColor;
            c.a *= (1f - t) * r.strength;
            r.sr.color = c;
        }
    }

    void UpdateMarker(Enemy target, float distance)
    {
        Camera cam = Camera.main;
        if(!showEdgeMarker || target == null || cam == null || !cam.orthographic)
        {
            marker.enabled = false;
            return;
        }
        Vector3 camPos = cam.transform.position;
        float halfH = cam.orthographicSize - edgeInset;
        float halfW = cam.orthographicSize * cam.aspect - edgeInset;
        Vector2 offset = (Vector2)(target.transform.position - camPos);
        //On screen: the ripples are enough
        if(Mathf.Abs(offset.x) <= halfW && Mathf.Abs(offset.y) <= halfH)
        {
            marker.enabled = false;
            return;
        }
        //Push onto the edge of the screen along the direction to the monster
        float scale = Mathf.Min(halfW / Mathf.Max(Mathf.Abs(offset.x), 0.001f), halfH / Mathf.Max(Mathf.Abs(offset.y), 0.001f));
        Vector2 edge = (Vector2)camPos + offset * scale;
        marker.transform.position = new Vector3(edge.x, edge.y, 0f);
        marker.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        //Beats faster when it's chasing
        bool chasing = target.currentState == Enemy.EnemyState.Chase || target.currentState == Enemy.EnemyState.FollowDoor;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (chasing ? 14f : 6f));
        //Closer = stronger
        float near = Mathf.InverseLerp(markerFarDistance, markerNearDistance, distance);
        Color c = markerColor;
        c.a *= Mathf.Lerp(markerMinAlpha, 1f, near) * Mathf.Lerp(0.6f, 1f, pulse);
        marker.color = c;
        marker.enabled = true;
    }

    //1 pixel ring, 16px = 1 world unit across
    static Sprite MakeRing()
    {
        const int size = 16;
        Texture2D tex = NewTexture(size);
        Color32[] px = new Color32[size * size];
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                px[y * size + x] = d > size * 0.5f - 1.5f && d <= size * 0.5f - 0.5f ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    //Small triangle pointing right
    static Sprite MakeArrow()
    {
        const int size = 7;
        Texture2D tex = NewTexture(size);
        Color32[] px = new Color32[size * size];
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(y - size / 2) <= (size - 1 - x) / 2;
                px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
    }

    static Texture2D NewTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}
