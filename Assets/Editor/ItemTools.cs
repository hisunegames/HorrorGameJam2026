using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

//Tools > Jam > Create Item Assets: every item the game uses, in Assets/Items (existing ones are left alone).
//Tools > Jam > Create Item Pickup: a pickup of the Item Data selected in the Project window (Bear Trap if none).
//Tools > Jam > Set Up Props: turns placed props into working ones by their sprite (lockers, notes, machine, generator,
//  nuke, chairs, puddles, item sprites -> pickups). Undoable. Electric boxes and the flask table are left to you.
//Tools > Jam > Create Small Monster / Create Researcher F
public static class ItemTools
{
    const string ItemFolder = "Assets/Items";
    const string Props = "Assets/Art/PropNFurniture/";
    const string PlaceholderFolder = "Assets/Art/Placeholder";

    [MenuItem("Tools/Jam/Create Item Assets")]
    static void CreateItemAssets()
    {
        if(!AssetDatabase.IsValidFolder(ItemFolder)) AssetDatabase.CreateFolder("Assets", "Items");
        int made = 0;

        Sprite[] trap = Sprites(Props + "Beartrap.png");
        made += Make("BearTrap", "Bear Trap", ItemKind.Trap, trap.FirstOrDefault(), i =>
        {
            i.isTrap = true;
            i.snappedSprite = trap.LastOrDefault();
        });
        made += Make("Knife", "Knife", ItemKind.Weapon, Sprite(Props + "Knife.png"), i => Weapon(i, 2, 1f, false, true, 0f));
        made += Make("Saw", "Saw", ItemKind.Weapon, Sprite(Props + "NewSaw.png"), i => Weapon(i, 2, 1f, false, true, 0f));
        made += Make("Chainsaw", "Chainsaw", ItemKind.Weapon, Sprite(Props + "NewChainsaw.png"), i => Weapon(i, 2, 2f, false, true, 1.5f));
        made += Make("Shotgun", "Shotgun", ItemKind.Weapon, Sprite(Props + "Shotgun.png"), i => Weapon(i, 1, 5f, true, false, 2.5f));
        made += Make("StunGun", "Stun Gun", ItemKind.Weapon, Sprite(Props + "Stun_Gun.png"), i => Weapon(i, 2, 4f, true, false, 0.3f));
        made += Make("Adrenaline", "Adrenaline", ItemKind.Adrenaline, Sprite(Props + "Adrenaline.png"), null);
        made += Make("Toolbox", "Toolbox", ItemKind.KeyItem, Sprite(Props + "Toolbox.png"), null);
        made += Make("Wire", "Wire", ItemKind.KeyItem, Sprite(Props + "WireCoil.png"), null);
        made += Make("EmptyBattery", "Empty Battery", ItemKind.KeyItem, Art("BatteryEmpty") ?? Placeholder("EmptyBattery", Battery(false)), null);
        made += Make("ChargedBattery", "Charged Battery", ItemKind.KeyItem, Art("BatteryFull") ?? Placeholder("ChargedBattery", Battery(true)), null);
        made += Make("SpareParts", "Spare Parts", ItemKind.KeyItem, Art("SpareParts") ?? Placeholder("SpareParts", Parts()), null);
        made += Make("Finger", "Researcher's Finger", ItemKind.KeyItem, Art("Finger") ?? Placeholder("Finger", Finger()), null);
        AssetDatabase.SaveAssets();
        Debug.Log($"Item assets: {made} new in {ItemFolder}. Edit their numbers there (range, charges, stun...)");
    }

    static void Weapon(ItemData i, int charges, float range, bool ranged, bool canCut, float noise)
    {
        i.charges = charges;
        i.range = range;
        i.ranged = ranged;
        i.canCut = canCut;
        i.useNoise = noise;
        i.weaponStun = 5f;
    }

