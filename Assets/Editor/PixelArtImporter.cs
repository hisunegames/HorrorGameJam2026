using UnityEditor;
using UnityEngine;

//Auto pixel-art import settings for anything under Assets/Art
//Only runs on first import (or while PPU is still Unity's default 100), so manual tweaks are kept
public class PixelArtImporter : AssetPostprocessor
{
    const string ArtFolder = "Assets/Art/";
    const float PixelsPerUnit = 16f;

    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith(ArtFolder)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        if(!importer.importSettingsMissing && !Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        //Pivot at the feet so y-sorting works
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
