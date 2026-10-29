using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//Tools > Jam > Create Body Capsule: drops a capsule with both sprites set where the Scene view is looking (Ctrl+D to copy it around).
//Tools > Jam > Refresh Capsule Body Types: fills the CapsuleSpawner with every body prefab in Assets/Prefab
public static class BodyCapsuleTools
{
    const string UnusedPath = "Assets/Art/PropNFurniture/Capsule.png";
    const string UsedPath = "Assets/Art/PropNFurniture/CapsuleUsed.png";
    const string PrefabFolder = "Assets/Prefab";

    [MenuItem("Tools/Jam/Create Body Capsule")]
    static void CreateCapsule()
    {
        Sprite unused = AssetDatabase.LoadAssetAtPath<Sprite>(UnusedPath);
        Sprite used = AssetDatabase.LoadAssetAtPath<Sprite>(UsedPath);
        if(unused == null || used == null)
        {
            Debug.LogError("Capsule sprites not found at " + UnusedPath + " / " + UsedPath);
            return;
        }

        GameObject capsuleObj = new GameObject("Capsule");
        Undo.RegisterCreatedObjectUndo(capsuleObj, "Create Body Capsule");
        Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        //Snap to the pixel grid
        capsuleObj.transform.position = new Vector3(Mathf.Round(pos.x * 16f) / 16f, Mathf.Round(pos.y * 16f) / 16f, 0f);
        if(Selection.activeTransform != null) Undo.SetTransformParent(capsuleObj.transform, Selection.activeTransform, "Create Body Capsule");

        SpriteRenderer sr = capsuleObj.AddComponent<SpriteRenderer>();
        sr.sprite = unused;
        //Default is drawn under the Tilemap layer, props go on Prop
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        BodyCapsule capsule = capsuleObj.AddComponent<BodyCapsule>();
        capsule.capsuleRenderer = sr;
        capsule.unusedSprite = unused;
        capsule.usedSprite = used;

        Selection.activeGameObject = capsuleObj;
        EditorSceneManager.MarkSceneDirty(capsuleObj.scene);

        if(Object.FindAnyObjectByType<CapsuleSpawner>(FindObjectsInactive.Include) == null)
        {
            Debug.LogWarning("No CapsuleSpawner in the scene yet, run Tools > Jam > Create Game Manager so capsules get bodies");
        }
    }

    [MenuItem("Tools/Jam/Refresh Capsule Body Types")]
    static void RefreshBodyTypes()
    {
        CapsuleSpawner spawner = Object.FindAnyObjectByType<CapsuleSpawner>(FindObjectsInactive.Include);
        if(spawner == null)
        {
            Debug.LogError("No CapsuleSpawner in the scene, run Tools > Jam > Create Game Manager first");
            return;
        }
        FillBodyTypes(spawner);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
    }

    //Every prefab in Assets/Prefab with a BodyScript, except the starting Player body
    public static void FillBodyTypes(CapsuleSpawner spawner)
    {
        Undo.RecordObject(spawner, "Refresh Capsule Body Types");
        spawner.bodyTypes.Clear();
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            BodyScript body = prefab != null ? prefab.GetComponent<BodyScript>() : null;
            if(body == null || body.isCurrentBody || prefab.name == "Player") continue;
            //Story bodies (Researcher F) are placed by hand, not dealt into capsules
            if(body.customBodySO != null && body.customBodySO.hasFingerprint) continue;
            spawner.bodyTypes.Add(body);
        }
        Debug.Log($"Capsule Spawner body types: {string.Join(", ", spawner.bodyTypes.ConvertAll(b => b.name))}", spawner);
    }
}
