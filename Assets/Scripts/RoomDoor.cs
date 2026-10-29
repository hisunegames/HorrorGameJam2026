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
    //Shortcut doors (metal door tiles) need a body that can unlock doors (Major) and the monster can't follow through them
    public bool isShortcut = false;
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
        if(body == null || body.isPlayingDead || !IsUsableBy(body)) return;
        //Something else nearer has the key (the Interactor decides)
        Interactor interactor = GameManager.Get<Interactor>();
        if(interactor != null && interactor.DoorFocus != this) return;
        //Not linked anywhere: the fake door out of the lab
        if(linkedDoor == null)
        {
            SpeechBubble.Say(body.transform, Lore.FakeDoor, 2.5f);
            return;
        }
        if (IsLocked)
        {
            SFXManager.Instance.PlaySFX("lock");
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

    //Standing in any door's trigger (Interactor gives the door priority over lockers etc. then)
    public static bool AnyAt(BodyScript body)
    {
        return NearestAt(body, out _) != null;
    }

    //The door the body is standing at, and how far its middle is from the door tile (Interactor compares this with
    //lockers, boxes etc. so the nearest thing gets the key)
    public static RoomDoor NearestAt(BodyScript body, out float distance)
    {
        RoomDoor best = null;
        distance = float.MaxValue;
        if(body == null) return null;
        Collider2D col = body.GetComponent<Collider2D>();
        Vector2 from = col != null && col.enabled ? (Vector2)col.bounds.center : (Vector2)body.transform.position;
        foreach(var door in All)
        {
            if(door == null || !door.isActiveAndEnabled || !door.IsUsableBy(body)) continue;
            float d = Vector2.Distance(from, door.transform.position);
            if(d < distance)
            {
                distance = d;
                best = door;
            }
        }
        return best;
    }

    //Standing at it (the prompt shows "Locked" instead of "Enter" if IsLocked)
    public bool IsUsableBy(BodyScript body)
    {
        return body != null && CurrentBodyAtDoor() == body;
    }

    //Shortcut and the body you're in can't unlock doors (same check as LockedDoor)
    public bool IsLocked
    {
        get
        {
            if (!isShortcut) return false;
            BodyManager bodyManager = GameManager.Get<BodyManager>();
            return bodyManager == null || !bodyManager.CurrentCanUnlockDoors;
        }
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
        SFXManager.Instance.PlaySFX("door");
        Vector3 from = body.transform.position;
        Vector3 to = linkedDoor.ExitPoint;
        to.z = from.z;
        body.rb.linearVelocity = Vector2.zero;
        body.rb.position = to;
        body.transform.position = to;
        //Cut the camera instead of panning across the gap between rooms
        CinemachineCore.OnTargetObjectWarped(body.transform, to - from);
        //Whatever you're dragging comes through with you
        BodyDrag drag = GameManager.Get<BodyDrag>();
        if(drag != null) drag.CarryThrough(body, to, linkedDoor.inwardDirection);
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
        Gizmos.color = linkedDoor == null ? Color.red : isShortcut ? new Color(1f, 0.6f, 0f) : Color.green;
        Gizmos.DrawWireSphere(ExitPoint, 0.2f);
        if(linkedDoor != null)
        {
            Gizmos.DrawLine(transform.position, linkedDoor.transform.position);
        }
    }
}
