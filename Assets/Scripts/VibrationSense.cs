using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//When the current body senses vibrations (CustomBodySO.sensesVibrations, the blind guy), every monster is tracked
//all the time: a marker above it when it's on screen, an arrow on the screen edge pointing at it when it isn't,
//each with its distance ("7m"). The Amalgam is red, small monsters amber. Footsteps also show as ripples.
//All drawn above the fog. Put it on the GameManager. Builds its own sprites and labels so nothing needs wiring
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
    //Ripples further than this are fainter
    public float fullStrengthRange = 6f;

    [Header("Markers")]
    public Color amalgamColor = new Color(1f, 0.15f, 0.12f, 1f);
    public Color smallMonsterColor = new Color(1f, 0.7f, 0.15f, 1f);
    //Arrow size (the arrow is 7 pixels)
    public float markerScale = 2f;
    //Brightest this close, dimmest (Min Alpha, still easy to see) this far
    public float markerNearDistance = 5f;
    public float markerFarDistance = 25f;
    [Range(0f, 1f)] public float markerMinAlpha = 0.6f;
    //World units in from the screen edge
    public float edgeInset = 0.5f;
    //How far above the monster's feet the on-screen marker floats
    public float amalgamMarkerHeight = 2.2f;
    public float smallMarkerHeight = 1.1f;
    public int distanceFontSize = 22;

    [Header("Sorting")]
    //Above the fog (32000), below the black screen (32500). Uses the topmost sorting layer
    public int sortingOrder = 32100;

    private class Ripple
    {
        public SpriteRenderer sr;
        public float age;
        public float strength;
    }

    private class Tracked
    {
        public Transform target;
        public bool small;
        public SpriteRenderer marker;
        public Text label;
        public Vector3 lastPos;
        public float stepTimer;
    }

    private readonly List<Ripple> ripples = new List<Ripple>();
    private readonly Dictionary<Component, Tracked> tracked = new Dictionary<Component, Tracked>();
    private Sprite ringSprite;
    private Sprite arrowSprite;
    private Canvas labelCanvas;
    private int sortingLayerID;
    private float refreshTimer;

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        SortingLayer[] layers = SortingLayer.layers;
        sortingLayerID = layers.Length > 0 ? layers[layers.Length - 1].id : 0;
        ringSprite = MakeRing();
        arrowSprite = MakeArrow();
        BuildLabelCanvas();
    }

    void Update()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        bool active = body != null && body.customBodySO != null && body.customBodySO.sensesVibrations && Time.timeScale > 0f;

        refreshTimer -= Time.deltaTime;
        if(refreshTimer <= 0f)
        {
            refreshTimer = 1f;
            RefreshTargets();
        }

        Camera cam = Camera.main;
        foreach(var pair in tracked)
        {
            Tracked t = pair.Value;
            bool alive = pair.Key != null && t.target != null && pair.Key is MonoBehaviour mb && mb.isActiveAndEnabled
                         && !(pair.Key is SmallMonster s && s.IsDead);
            if(!active || !alive || cam == null)
            {
                Hide(t);
                continue;
            }
            Vector3 pos = t.target.position;
            float dist = Vector2.Distance(pos, body.transform.position);
            bool chasing = IsChasing(pair.Key);
            Footsteps(t, pos, dist, chasing);
            PlaceMarker(t, cam, pos, dist, chasing);
        }
        UpdateRipples();
    }

    //Every Amalgam and small monster in the scene gets a marker + label
    void RefreshTargets()
    {
        foreach(var enemy in FindObjectsByType<Enemy>()) Track(enemy, false);
        foreach(var small in SmallMonster.All) if(small != null) Track(small, true);
    }

    void Track(Component monster, bool small)
    {
        if(tracked.ContainsKey(monster)) return;
        GameObject obj = new GameObject("VibrationMarker");
        obj.transform.SetParent(transform, false);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = arrowSprite;
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;
        tracked[monster] = new Tracked
        {
            target = monster.transform,
            small = small,
            marker = sr,
            label = MakeLabel(),
            lastPos = monster.transform.position,
        };
    }

    static bool IsChasing(Component monster)
    {
        if(monster is Enemy e) return e.currentState == Enemy.EnemyState.Chase || e.currentState == Enemy.EnemyState.FollowDoor;
        return false;
    }

    void Hide(Tracked t)
    {
        if(t.marker != null) t.marker.enabled = false;
        if(t.label != null) t.label.enabled = false;
    }

    void Footsteps(Tracked t, Vector3 pos, float dist, bool chasing)
    {
        bool moved = (pos - t.lastPos).sqrMagnitude > 0.0001f;
        t.lastPos = pos;
        if(!moved) return;
        t.stepTimer -= Time.deltaTime;
        if(t.stepTimer > 0f) return;
        t.stepTimer = chasing ? chaseStepInterval : stepInterval;
        float strength = dist <= fullStrengthRange ? 1f : Mathf.Clamp(fullStrengthRange / dist, 0.25f, 1f);
        SpawnRipple(pos, strength * (t.small ? 0.6f : 1f));
    }

    //Above it when on screen, on the screen edge pointing at it when not. Always with the distance
    void PlaceMarker(Tracked t, Camera cam, Vector3 pos, float dist, bool chasing)
    {
        Vector3 camPos = cam.transform.position;
        float halfH = cam.orthographicSize - edgeInset;
        float halfW = cam.orthographicSize * cam.aspect - edgeInset;
        Vector2 above = (Vector2)pos + Vector2.up * (t.small ? smallMarkerHeight : amalgamMarkerHeight);
        Vector2 offset = above - (Vector2)camPos;
        bool onScreen = Mathf.Abs(offset.x) <= halfW && Mathf.Abs(offset.y) <= halfH;

        Vector2 at;
        float angle;
        if(onScreen)
        {
            //Pointing down at it
            at = above;
            angle = -90f;
        }
        else
        {
            float scale = Mathf.Min(halfW / Mathf.Max(Mathf.Abs(offset.x), 0.001f), halfH / Mathf.Max(Mathf.Abs(offset.y), 0.001f));
            at = (Vector2)camPos + offset * scale;
            Vector2 toMonster = (Vector2)pos - (Vector2)camPos;
            angle = Mathf.Atan2(toMonster.y, toMonster.x) * Mathf.Rad2Deg;
        }
        t.marker.transform.position = new Vector3(at.x, at.y, 0f);
        t.marker.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        //Beats faster while it's chasing
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (chasing ? 14f : 5f));
        float size = markerScale * (1f + 0.15f * pulse);
        t.marker.transform.localScale = new Vector3(size, size, 1f);
        float near = Mathf.InverseLerp(markerFarDistance, markerNearDistance, dist);
        Color c = t.small ? smallMonsterColor : amalgamColor;
        c.a *= Mathf.Lerp(markerMinAlpha, 1f, near);
        t.marker.color = c;
        t.marker.enabled = true;

        //Distance next to the marker, towards the middle of the screen so it stays readable at the edges
        Vector2 inward = ((Vector2)camPos - at).normalized * 0.45f;
        Vector3 screen = cam.WorldToScreenPoint(new Vector3(at.x + inward.x, at.y + inward.y + (onScreen ? 0.35f : 0f), 0f));
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)labelCanvas.transform, screen, null, out Vector2 local))
        {
            t.label.rectTransform.anchoredPosition = local;
        }
        t.label.text = Mathf.RoundToInt(dist) + "m";
        Color lc = c;
        lc.a = Mathf.Max(0.75f, c.a);
        t.label.color = lc;
        t.label.enabled = true;
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

    void BuildLabelCanvas()
    {
        GameObject canvasObj = new GameObject("VibrationLabels");
        canvasObj.transform.SetParent(transform, false);
        labelCanvas = canvasObj.AddComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        //Under the HUD prompt (50)
        labelCanvas.sortingOrder = 46;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
    }

    Text MakeLabel()
    {
        GameObject obj = new GameObject("Distance");
        obj.transform.SetParent(labelCanvas.transform, false);
        Text t = obj.AddComponent<Text>();
        t.font = GameFont.Get();
        t.fontSize = distanceFontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        t.rectTransform.sizeDelta = new Vector2(80, 30);
        t.enabled = false;
        return t;
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
