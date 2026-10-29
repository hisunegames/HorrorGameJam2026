using System.Collections.Generic;
using UnityEngine;

//Blood smears left behind dragged bodies (BodyDrag). The monster smells them and follows the trail (Enemy.FollowBlood)
public static class BloodTrail
{
    public class Drop
    {
        public Vector2 position;
        public float time;
        public GameObject obj;
    }

    const int MaxDrops = 400;
    private static readonly List<Drop> drops = new List<Drop>();
    private static Sprite[] sprites;

    public static void Add(Vector2 position, Sprite sprite = null)
    {
        drops.RemoveAll(d => d.obj == null);
        if(drops.Count >= MaxDrops)
        {
            Object.Destroy(drops[0].obj);
            drops.RemoveAt(0);
        }
        if(sprites == null) sprites = MakeSprites();

        GameObject obj = new GameObject("Blood");
        obj.transform.position = position + Random.insideUnitCircle * 0.06f;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        if(sprite != null)
        {
            //Real art: keep its colours, vary it a little so the trail doesn't repeat
            sr.sprite = sprite;
            sr.flipX = Random.value < 0.5f;
            sr.flipY = Random.value < 0.5f;
        }
        else
        {
            sr.sprite = sprites[Random.Range(0, sprites.Length)];
            sr.color = new Color(0.45f, 0.02f, 0.02f, 0.9f);
        }
        //On the floor, under props and bodies
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        sr.sortingOrder = -10;
        drops.Add(new Drop { position = obj.transform.position, time = Time.time, obj = obj });
    }

    //Newest drop within range that's newer than 'after'
    public static Drop FreshestNear(Vector2 from, float range, float after)
    {
        Drop best = null;
        foreach(var d in drops)
        {
            if(d.obj == null || d.time <= after) continue;
            if(Vector2.Distance(d.position, from) > range) continue;
            if(best == null || d.time > best.time) best = d;
        }
        return best;
    }

    //A few small irregular blobs, 1 texture pixel = 1 game pixel
    static Sprite[] MakeSprites()
    {
        var result = new Sprite[4];
        for(int s = 0; s < result.Length; s++)
        {
            const int size = 6;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color32[] px = new Color32[size * size];
            for(int i = 0; i < px.Length; i++)
            {
                int x = i % size, y = i / size;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(3f, 3f));
                bool on = d < 1.6f || (d < 2.8f && Random.value < 0.45f);
                px[i] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            result[s] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
        return result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        drops.Clear();
        sprites = null;
    }
}