    static int Make(string file, string name, ItemKind kind, Sprite sprite, System.Action<ItemData> setup)
    {
        string path = $"{ItemFolder}/{file}.asset";
        ItemData existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if(existing != null)
        {
            //Real art has turned up for an item still showing a placeholder
            string current = existing.worldSprite != null ? AssetDatabase.GetAssetPath(existing.worldSprite) : "";
            if(sprite != null && current.StartsWith(PlaceholderFolder) && !AssetDatabase.GetAssetPath(sprite).StartsWith(PlaceholderFolder))
            {
                existing.worldSprite = sprite;
                EditorUtility.SetDirty(existing);
                Debug.Log($"{file}: now uses {AssetDatabase.GetAssetPath(sprite)}", existing);
            }
            //Made by the old Bear Trap tool before kinds existed
            if(existing.isTrap && existing.kind != ItemKind.Trap)
            {
                existing.kind = ItemKind.Trap;
                EditorUtility.SetDirty(existing);
            }
            return 0;
        }
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = name;
        item.kind = kind;
        item.worldSprite = sprite;
        setup?.Invoke(item);
        AssetDatabase.CreateAsset(item, path);
        return 1;
    }

    [MenuItem("Tools/Jam/Create Item Pickup")]
    static void CreatePickup()
    {
        ItemData item = Selection.activeObject as ItemData;
        if(item == null) item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/BearTrap.asset");
        if(item == null)
        {
            Debug.LogError("Select an Item Data in the Project window, or run Tools > Jam > Create Item Assets first");
            return;
        }
        GameObject obj = new GameObject(item.itemName);
        Undo.RegisterCreatedObjectUndo(obj, "Create Item Pickup");
        obj.transform.position = ScenePoint();
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = item.worldSprite;
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        WorldItem pickup = obj.AddComponent<WorldItem>();
        pickup.item = item;
        pickup.range = 0.8f;
        Selection.activeGameObject = obj;
        EditorSceneManager.MarkSceneDirty(obj.scene);
    }

    //Placed prop sprite -> what it should be
    [MenuItem("Tools/Jam/Set Up Props")]
    static void SetUpProps()
    {
        var itemsBySprite = new Dictionary<string, string>
        {
            {"Beartrap", "BearTrap"}, {"Knife", "Knife"}, {"Saw", "Saw"}, {"NewSaw", "Saw"}, {"Chainsaw", "Chainsaw"},
            {"NewChainsaw", "Chainsaw"}, {"Shotgun", "Shotgun"}, {"Stun_Gun", "StunGun"}, {"Adrenaline", "Adrenaline"},
            {"Toolbox", "Toolbox"},
        };
        var counts = new Dictionary<string, int>();
        foreach(var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if(sr.sprite == null) continue;
            string path = AssetDatabase.GetAssetPath(sr.sprite);
            if(!path.StartsWith(Props)) continue;
            string tex = Path.GetFileNameWithoutExtension(path);
            GameObject go = sr.gameObject;
            string what = null;

            if(itemsBySprite.TryGetValue(tex, out string itemFile))
            {
                if(go.GetComponent<WorldItem>() == null && go.GetComponent<ArmedTrap>() == null)
                {
                    ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemFolder}/{itemFile}.asset");
                    if(item == null) continue;
                    Add<WorldItem>(go).item = item;
                    go.GetComponent<WorldItem>().range = 0.8f;
                    what = "pickups";
                }
            }
            else if(tex == "Locker" && go.GetComponent<Locker>() == null) { Add<Locker>(go); what = "lockers"; }
            else if(tex == "Notes" && go.GetComponent<NoteObject>() == null) { Add<NoteObject>(go); what = "notes (write their text!)"; }
            else if(tex == "LargerMachine" && go.GetComponent<SalvageMachine>() == null) { Add<SalvageMachine>(go); what = "salvage machine"; }
            else if(tex == "Generator" && go.GetComponent<Generator>() == null) { Add<Generator>(go); what = "generator"; }
            else if(tex == "Nuke" && go.GetComponent<NukeTerminal>() == null) { Add<NukeTerminal>(go); what = "nuke"; }
            else if(tex == "Chair" && go.GetComponent<DraggableProp>() == null) { Add<DraggableProp>(go); what = "chairs (draggable)"; }
            else if(tex == "Body" && go.GetComponent<DraggableProp>() == null)
            {
                DraggableProp corpse = Add<DraggableProp>(go);
                corpse.isBody = true;
                corpse.canStandOn = false;
                what = "corpse props (draggable)";
            }
            else if(tex == "Water" && go.GetComponent<WaterPuddle>() == null)
            {
                Add<WaterPuddle>(go);
                if(go.GetComponent<Collider2D>() == null)
                {
                    BoxCollider2D box = Undo.AddComponent<BoxCollider2D>(go);
                    box.isTrigger = true;
                }
                what = "puddles";
            }
            if(what == null) continue;
            counts[what] = counts.TryGetValue(what, out int n) ? n + 1 : 1;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
        //Blood trail art for dragged bodies
        BodyDrag drag = Object.FindAnyObjectByType<BodyDrag>(FindObjectsInactive.Include);
        if(drag != null && drag.bloodSprite == null)
        {
            Undo.RecordObject(drag, "Set Up Props");
            drag.bloodSprite = Sprite(Props + "BLoodPuddle.png");
            EditorSceneManager.MarkSceneDirty(drag.gameObject.scene);
            counts["blood trail sprite"] = 1;
        }
        string summary = counts.Count == 0 ? "nothing new" : string.Join(", ", counts.Select(p => $"{p.Value} {p.Key}"));
        Debug.Log("Set Up Props: " + summary + ".\nStill by hand: Fingerprint Scanner + Wire Box on the electric boxes, Chemical Charging Station on the flask table, Exit Door in the exit room. Save the scene.");
    }

