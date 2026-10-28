using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

//Door between rooms. Stand at it and press the interact key to come out of the linked door in the other room.
//Made by Tools > Jam > Generate Room Doors, or placed by hand
[RequireComponent(typeof(BoxCollider2D))]
public class RoomDoor : MonoBehaviour
{
    //(body, door it walked into, door it came out of) - Enemy listens to follow the player
    public static event Action<BodyScript, RoomDoor, RoomDoor> OnBodyUsedDoor;

    public RoomDoor linkedDoor;
    public KeyCode interactKey = KeyCode.F;
    //Points from the door into its own room
    public Vector2 inwardDirection = Vector2.down;
    //How far inside the room you appear
    public float exitDistance = 0.9f;
    [Header("Transition")]
    //Optional, found automatically. Without one the move is instant
    public BlackScreenUI blackScreen;
    public float fadeTime = 0.15f;

    private static bool isTransitioning = false;
    private BoxCollider2D trigger;

    //Where you appear when coming out of this door
    public Vector3 ExitPoint => transform.position + (Vector3)(inwardDirection.normalized * exitDistance);

    void Awake()
    {
        trigger = GetComponent<BoxCollider2D>();
        if(blackScreen == null) blackScreen = GameManager.Get<BlackScreenUI>();
    }

    void OnDisable()
    {
        All.Remove(this);
        isTransitioning = false;
    }

    void Update()
    {
        if(isTransitioning || Time.timeScale == 0f) return;
        if(!Input.GetKeyDown(interactKey)) return;

        BodyScript body = CurrentBodyAtDoor();
        if(body == null) return;
        if(linkedDoor == null)
        {
            string room = transform.parent != null ? transform.parent.name + "/" : "";
            Debug.LogWarning($"{room}{name} isn't linked to another door", this);
            return;
        }
        StartCoroutine(GoThrough(body));
    }

    //For the interact prompt
    public static readonly System.Collections.Generic.List<RoomDoor> All = new System.Collections.Generic.List<RoomDoor>();

    void OnEnable()
    {
        All.Add(this);
    }

    public bool IsUsableBy(BodyScript body)
    {
        return linkedDoor != null && body != null && CurrentBodyAtDoor() == body;
    }

    BodyScript CurrentBodyAtDoor()
    {
        Bounds b = trigger.bounds;
        foreach(var hit in Physics2D.OverlapBoxAll(b.center, b.size, 0f))
        {
            BodyScript body = hit.GetComponent<BodyScript>();
            if(body != null && body.isCurrentBody) return body;
        }
        return null;
    }

    IEnumerator GoThrough(BodyScript body)
    {
        isTransitioning = true;
        Time.timeScale = 0f;
        if(blackScreen != null)
        {
            blackScreen.SetText("", "");
            yield return blackScreen.FadeIn(fadeTime);
        }

        Vector3 from = body.transform.position;
        Vector3 to = linkedDoor.ExitPoint;
        to.z = from.z;
        body.rb.linearVelocity = Vector2.zero;
        body.rb.position = to;
        body.transform.position = to;
        //Cut the camera instead of panning across the gap between rooms
        CinemachineCore.OnTargetObjectWarped(body.transform, to - from);
        OnBodyUsedDoor?.Invoke(body, this, linkedDoor);

        if(blackScreen != null)
        {
            yield return blackScreen.FadeOut(fadeTime);
        }
        Time.timeScale = 1f;
        isTransitioning = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = linkedDoor != null ? Color.green : Color.red;
        Gizmos.DrawWireSphere(ExitPoint, 0.2f);
        if(linkedDoor != null)
        {
            Gizmos.DrawLine(transform.position, linkedDoor.transform.position);
        }
    }
}
