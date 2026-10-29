using TMPro;
using UnityEngine;

//The game's one font (VCR OSD Mono, in Assets/Resources/Fonts), for every bit of UI text built in code.
//Falls back to Unity's built-in font if the file is missing
public static class GameFont
{
    const string FontPath = "Fonts/VCR_OSD_MONO_1.001";
    const string TmpPath = "Fonts/VCR_OSD_MONO_1.001 SDF";

    private static Font font;
    private static TMP_FontAsset tmpFont;

    //For legacy UI Text
    public static Font Get()
    {
        if(font == null) font = Resources.Load<Font>(FontPath);
        if(font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    //For TextMeshPro text (the dialogue box). Null if the SDF asset is missing
    public static TMP_FontAsset GetTMP()
    {
        if(tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>(TmpPath);
        return tmpFont;
    }
}
