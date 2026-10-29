using System.Collections.Generic;
using UnityEngine;

//Puts a random body type into every BodyCapsule in the scene when the game starts. Put it on the GameManager
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

        foreach(var capsule in capsules)
        {
            if(capsule.forcedBody != null)
            {
                capsule.Fill(capsule.forcedBody);
                continue;
            }
            if(Random.value < emptyChance) continue;
            capsule.Fill(Draw());
        }
        if(seed >= 0) Random.state = oldState;
    }

    BodyScript Draw()
    {
        if(!avoidRepeats) return bodyTypes[Random.Range(0, bodyTypes.Count)];
        if(deck.Count == 0)
        {
            deck.AddRange(bodyTypes);
            //Shuffle
            for(int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }
        BodyScript next = deck[deck.Count - 1];
        deck.RemoveAt(deck.Count - 1);
        return next;
    }
}
