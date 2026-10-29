using UnityEngine;
using UnityEngine.UI;

//The end-game choice. Once the generator is running and all six story papers are read, you can leave (ExitDoor) -
//or get close to her and [E] to take her body so she can never get out. Then you are her, the vignette creeps in as your mind goes, and when it's black
//it's the Karma ending. Get her back to the warhead and arm it before that: the true ending (NukeTerminal).
//Put it on the GameManager
public class DaughterTransfer : MonoBehaviour
{
    public static bool Active { get; private set; }

    public BodyManager bodyManager;
    public KeyCode transferKey = KeyCode.E;
    //How close to her you have to get (she's still deadly up close)
    public float transferRange = 2f;
    //Seconds as her until everything goes black
    public float madnessTime = 75f;
    //Her walking speed once she's yours
    public float daughterSpeed = 3.2f;
    public Color vignetteColor = Color.black;

    [Header("People running around while you're her")]
    public int npcCount = 14;
    //Body prefabs to use for them. Empty = the Capsule Spawner's body types
    public BodyScript[] npcPrefabs;
    //Rooms whose middle is within this of you get people in them
    public float spawnRadius = 25f;
    //Not right on top of you
    public float minSpawnDistance = 3f;
    [Range(0f, 1f)] public float hintChance = 0.5f;
    public float npcFleeSpeed = 2.9f;
    //Walk into them and they die
    public float killRange = 0.7f;

    private readonly System.Collections.Generic.List<BodyScript> npcs = new System.Collections.Generic.List<BodyScript>();

    public float Madness01 { get; private set; }

