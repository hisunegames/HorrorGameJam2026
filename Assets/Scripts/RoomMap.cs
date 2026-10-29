using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//Which Room a point is in, and routes between rooms through the normal (non-shortcut) doors.
//Rooms are the "Room*" objects; their area is the painted part of their Wall tilemap
public static class RoomMap
{
    const string RoomPrefix = "Room";
    const string WallName = "Wall";

    private static readonly Dictionary<Transform, Bounds> rooms = new Dictionary<Transform, Bounds>();

    public static Transform RoomAt(Vector2 position)
    {
        if(NeedsRebuild()) Rebuild();
        foreach(var pair in rooms)
        {
            Bounds b = pair.Value;
            if(position.x >= b.min.x && position.x <= b.max.x && position.y >= b.min.y && position.y <= b.max.y) return pair.Key;
        }
        return null;
    }

    //Area of a room (its painted Wall tiles)
    public static bool TryGetBounds(Transform room, out Bounds bounds)
    {
        bounds = default;
        if(room == null) return false;
        if(NeedsRebuild()) Rebuild();
        return rooms.TryGetValue(room, out bounds);
    }

    public static Transform RoomOf(RoomDoor door)
    {
        return door != null ? FindRoom(door.transform) : null;
    }

    //Doors to walk through, in order, to get from one room to another without shortcuts. Null if there's no way
    public static List<RoomDoor> FindRoute(Transform from, Transform to)
    {
        if(from == null || to == null) return null;
        if(from == to) return new List<RoomDoor>();
        var cameFrom = new Dictionary<Transform, RoomDoor>();
        var queue = new Queue<Transform>();
        queue.Enqueue(from);
        cameFrom[from] = null;
        while(queue.Count > 0)
        {
            Transform room = queue.Dequeue();
            foreach(var door in RoomDoor.All)
            {
                if(door.isShortcut || door.linkedDoor == null || RoomOf(door) != room) continue;
                Transform next = RoomOf(door.linkedDoor);
                if(next == null || cameFrom.ContainsKey(next)) continue;
                cameFrom[next] = door;
                if(next == to) return Unwind(cameFrom, to);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    //Greedy: in each room take the (normal) door whose other side is closest to the target, never revisiting a room.
    //Falls back to the shortest route if that runs into a dead end
    public static List<RoomDoor> GreedyRoute(Transform from, Transform to, Vector3 target)
    {
        if(from == null || to == null) return null;
        var route = new List<RoomDoor>();
        var visited = new HashSet<Transform> { from };
        Transform room = from;
        for(int step = 0; step < 64 && room != to; step++)
        {
            RoomDoor best = null;
            float bestDist = float.MaxValue;
            foreach(var door in RoomDoor.All)
            {
                if(door.isShortcut || door.linkedDoor == null || RoomOf(door) != room) continue;
                Transform next = RoomOf(door.linkedDoor);
                if(next == null || visited.Contains(next)) continue;
                //A door straight into the target room always wins
                float dist = next == to ? -1f : Vector2.Distance(door.linkedDoor.ExitPoint, target);
                if(dist < bestDist)
                {
                    bestDist = dist;
                    best = door;
                }
            }
            if(best == null) return FindRoute(from, to);
            route.Add(best);
            room = RoomOf(best.linkedDoor);
            visited.Add(room);
        }
        return room == to ? route : FindRoute(from, to);
    }

    static List<RoomDoor> Unwind(Dictionary<Transform, RoomDoor> cameFrom, Transform to)
    {
        var route = new List<RoomDoor>();
        Transform room = to;
        while(cameFrom[room] != null)
        {
            RoomDoor door = cameFrom[room];
            route.Insert(0, door);
            room = RoomOf(door);
        }
        return route;
    }

    static bool NeedsRebuild()
    {
        if(rooms.Count == 0) return true;
        //Scene reloaded: old room objects are gone
        foreach(var room in rooms.Keys)
        {
            return room == null;
        }
        return false;
    }

    public static void Rebuild()
    {
        rooms.Clear();
        foreach(var tilemap in Object.FindObjectsByType<Tilemap>())
        {
            if(tilemap.name != WallName) continue;
            Transform room = FindRoom(tilemap.transform);
            if(room == null || rooms.ContainsKey(room) || !TryGetPaintedBounds(tilemap, out Bounds b)) continue;
            rooms[room] = b;
        }
    }

    public static bool TryGetPaintedBounds(Tilemap tilemap, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach(var cell in tilemap.cellBounds.allPositionsWithin)
        {
            if(!tilemap.HasTile(cell)) continue;
            Vector3 min = tilemap.CellToWorld(cell);
            Vector3 max = tilemap.CellToWorld(cell + Vector3Int.one);
            Bounds cellBounds = new Bounds((min + max) * 0.5f, Vector3.zero);
            cellBounds.Encapsulate(min);
            cellBounds.Encapsulate(max);
            if(!any) bounds = cellBounds;
            else bounds.Encapsulate(cellBounds);
            any = true;
        }
        return any;
    }

    public static Transform FindRoom(Transform t)
    {
        while(t != null)
        {
            if(t.name.StartsWith(RoomPrefix)) return t;
            t = t.parent;
        }
        return null;
    }
}
