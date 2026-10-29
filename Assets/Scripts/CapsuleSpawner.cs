using System.Collections.Generic;
using UnityEngine;

//Fills every BodyCapsule in the scene when the game starts: at least one of each body type (in random capsules),
//the rest random picks from the pool. Put it on the GameManager
public class CapsuleSpawner : MonoBehaviour
{
    //Body prefabs to pick from (not the Player). Tools > Jam > Create Game Manager fills this in
    public List<BodyScript> bodyTypes = new List<BodyScript>();
    //0 = every capsule gets a body, 0.3 = roughly a third are left empty
    [Range(0f, 1f)] public float emptyChance = 0f;
    //Deal every type once before any repeats, so you don't get four of the same
    public bool avoidRepeats = true;
    //-1 = different every run. Set a number to get the same layout each time while testing
    public int seed = -1;

    private readonly List<BodyScript> deck = new List<BodyScript>();

    //One of every body type first (in random capsules), then the leftover capsules get random picks from the pool
    void Start()
    {
        bodyTypes.RemoveAll(b => b == null);
        BodyCapsule[] capsules = FindObjectsByType<BodyCapsule>();
        if(capsules.Length == 0) return;
        if(bodyTypes.Count == 0)
        {
            Debug.LogWarning("CapsuleSpawner has no body types to spawn", this);
            return;
        }

        Random.State oldState = Random.state;
        if(seed >= 0) Random.InitState(seed);
        //Same order every run so a seed gives the same layout
        System.Array.Sort(capsules, (a, b) =>
        {
            int byX = a.transform.position.x.CompareTo(b.transform.position.x);
            return byX != 0 ? byX : a.transform.position.y.CompareTo(b.transform.position.y);
        });

        //Capsules with a forced body get it; they count towards "one of each"
        var open = new List<BodyCapsule>();
        var placed = new HashSet<BodyScript>();
        foreach(var capsule in capsules)
        {
            if(capsule.forcedBody != null)
            {
                capsule.Fill(capsule.forcedBody);
                placed.Add(capsule.forcedBody);
            }
            else open.Add(capsule);
        }
        Shuffle(open);

        //Every type that isn't in a capsule yet gets one (the list may name a type twice to make it more common)
        var missing = new List<BodyScript>();
        foreach(var type in bodyTypes) if(!placed.Contains(type) && !missing.Contains(type)) missing.Add(type);
        Shuffle(missing);
        int next = 0;
        foreach(var type in missing)
        {
            if(next >= open.Count)
            {
                Debug.LogWarning($"Not enough capsules for one of every body type: {type.name} isn't in the level", this);
                continue;
            }
            open[next++].Fill(type);
        }

        //The rest: any from the pool, or left empty
        for(; next < open.Count; next++)
        {
            if(Random.value < emptyChance) continue;
            open[next].Fill(Draw());
        }
        if(seed >= 0) Random.state = oldState;
    }

    static void Shuffle<T>(List<T> list)
    {
        for(int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    BodyScript Draw()
    {
        if(!avoidRepeats) return bodyTypes[Random.Range(0, bodyTypes.Count)];
        if(deck.Count == 0)
        {
            deck.AddRange(bodyTypes);
            Shuffle(deck);
        }
        BodyScript next = deck[deck.Count - 1];
        deck.RemoveAt(deck.Count - 1);
        return next;
    }
}
