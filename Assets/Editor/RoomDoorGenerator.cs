using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

//Tools > Jam > Generate Room Doors: finds the door tiles in every Room's tilemaps, makes a RoomDoor trigger
//for each and links doors that face each other across the gap between rooms. Undoable, re-running replaces them
public static class RoomDoorGenerator
{
    //Door sprites in HorrorJam-Sheet and the direction from each door into its room
    static readonly Dictionary<string, Vector2> DoorSprites = new Dictionary<string, Vector2>
    {
        {"HorrorJam-Sheet_16", Vector2.right}, //door in the left wall
        {"HorrorJam-Sheet_15", Vector2.left},  //right wall
        {"HorrorJam-Sheet_20", Vector2.up},    //bottom wall
        {"HorrorJam-Sheet_22", Vector2.down},  //top wall
    };
    const string DoorPrefix = "Door_";
    //How far off-line two doors can be and still count as facing each other
    const float AlignTolerance = 1f;
    //Longest gap between two rooms' doors
    const float MaxLinkDistance = 10f;

    [MenuItem("Tools/Jam/Generate Room Doors")]
    static void Generate()
    {
        foreach(var old in Object.FindObjectsByType<RoomDoor>(FindObjectsInactive.Include))
        {
            if(old.name.StartsWith(DoorPrefix)) Undo.DestroyObjectImmediate(old.gameObject);
        }

        List<RoomDoor> doors = new List<RoomDoor>();
        foreach(var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            Transform room = FindRoom(tilemap.transform);
            if(room == null) continue;
            foreach(var cell in tilemap.cellBounds.allPositionsWithin)
            {
                Sprite sprite = tilemap.GetSprite(cell);
                if(sprite == null || !DoorSprites.TryGetValue(sprite.name, out Vector2 inward)) continue;
                doors.Add(CreateDoor(room, tilemap.GetCellCenterWorld(cell), inward));
            }
        }

        //Closest facing pairs first, so a door with no partner can't steal a further one
        List<(float dist, RoomDoor a, RoomDoor b)> pairs = new List<(float, RoomDoor, RoomDoor)>();
        for(int i = 0; i < doors.Count; i++)
        {
            for(int j = i + 1; j < doors.Count; j++)
            {
                if(Faces(doors[i], doors[j], out float dist)) pairs.Add((dist, doors[i], doors[j]));
            }
        }
        pairs.Sort((p, q) => p.dist.CompareTo(q.dist));
        foreach(var pair in pairs)
        {
            if(pair.a.linkedDoor != null || pair.b.linkedDoor != null) continue;
            pair.a.linkedDoor = pair.b;
            pair.b.linkedDoor = pair.a;
        }

        int unlinked = 0;
        foreach(var door in doors)
        {
            if(door.linkedDoor != null) continue;
            unlinked++;
            Debug.LogWarning($"{door.transform.parent.name}/{door.name} has no door facing it", door);
        }
        if(doors.Count > 0) EditorSceneManager.MarkSceneDirty(doors[0].gameObject.scene);
        Debug.Log($"Room doors: {doors.Count} made, {doors.Count - unlinked} linked, {unlinked} without a partner. Save the scene to keep them.");
    }

    static Transform FindRoom(Transform t)
    {
        while(t != null)
        {
            if(t.name.StartsWith("Room")) return t;
            t = t.parent;
        }
        return null;
    }

    static RoomDoor CreateDoor(Transform room, Vector3 position, Vector2 inward)
    {
        string side = inward == Vector2.right ? "Left" : inward == Vector2.left ? "Right" : inward == Vector2.up ? "Bottom" : "Top";
        GameObject obj = new GameObject(DoorPrefix + side);
        Undo.RegisterCreatedObjectUndo(obj, "Generate Room Doors");
        obj.transform.SetParent(room, true);
        obj.transform.position = position;

        //Small area just inside the room in front of the door; stand here and press the interact key
        BoxCollider2D trigger = obj.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(Mathf.Abs(inward.x) > 0f ? 0.6f : 1f, Mathf.Abs(inward.y) > 0f ? 0.6f : 1f);
        trigger.offset = inward * 0.6f;

        RoomDoor door = obj.AddComponent<RoomDoor>();
        door.inwardDirection = inward;
        return door;
    }

    static bool Faces(RoomDoor a, RoomDoor b, out float dist)
    {
        dist = 0f;
        if(a.transform.parent == b.transform.parent) return false;
        if(Vector2.Dot(a.inwardDirection, b.inwardDirection) > -0.9f) return false;

        Vector2 delta = b.transform.position - a.transform.position;
        //b has to be on the outside of a's wall
        if(Vector2.Dot(delta, -a.inwardDirection) <= 0f) return false;
        Vector2 across = new Vector2(a.inwardDirection.y, a.inwardDirection.x);
        if(Mathf.Abs(Vector2.Dot(delta, across)) > AlignTolerance) return false;

        dist = delta.magnitude;
        return dist <= MaxLinkDistance;
    }
}
