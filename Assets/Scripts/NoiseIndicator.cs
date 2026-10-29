using UnityEngine;

//Noise level the monster can hear: a 0-1 Level the HUD shows as a meter under the stamina bar, plus optional rings
//on the floor showing how far each noise carries (loudness x the monster's hearing range). Listens to the same Noise.Make calls the monster does
//(sprinting, boots, chainsaw, gun, salvaging...). Put it on the GameManager
public class NoiseIndicator : MonoBehaviour
{
    [Tooltip("Also draw a ring on the floor out to how far each noise carries")]
    public bool showRings = false;
    public Color ringColor = new Color(1f, 1f, 1f, 0.35f);
    public float ringTime = 0.6f;
    //Rings closer together than this are skipped (sprinting makes a noise every 0.35s)
    public float minRingGap = 0.3f;
    //Used when there's no monster in the scene to read its hearing range from
    public float fallbackHearingRange = 8f;
    //How fast the meter drains, per second
    public float levelDecay = 0.8f;
    //Over the fog (32000), under the vibration sense (32100)
    public int sortingOrder = 32050;

    //0 = silent, 1 = as loud as it gets (a noise heard from 1.5x a sprint's range)
    public float Level01 { get; private set; }

    private SpriteRenderer[] rings = new SpriteRenderer[6];
    private float[] ringAge = new float[6];
    private float[] ringSize = new float[6];
    private float lastRing = -10f;
    private Sprite ringSprite;
    private int sortingLayerID;

    void Awake()
    {
        SortingLayer[] layers = SortingLayer.layers;
        sortingLayerID = layers.Length > 0 ? layers[layers.Length - 1].id : 0;
        ringSprite = MakeRing();
    }

    void OnEnable() { Noise.OnNoise += HandleNoise; }
    void OnDisable() { Noise.OnNoise -= HandleNoise; }

    void HandleNoise(Vector2 position, float loudness)
    {
        Level01 = Mathf.Max(Level01, Mathf.Clamp01(loudness / 1.5f));
        if(!showRings || Time.time - lastRing < minRingGap) return;
        lastRing = Time.time;
        float hearing = fallbackHearingRange;
        Enemy enemy = FindAnyObjectByType<Enemy>();
        if(enemy != null) hearing = enemy.hearingRange;
        SpawnRing(position, hearing * loudness);
    }

    void SpawnRing(Vector2 position, float radius)
    {
        int slot = 0;
        for(int i = 0; i < rings.Length; i++)
        {
            if(rings[i] == null || !rings[i].enabled)
            {
                slot = i;
                break;
            }
            if(ringAge[i] > ringAge[slot]) slot = i;
        }
        if(rings[slot] == null)
        {
            rings[slot] = new GameObject("NoiseRing").AddComponent<SpriteRenderer>();
            rings[slot].transform.SetParent(transform, false);
            rings[slot].sprite = ringSprite;
            rings[slot].sortingLayerID = sortingLayerID;
            rings[slot].sortingOrder = sortingOrder;
        }
        rings[slot].transform.position = new Vector3(position.x, position.y, 0f);
        ringAge[slot] = 0f;
        ringSize[slot] = radius * 2f;
        rings[slot].enabled = true;
    }

    void Update()
    {
        Level01 = Mathf.MoveTowards(Level01, 0f, levelDecay * Time.deltaTime);
        for(int i = 0; i < rings.Length; i++)
        {
            SpriteRenderer sr = rings[i];
            if(sr == null || !sr.enabled) continue;
            ringAge[i] += Time.deltaTime;
            float t = ringAge[i] / ringTime;
            if(t >= 1f)
            {
                sr.enabled = false;
                continue;
            }
            //Grows out fast, then hangs at the full range while it fades
            float size = ringSize[i] * Mathf.Sqrt(Mathf.Clamp01(t * 1.6f));
            sr.transform.localScale = new Vector3(size, size, 1f);
            Color c = ringColor;
            c.a *= 1f - t;
            sr.color = c;
        }
    }

    //1 pixel ring, 64px across = 1 world unit (thin even when scaled up big)
    static Sprite MakeRing()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[size * size];
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                px[y * size + x] = d > size * 0.5f - 1.2f && d <= size * 0.5f ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
