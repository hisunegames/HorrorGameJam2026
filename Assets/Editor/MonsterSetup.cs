using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//Tools > Jam > Set Up Monster Sprites: puts the Amalgam sprites on every Enemy in the open scene(s). Undoable
public static class MonsterSetup
{
    const string NormalPath = "Assets/Art/Amalgam/Amalgam_Norm.png";
    const string ChasePath = "Assets/Art/Amalgam/Amalgam_Cry.png";

    [MenuItem("Tools/Jam/Set Up Monster Sprites")]
    static void SetUpMonsterSprites()
    {
        Sprite normal = AssetDatabase.LoadAssetAtPath<Sprite>(NormalPath);
        Sprite chase = AssetDatabase.LoadAssetAtPath<Sprite>(ChasePath);
        if(normal == null || chase == null)
        {
            Debug.LogError("Monster sprites not found in Assets/Art/Amalgam");
            return;
        }

        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include);
        foreach(var enemy in enemies)
        {
            SpriteRenderer sr = enemy.spriteRenderer != null ? enemy.spriteRenderer : enemy.GetComponentInChildren<SpriteRenderer>();

            Undo.RecordObject(enemy, "Set Up Monster Sprites");
            enemy.spriteRenderer = sr;
            enemy.normalSprite = normal;
            enemy.chaseSprite = chase;
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);

            if(sr != null)
            {
                Undo.RecordObject(sr, "Set Up Monster Sprites");
                sr.sprite = normal;
                //Placeholder was tinted red, real art needs white
                sr.color = Color.white;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
            }
            EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene);
        }
        Debug.Log($"Monster sprites set on {enemies.Length} enemy(s). Save the scene to keep it.");
    }
}
