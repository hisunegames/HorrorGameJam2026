using UnityEngine;

//Fog that closes in around the current body when it has no eyes (CustomBodySO.isNearsighted).
//Put it on the GameManager. Builds its own dithered fog sprite so nothing needs wiring
public class BodyFog : MonoBehaviour
{
    public BodyManager bodyManager;

    [Header("Look")]
    public Color fogColor = new Color(0.06f, 0.07f, 0.06f, 1f);
    //Soft edge between clear and full fog, in world units
    public float edgeWidth = 1.25f;
    //Fog texture pixels per world unit. 16 matches the game's pixels
    public int pixelsPerUnit = 16;
    //Has to cover the screen even when the camera stops at a wall and the player is near the edge
    public float coverSize = 32f;
    public float fadeTime = 0.6f;
    [Tooltip("How black and white the screen goes with no eyes (0 = keep colours, 1 = fully grey)")]
    [Range(0f, 1f)] public float desaturate = 1f;
    static readonly int DesaturateId = Shader.PropertyToID("_JamDesaturate");
    //Draw on top of the world (UI canvases are still above). Empty = the topmost sorting layer
    public string fogSortingLayer = "";
    public int fogSortingOrder = 32000;
    //Optional, e.g. Sprite-Unlit-Default so 2D lights don't dim the fog. Empty = default sprite material
    public Material material;

    private SpriteRenderer fog;

    void OnDisable()
    {
        Shader.SetGlobalFloat(DesaturateId, 0f);
    }
    private float builtRadius = -1f;
    private float alpha;

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        GameObject fogObj = new GameObject("BodyFog");
        fogObj.transform.SetParent(transform, false);
        fog = fogObj.AddComponent<SpriteRenderer>();
        //Layers are drawn in list order, so the last one is on top of everything
        SortingLayer[] layers = SortingLayer.layers;
        if(string.IsNullOrEmpty(fogSortingLayer) && layers.Length > 0) fog.sortingLayerID = layers[layers.Length - 1].id;
        else fog.sortingLayerName = fogSortingLayer;
        fog.sortingOrder = fogSortingOrder;
        if(material != null) fog.sharedMaterial = material;
        fog.enabled = false;
    }

    void LateUpdate()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        CustomBodySO so = body != null ? body.customBodySO : null;
        bool blind = so != null && so.isNearsighted;

        if(blind && !Mathf.Approximately(builtRadius, so.fogRadius))
        {
            Build(so.fogRadius);
        }
        alpha = Mathf.MoveTowards(alpha, blind ? 1f : 0f, fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f);
        //No eyes, no colour: the CRT pass greys the screen out with the fog
        Shader.SetGlobalFloat(DesaturateId, alpha * desaturate);
        fog.enabled = alpha > 0f && fog.sprite != null;
        if(!fog.enabled) return;

        Color c = fogColor;
        c.a *= alpha;
        fog.color = c;
        if(body != null)
        {
            Vector3 p = body.transform.position;
            fog.transform.position = new Vector3(p.x, p.y, fog.transform.position.z);
        }
    }

    //Square of fog with a clear circle in the middle. Ordered dithering on the edge keeps it looking pixel art
    void Build(float radius)
    {
        builtRadius = radius;
        int size = Mathf.CeilToInt(coverSize * pixelsPerUnit);
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        float centre = size * 0.5f;
        float inner = radius * pixelsPerUnit;
        float outer = (radius + Mathf.Max(0.01f, edgeWidth)) * pixelsPerUnit;
        Color32[] pixels = new Color32[size * size];
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(centre, centre));
                float t = Mathf.InverseLerp(inner, outer, dist);
                bool solid = t >= 1f || (t > 0f && t > Bayer(x, y));
                pixels[y * size + x] = solid ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        if(fog.sprite != null) Destroy(fog.sprite.texture);
        fog.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    //4x4 ordered dither threshold, 0..1
    static readonly int[] BayerMatrix = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    static float Bayer(int x, int y)
    {
        return (BayerMatrix[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
    }
}
