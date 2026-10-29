using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

//Tools > Jam > Create Game Manager: makes a "GameManager" object in the open scene and moves the game-wide scripts onto it,
//keeping their settings and fixing every reference that pointed at the old ones. Objects left empty are deleted. Undoable
public static class GameManagerSetup
{
    //Order they show up in the Inspector
    static readonly Type[] Systems =
    {
        typeof(BodyManager),
        typeof(PlayerSprint),
        typeof(DialogueController),
        typeof(BlackScreenUI),
        typeof(GameOverScreen),
        typeof(SwapMemoryScreen),
        typeof(PlayerHUD),
    };

    //Added fresh if the scene doesn't have one anywhere
    static readonly Type[] NewSystems =
    {
        typeof(BodyFog),
        typeof(VibrationSense),
        typeof(PlayDead),
        typeof(PlayerInventory),
        typeof(BodyDrag),
        typeof(Interactor),
        typeof(ModalUI),
    };

    [MenuItem("Tools/Jam/Create Game Manager")]
    static void CreateGameManager()
    {
        if(EditorApplication.isPlaying)
        {
            Debug.LogError("Leave Play mode first");
            return;
        }
        Undo.SetCurrentGroupName("Create Game Manager");
        int undoGroup = Undo.GetCurrentGroup();

        GameManager manager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if(manager == null)
        {
            GameObject managerObj = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create Game Manager");
            manager = Undo.AddComponent<GameManager>(managerObj);
        }
        GameObject target = manager.gameObject;

        //New systems that only ever live on the GameManager
        foreach(var type in NewSystems)
        {
            if(target.GetComponent(type) != null || Object.FindAnyObjectByType(type, FindObjectsInactive.Include) != null) continue;
            Undo.AddComponent(target, type);
            EditorSceneManager.MarkSceneDirty(target.scene);
            Debug.Log($"Added {ObjectNames.NicifyVariableName(type.Name)} to the GameManager", target);
        }
        if(target.GetComponent<CapsuleSpawner>() == null && Object.FindAnyObjectByType<CapsuleSpawner>(FindObjectsInactive.Include) == null)
        {
            CapsuleSpawner spawner = Undo.AddComponent<CapsuleSpawner>(target);
            BodyCapsuleTools.FillBodyTypes(spawner);
            EditorSceneManager.MarkSceneDirty(target.scene);
            Debug.Log("Added Capsule Spawner to the GameManager", target);
        }

        //Old component -> its copy on the GameManager
        var moved = new Dictionary<Object, Object>();
        var leftBehind = new HashSet<GameObject>();
        foreach(var type in Systems)
        {
            foreach(var old in Object.FindObjectsByType(type, FindObjectsInactive.Include))
            {
                Component oldComp = (Component)old;
                if(oldComp.gameObject == target) continue;
                if(target.GetComponent(type) != null)
                {
                    Debug.LogWarning($"GameManager already has a {type.Name}, left the one on {oldComp.name} alone", oldComp);
                    continue;
                }
                ComponentUtility.CopyComponent(oldComp);
                ComponentUtility.PasteComponentAsNew(target);
                moved[oldComp] = target.GetComponent(type);
                leftBehind.Add(oldComp.gameObject);
            }
        }
        if(moved.Count == 0)
        {
            Debug.Log("Nothing to move, the GameManager already has everything. Save the scene if anything was added");
            Selection.activeGameObject = target;
            return;
        }

        //Point every reference in the scene at the new copies (including the copies pointing at each other)
        int fixedRefs = 0;
        //Only scripts can hold references to these (skips big tilemap data)
        foreach(var comp in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if(comp == null) continue;
            SerializedObject so = new SerializedObject(comp);
            SerializedProperty prop = so.GetIterator();
            bool changed = false;
            while(prop.Next(true))
            {
                if(prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if(prop.objectReferenceValue != null && moved.TryGetValue(prop.objectReferenceValue, out Object replacement))
                {
                    prop.objectReferenceValue = replacement;
                    changed = true;
                    fixedRefs++;
                }
            }
            if(changed)
            {
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            }
        }

        foreach(var old in moved.Keys)
        {
            Undo.DestroyObjectImmediate(old);
        }

        //Remove objects that only held those scripts
        int deleted = 0;
        foreach(var obj in leftBehind)
        {
            if(obj == null || obj.transform.childCount > 0) continue;
            if(obj.GetComponents<Component>().Length > 1) continue;
            Undo.DestroyObjectImmediate(obj);
            deleted++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(target.scene);
        Selection.activeGameObject = target;
        Debug.Log($"Moved {moved.Count} script(s) onto GameManager, fixed {fixedRefs} reference(s), removed {deleted} empty object(s). Save the scene to keep it.", target);
    }
}