    static T Add<T>(GameObject go) where T : Component
    {
        //Reset() runs and fills in item slots
        return Undo.AddComponent<T>(go);
    }

    [MenuItem("Tools/Jam/Create Small Monster")]
    static void CreateSmallMonster()
    {
        GameObject root = new GameObject("SmallMonster");
        Undo.RegisterCreatedObjectUndo(root, "Create Small Monster");
        root.transform.position = ScenePoint();
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.25f;
        agent.height = 1f;
        //Same as the big one
        agent.baseOffset = 0.5f;
        agent.acceleration = 30f;
        agent.angularSpeed = 0f;
        root.AddComponent<SmallMonster>();

        GameObject spriteObj = new GameObject("Sprite");
        spriteObj.transform.SetParent(root.transform, false);
        SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite("Assets/Art/KillableMonster/Small_Monster.png");
        if(SortingLayer.NameToID("Player&Monster") != 0) sr.sortingLayerName = "Player&Monster";
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    //Copy of BlindGuy with the female scientist art, her own body asset (fingerprint, prey, corpse sprite)
    [MenuItem("Tools/Jam/Create Researcher F")]
    static void CreateResearcherF()
    {
        const string prefabPath = "Assets/Prefab/ResearcherF.prefab";
        const string soPath = "Assets/BodySO/ResearcherF.asset";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(existing != null)
        {
            //Made before she had the AI
            if(existing.GetComponent<ResearcherAI>() == null)
            {
                GameObject c = PrefabUtility.LoadPrefabContents(prefabPath);
                c.AddComponent<ResearcherAI>();
                PrefabUtility.SaveAsPrefabAsset(c, prefabPath);
                PrefabUtility.UnloadPrefabContents(c);
                Debug.Log("Added ResearcherAI to " + prefabPath);
            }
            else Debug.Log(prefabPath + " already exists, drag it into the level");
            return;
        }
        CustomBodySO so = AssetDatabase.LoadAssetAtPath<CustomBodySO>(soPath);
        if(so == null)
        {
            so = ScriptableObject.CreateInstance<CustomBodySO>();
            so.moveSpeed = 5f;
            so.hasFingerprint = true;
            so.isPrey = true;
            so.corpseSprite = Sprite(Props + "Body.png");
            so.fingerItem = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Finger.asset");
            AssetDatabase.CreateAsset(so, soPath);
        }
        if(!AssetDatabase.CopyAsset("Assets/Prefab/BlindGuy.prefab", prefabPath))
        {
            Debug.LogError("Couldn't copy BlindGuy.prefab");
            return;
        }
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            contents.name = "ResearcherF";
            BodyScript body = contents.GetComponent<BodyScript>();
            body.customBodySO = so;
            body.collapseAnimator = null;
            Animator animator = contents.GetComponent<Animator>();
            if(animator != null) animator.runtimeAnimatorController = null;
            const string art = "Assets/Art/Scientist_F/Scientist_Female_";
            BodyFacing facing = contents.GetComponent<BodyFacing>();
            if(facing != null)
            {
                facing.frontSprite = Sprite(art + "Front.png");
                facing.backSprite = Sprite(art + "Back.png");
                facing.sideSprite = Sprite(art + "Side_Right.png");
            }
            SpriteRenderer sr = contents.GetComponent<SpriteRenderer>();
            sr.sprite = Sprite(art + "Front.png");
            sr.color = Color.white;
            //Scared wandering / running away / lines
            if(contents.GetComponent<ResearcherAI>() == null) contents.AddComponent<ResearcherAI>();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Made " + prefabPath + ". Run Tools > Jam > Set Up Collapse Animations to give her the ScientistFCollapse fall, then place her");
    }

    static Vector3 ScenePoint()
    {
        Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        return new Vector3(Mathf.Round(pos.x * 16f) / 16f, Mathf.Round(pos.y * 16f) / 16f, 0f);
    }

    //Real sprite in PropNFurniture if there is one
    static Sprite Art(string name)
    {
        return Sprite(Props + name + ".png");
    }

    static Sprite Sprite(string path)
    {
        return Sprites(path).FirstOrDefault();
    }

    static Sprite[] Sprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
    }

