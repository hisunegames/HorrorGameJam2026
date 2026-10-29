using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

//Tools > Jam > Set Up Collapse Animations: for every body prefab in Assets/Prefab makes
//  Assets/Animations/Collapse/<Sheet>.anim         (the fall, from Assets/Art/<Name>Collapse.png, frames left to right)
//  Assets/Animations/Collapse/<Sheet>.controller   (one state playing it, the default state)
//and puts a disabled Animator with that controller on the prefab (BodyScript turns it on when the body is left).
//Clips and controllers that already exist are kept, so edits made in the Animation window survive re-running it
public static class CollapseAnimationSetup
{
    const string PrefabFolder = "Assets/Prefab";
    const string ArtFolder = "Assets/Art/";
    const string AnimFolder = "Assets/Animations/Collapse";
    const float FrameRate = 10f;

    [MenuItem("Tools/Jam/Set Up Collapse Animations")]
    static void SetUp()
    {
        EnsureFolder(AnimFolder);
        var done = new List<string>();
        var missing = new List<string>();
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab == null || prefab.GetComponent<BodyScript>() == null) continue;

            string sheet = FindSheet(prefab);
            if(sheet == null)
            {
                missing.Add(prefab.name);
                continue;
            }
            AnimatorController controller = GetOrCreateController(sheet);

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Animator animator = contents.GetComponent<Animator>();
                if(animator == null) animator = contents.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = false;
                contents.GetComponent<BodyScript>().collapseAnimator = animator;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            done.Add($"{prefab.name} -> {Path.GetFileNameWithoutExtension(sheet)}");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Collapse animations set: " + (done.Count > 0 ? string.Join(", ", done) : "none")
            + (missing.Count > 0 ? "\nNo <Name>Collapse.png sheet found for: " + string.Join(", ", missing) : "")
            + "\nEdit the clips in " + AnimFolder + " with the Animation window");
    }

    static AnimatorController GetOrCreateController(string sheet)
    {
        string name = Path.GetFileNameWithoutExtension(sheet);
        string clipPath = AnimFolder + "/" + name + ".anim";
        string controllerPath = AnimFolder + "/" + name + ".controller";

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if(clip == null)
        {
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
            clip = new AnimationClip { frameRate = FrameRate };
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for(int i = 0; i < frames.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = i / FrameRate, value = frames[i] };
            }
            //Repeat the last frame so it's held for a full frame before the clip ends
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / FrameRate, value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
        }
        return controller;
    }

    //Body's art folder (Assets/Art/Scientist_F/... -> ScientistFCollapse.png), or else the prefab name (Major -> MajorCollapse.png)
    static string FindSheet(GameObject prefab)
    {
        var candidates = new List<string>();
        BodyFacing facing = prefab.GetComponent<BodyFacing>();
        SpriteRenderer sr = prefab.GetComponent<SpriteRenderer>();
        Sprite art = facing != null && facing.frontSprite != null ? facing.frontSprite : sr != null ? sr.sprite : null;
        string artPath = art != null ? AssetDatabase.GetAssetPath(art) : null;
        if(!string.IsNullOrEmpty(artPath) && artPath.StartsWith(ArtFolder))
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(artPath));
            if(!string.IsNullOrEmpty(folder) && folder != "Art") candidates.Add(folder.Replace("_", ""));
        }
        candidates.Add(prefab.name);

        foreach(var name in candidates)
        {
            string sheet = ArtFolder + name + "Collapse.png";
            if(AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().Any()) return sheet;
        }
        return null;
    }

    static void EnsureFolder(string folder)
    {
        if(AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
