using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

//Camera follows the player but stops at the walls of the room the player is in.
//Builds a bounds box per Room from its Wall tilemap at start, then swaps the Confiner 2D shape when the player changes room
[RequireComponent(typeof(CinemachineConfiner2D))]
public class CameraRoomConfiner : MonoBehaviour
{
    public string roomPrefix = "Room";
    public string wallTilemapName = "Wall";
    //Negative pulls the edge in (hides some of the outer wall), positive shows some void past it
    public float padding = 0f;

    private CinemachineCamera vcam;
    private CinemachineConfiner2D confiner;
    private List<Bounds> roomRects = new List<Bounds>();
    private List<Collider2D> roomShapes = new List<Collider2D>();
    private int currentRoom = -1;

    void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
        confiner = GetComponent<CinemachineConfiner2D>();
        BuildRoomBounds();
    }

    //Update, not LateUpdate: door teleports happen in physics, so the new room is set before the camera moves
    void Update()
    {
        Transform target = vcam != null ? vcam.Follow : null;
        if(target == null) return;

        Vector2 pos = target.position;
        if(currentRoom >= 0 && roomRects[currentRoom].Contains(pos)) return;

        for(int i = 0; i < roomRects.Count; i++)
        {
            if(i == currentRoom || !roomRects[i].Contains(pos)) continue;
            currentRoom = i;
            confiner.BoundingShape2D = roomShapes[i];
            confiner.InvalidateBoundingShapeCache();
            return;
        }
    }

    //World size the camera shows (a hair bigger, so a room exactly that size doesn't wobble either)
    Vector2 ViewSize()
    {
        float height = vcam != null ? vcam.Lens.OrthographicSize * 2f : 5.625f;
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        return new Vector2(height * aspect, height) + Vector2.one * 0.05f;
    }

    void BuildRoomBounds()
    {
        foreach(var tilemap in FindObjectsByType<Tilemap>())
        {
            if(tilemap.name != wallTilemapName) continue;
            Transform room = FindRoom(tilemap.transform);
            if(room == null || !TryGetPaintedBounds(tilemap, out Bounds rect)) continue;

            rect.Expand(padding * 2f);
            //Z doesn't matter for 2D, make it deep so Contains ignores it
            rect.size = new Vector3(rect.size.x, rect.size.y, 1000f);

            GameObject obj = new GameObject("CameraBounds");
            obj.layer = LayerMask.NameToLayer("Ignore Raycast");
            obj.transform.SetParent(room, true);
            obj.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
            BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            //A room smaller than the view (the exit room) can't hold the camera, so the confiner jitters. Grow its box to
            //the view size, centred on the room: the camera then just sits still in the middle. The room rect used to
            //tell which room you're in stays the real size
            Vector2 view = ViewSize();
            box.size = new Vector2(Mathf.Max(rect.size.x, view.x), Mathf.Max(rect.size.y, view.y));

            roomRects.Add(rect);
            roomShapes.Add(box);
        }
        if(roomRects.Count == 0)
        {
            Debug.LogWarning($"CameraRoomConfiner: no '{wallTilemapName}' tilemaps under '{roomPrefix}*' objects found");
        }
    }

    Transform FindRoom(Transform t)
    {
        while(t != null)
        {
            if(t.name.StartsWith(roomPrefix)) return t;
            t = t.parent;
        }
        return null;
    }

    //World rect around the tiles actually painted (cellBounds can include erased cells)
    bool TryGetPaintedBounds(Tilemap tilemap, out Bounds rect)
    {
        rect = default;
        Vector3Int min = Vector3Int.zero, max = Vector3Int.zero;
        bool found = false;
        foreach(var cell in tilemap.cellBounds.allPositionsWithin)
        {
            if(!tilemap.HasTile(cell)) continue;
            if(!found)
            {
                min = max = cell;
                found = true;
                continue;
            }
            min = Vector3Int.Min(min, cell);
            max = Vector3Int.Max(max, cell);
        }
        if(!found) return false;

        Vector3 worldMin = tilemap.CellToWorld(min);
        Vector3 worldMax = tilemap.CellToWorld(max + new Vector3Int(1, 1, 0));
        rect = new Bounds((worldMin + worldMax) * 0.5f, worldMax - worldMin);
        return true;
    }
}
