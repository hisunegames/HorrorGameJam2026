using UnityEngine;
using UnityEngine.Rendering.Universal;

//Same as Full Screen Pass Renderer Feature, but respects the camera's Post Processing tick box
//(the built-in one ignores it, including the Scene view's effects menu > Post Processing)
public class CRTRendererFeature : FullScreenPassRendererFeature
{
    //Off = never in Scene view. On = follows the Scene view's Post Processing toggle
    public bool showInSceneView = true;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if(!renderingData.postProcessingEnabled || !renderingData.cameraData.postProcessEnabled) return;
        if(!showInSceneView && renderingData.cameraData.cameraType == CameraType.SceneView) return;

        base.AddRenderPasses(renderer, ref renderingData);
    }
}
