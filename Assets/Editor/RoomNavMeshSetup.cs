using NavMeshPlus.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

//Tools > Jam > Set Up Room NavMesh: every Room's Ground tilemap becomes walkable and its Wall tilemap not walkable,
//so NavMeshPlus can bake the floors. Press Bake on the NavMesh object afterwards. Undoable
public static class RoomNavMeshSetup
{
    const string RoomPrefix = "Room";
    const string GroundName = "Ground";
    const string WallName = "Wall";
    //Unity's built-in navmesh areas
    const int Walkable = 0;
    const int NotWalkable = 1;

    [MenuItem("Tools/Jam/Set Up Room NavMesh")]
    static void SetUp()
    {
        int grounds = 0, walls = 0;
        foreach(var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            if(!InRoom(tilemap.transform)) continue;

            int area;
            if(tilemap.name == GroundName) { area = Walkable; grounds++; }
            else if(tilemap.name == WallName) { area = NotWalkable; walls++; }
            else continue;

            NavMeshModifier modifier = tilemap.GetComponent<NavMeshModifier>();
            if(modifier == null) modifier = Undo.AddComponent<NavMeshModifier>(tilemap.gameObject);
            Undo.RecordObject(modifier, "Set Up Room NavMesh");
            modifier.overrideArea = true;
            modifier.area = area;
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        }
        Debug.Log($"Room NavMesh: {grounds} floors walkable, {walls} walls not walkable. Now press Bake on the NavMesh object and save the scene.");
    }

    static bool InRoom(Transform t)
    {
        while(t != null)
        {
            if(t.name.StartsWith(RoomPrefix)) return true;
            t = t.parent;
        }
        return false;
    }
}
