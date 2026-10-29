using Febucci.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//The game's font (VCR OSD Mono) from Assets/Resources/Fonts. Every UI script uses this instead of Unity's default
public static class GameFont
{
    const string FontPath = "Fonts/VCR_OSD_MONO_1.001";

    private static Font font;
    private static TMP_FontAsset tmpFont;

    //Legacy UI Text
    public static Font Get()
    {
        if(font == null) font = Resources.Load<Font>(FontPath);
        if(font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    //TextMeshPro
    public static TMP_FontAsset GetTMP()
    {
        if(tmpFont == null)
        {
            tmpFont = Resources.Load<TMP_FontAsset>(FontPath + " SDF");
            //VCR has no Russian letters: those come from TMP's LiberationSans (dynamic, so any character works)
            TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            if(tmpFont != null && fallback != null)
            {
                if(tmpFont.fallbackFontAssetTable == null) tmpFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if(!tmpFont.fallbackFontAssetTable.Contains(fallback)) tmpFont.fallbackFontAssetTable.Add(fallback);
            }
        }
        return tmpFont;
    }

    //Every text in the open scenes (menus, panels, prefabs placed by hand) switched to VCR OSD Mono, hidden ones too
    public static void ApplyToScene()
    {
        Font legacy = Get();
        foreach(Text t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(t.font != legacy) t.font = legacy;
        }
        TMP_FontAsset tmp = GetTMP();
        if(tmp == null) return;
        foreach(TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(t.font != tmp) t.font = tmp;
        }
    }

    //Text Animator (Febucci) on a TextMeshPro text: letters type in one by one with the built-in appear effects.
    //Show lines with the returned typewriter's ShowText. Runs on unscaled time so it still types while paused
    public static TypewriterByCharacter Animate(TMP_Text text, float secondsPerLetter = 0.03f)
    {
        TextAnimator_TMP animator = text.GetComponent<TextAnimator_TMP>();
        if(animator == null) animator = text.gameObject.AddComponent<TextAnimator_TMP>();
        animator.timeScale = TimeScale.Unscaled;
        TypewriterByCharacter typewriter = text.GetComponent<TypewriterByCharacter>();
        if(typewriter == null) typewriter = text.gameObject.AddComponent<TypewriterByCharacter>();
        typewriter.useTypeWriter = true;
        typewriter.waitForNormalChars = secondsPerLetter;
        return typewriter;
    }
}

//Puts the font on everything at the start of each scene (GameManager adds it)
public class GameFontApplier : MonoBehaviour
{
    System.Collections.IEnumerator Start()
    {
        GameFont.ApplyToScene();
        //Again once everything else has built its UI
        yield return null;
        GameFont.ApplyToScene();
    }
}