    //Tiny pixel art stand-ins until real sprites exist. Saved as PNGs so they can be painted over
    static Sprite Placeholder(string name, string[] rows)
    {
        string path = $"{PlaceholderFolder}/{name}.png";
        if(!File.Exists(path))
        {
            if(!AssetDatabase.IsValidFolder(PlaceholderFolder)) AssetDatabase.CreateFolder("Assets/Art", "Placeholder");
            var palette = new Dictionary<char, Color32>
            {
                {'.', new Color32(0, 0, 0, 0)}, {'k', new Color32(30, 30, 35, 255)}, {'g', new Color32(120, 125, 130, 255)},
                {'l', new Color32(190, 195, 200, 255)}, {'y', new Color32(240, 200, 60, 255)}, {'G', new Color32(80, 200, 90, 255)},
                {'s', new Color32(225, 180, 150, 255)}, {'r', new Color32(150, 20, 20, 255)},
            };
            int h = rows.Length, w = rows[0].Length;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for(int y = 0; y < h; y++)
            {
                for(int x = 0; x < w; x++) tex.SetPixel(x, h - 1 - y, palette[rows[y][x]]);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        return Sprite(path);
    }

    static string[] Battery(bool charged)
    {
        char c = charged ? 'G' : 'g';
        return new[]
        {
            "..kk..",
            ".kllk.",
            ".k" + c + c + "k.",
            ".k" + c + c + "k.",
            ".k" + c + c + "k.",
            ".kyyk.",
            ".kkkk.",
        };
    }

    static string[] Parts()
    {
        return new[]
        {
            ".g..l..",
            "glg.lk.",
            ".g.kkkk",
            "..kgggk",
            "l.kgkgk",
            "lk.kkk.",
        };
    }

    static string[] Finger()
    {
        return new[]
        {
            ".ss.",
            "slls",
            "ssss",
            "ssss",
            "ssss",
            "rrrr",
        };
    }
}