    private Canvas canvas;
    private RawImage vignette;
    private Image blackout;
    private bool ended;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Active = false;
    }

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        Active = false;
    }

    void Update()
    {
        if(Active)
        {
            UpdateMadness();
            return;
        }
        if(Time.timeScale == 0f || ModalUI.IsOpen) return;
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        Enemy her = Reachable(body);
        //[E] is also swap: only take her if there's no ordinary body right there
        if(her != null && Input.GetKeyDown(transferKey) && bodyManager.FindNearestBody() == null) Transfer(body, her);
    }

    //She's close enough to take. Only once you know everything (all six story papers read) and the power's back on
    public Enemy Reachable(BodyScript body)
    {
        if(Active || !Generator.IsFixed || !NoteObject.ReadAllStoryNotes) return null;
        if(body == null || body.isHiding || body.isPlayingDead) return null;
        foreach(var enemy in FindObjectsByType<Enemy>())
        {
            if(Vector2.Distance(enemy.transform.position, body.transform.position) <= transferRange) return enemy;
        }
        return null;
    }

    //Turn the monster into a body the player controls, and swap into it
    void Transfer(BodyScript from, Enemy her)
    {
        GameObject go = her.gameObject;
        her.enabled = false;
        UnityEngine.AI.NavMeshAgent agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if(agent != null) agent.enabled = false;
        if(her.spriteRenderer != null && her.normalSprite != null) her.spriteRenderer.sprite = her.normalSprite;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if(rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if(go.GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
            col.offset = new Vector2(0f, 0.4f);
        }
        CustomBodySO so = ScriptableObject.CreateInstance<CustomBodySO>();
        so.name = "Daughter";
        so.moveSpeed = daughterSpeed / Mathf.Max(0.01f, GameManager.PlayerSpeedMultiplier);
        BodyScript daughter = go.AddComponent<BodyScript>();
        daughter.rb = rb;
        daughter.customBodySO = so;

        Active = true;
        Madness01 = 0f;
        bodyManager.SwapInto(daughter);
        SpeechBubble.Say(go.transform, Lore.TakeHerBody, 3f);
        BuildOverlay();
        SpawnNpcs(go.transform.position);
        Debug.Log("Transferred into the daughter", this);
    }

    void UpdateMadness()
    {
        if(ended || Time.timeScale == 0f) return;
        KillTouchedNpcs();
        Madness01 = Mathf.Clamp01(Madness01 + Time.deltaTime / Mathf.Max(1f, madnessTime));
        //The clear middle shrinks, then it all goes black. Zooming the texture out (UV rect) shrinks the hole while the
        //image stays full screen; the texture clamps, so everything past its edge is solid black
        float zoom = Mathf.Lerp(0.4f, 2.8f, Madness01);
        //A slow pulse, like a heartbeat in your ears
        zoom *= 1f + 0.04f * Mathf.Sin(Time.time * 3f) * Madness01;
        vignette.uvRect = new Rect(0.5f - zoom * 0.5f, 0.5f - zoom * 0.5f, zoom, zoom);
        Color b = vignetteColor;
        b.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, Madness01));
        blackout.color = b;
        if(Madness01 < 1f) return;
        ended = true;
        GameOverScreen end = GameManager.Get<GameOverScreen>();
        if(end != null) end.ShowEnding("KARMA", Lore.EndingKarma);
    }

    //Panicking survivors in the rooms around you: some only scream, some talk about the warhead
    void SpawnNpcs(Vector2 around)
    {
        var prefabs = new System.Collections.Generic.List<BodyScript>();
        if(npcPrefabs != null) foreach(var p in npcPrefabs) if(p != null) prefabs.Add(p);
        if(prefabs.Count == 0)
        {
            CapsuleSpawner spawner = GameManager.Get<CapsuleSpawner>();
            if(spawner != null) foreach(var p in spawner.bodyTypes) if(p != null) prefabs.Add(p);
        }
        if(prefabs.Count == 0)
        {
            Debug.LogWarning("Daughter Transfer: no body prefabs to spawn people from", this);
            return;
        }
        var rooms = new System.Collections.Generic.List<Bounds>();
        foreach(var room in RoomMap.AllRooms)
        {
            if(RoomMap.TryGetBounds(room, out Bounds b) && Vector2.Distance(b.center, around) <= spawnRadius) rooms.Add(b);
        }
        if(rooms.Count == 0) return;

        for(int i = 0; i < npcCount; i++)
        {
            if(!FindSpot(rooms, around, out Vector3 spot)) continue;
            BodyScript npc = Instantiate(prefabs[Random.Range(0, prefabs.Count)], spot, Quaternion.identity);
            npc.isCurrentBody = false;
            ResearcherAI ai = npc.GetComponent<ResearcherAI>();
            if(ai == null) ai = npc.gameObject.AddComponent<ResearcherAI>();
            ai.fleeSpeed = npcFleeSpeed;
            //What they shout when you come near
            ai.monsterLines = Random.value < hintChance ? Lore.NpcHints : Lore.NpcPanic;
            ai.talkCooldown = 5f;
            npcs.Add(npc);
        }
    }

    bool FindSpot(System.Collections.Generic.List<Bounds> rooms, Vector2 avoid, out Vector3 spot)
    {
        for(int tries = 0; tries < 20; tries++)
        {
            Bounds b = rooms[Random.Range(0, rooms.Count)];
            Vector3 p = new Vector3(Random.Range(b.min.x + 1.5f, b.max.x - 1.5f), Random.Range(b.min.y + 1.5f, b.max.y - 1.5f), 0f);
            if(!UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            if(Vector2.Distance(hit.position, avoid) < minSpawnDistance) continue;
            spot = hit.position;
            return true;
        }
        spot = Vector3.zero;
        return false;
    }

    //You're the monster: anyone you walk into dies (collapse + blood)
    void KillTouchedNpcs()
    {
        BodyScript me = bodyManager != null ? bodyManager.currentBody : null;
        if(me == null) return;
        foreach(var npc in npcs)
        {
            if(npc == null || npc.isConsumed) continue;
            if(Vector2.Distance(npc.transform.position, me.transform.position) <= killRange) npc.Consume();
        }
    }

    void BuildOverlay()
    {
        GameObject canvasObj = new GameObject("MadnessOverlay");
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        //Over the world and HUD, under note / nuke panels
        canvas.sortingOrder = 55;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        vignette = new GameObject("Vignette").AddComponent<RawImage>();
        vignette.transform.SetParent(canvasObj.transform, false);
        vignette.texture = MakeVignette();
        vignette.color = vignetteColor;
        vignette.raycastTarget = false;
        RectTransform r = vignette.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        blackout = new GameObject("Blackout").AddComponent<Image>();
        blackout.transform.SetParent(canvasObj.transform, false);
        blackout.raycastTarget = false;
        RectTransform br = blackout.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        blackout.color = Color.clear;
    }

    static Texture2D MakeVignette()
    {
        const int w = 96, h = 54;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[w * h];
        for(int y = 0; y < h; y++)
        {
            for(int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f;
                float dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.95f, d));
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
