using UnityEngine;

//A radio set down with [Q] (Lure item). After a short delay it starts blaring: the Amalgam is forced to come to it
//from wherever it is, through the doors, and it keeps making noise for a while. Leave it in Researcher F's room and
//the monster finds her there. One use: it dies once it's done
public class RadioLure : MonoBehaviour
{
    public ItemData item;

    private float timer;
    private float pulseTimer;
    private bool playing;
    private bool done;
    private AudioSource source;
    private SpriteRenderer sr;

    public static RadioLure Place(ItemData item, Vector2 position)
    {
        GameObject obj = new GameObject(item.itemName + " (on)");
        obj.transform.position = position;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = item.worldSprite;
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        sr.sortingOrder = 5;
        RadioLure radio = obj.AddComponent<RadioLure>();
        radio.item = item;
        radio.sr = sr;
        radio.timer = item.lureDelay;
        return radio;
    }

    void Update()
    {
        if(done || item == null) return;
        timer -= Time.deltaTime;
        if(!playing)
        {
            if(timer > 0f) return;
            StartPlaying();
            return;
        }
        //Little shake so it reads as "on"
        if(sr != null) sr.transform.localPosition = (Vector3)(Random.insideUnitCircle * 0.02f);
        pulseTimer -= Time.deltaTime;
        if(pulseTimer <= 0f)
        {
            pulseTimer = 1f;
            Noise.Make(transform.position, item.lureLoudness);
        }
        if(timer <= 0f) Stop();
    }

    void StartPlaying()
    {
        playing = true;
        timer = item.lureDuration;
        pulseTimer = 0f;
        //Every Amalgam comes running, wherever it is
        foreach(var enemy in FindObjectsByType<Enemy>()) enemy.Lure(transform.position);
        if(item.lureSound != null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = item.lureSound;
            source.loop = true;
            source.spatialBlend = 0f;
            source.Play();
        }
    }

    void Stop()
    {
        done = true;
        if(source != null) source.Stop();
        //Dead radio: greyed out, stays where it is
        if(sr != null)
        {
            sr.transform.localPosition = Vector3.zero;
            sr.color = new Color(0.55f, 0.55f, 0.55f);
        }
    }
}
