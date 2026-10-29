using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//Bloom over the whole game: bright things (lab coats, screens, sparks, the flashbang) glow. Builds a global
//post-processing Volume at runtime, so there's no profile asset to set up. Tune it on the GameManager while playing.
//The camera needs Post Processing ticked (it is in GameplayScene)
public class GameBloom : MonoBehaviour
{
    public bool bloomOn = true;
    [Tooltip("How bright a pixel has to be before it glows (lower = more of the screen glows)")]
    [Range(0f, 2f)] public float threshold = 0.8f;
    [Range(0f, 5f)] public float intensity = 1.2f;
    [Tooltip("How far the glow spreads")]
    [Range(0f, 1f)] public float scatter = 0.6f;
    public Color tint = Color.white;

    private Volume volume;
    private Bloom bloom;

    void Awake()
    {
        GameObject obj = new GameObject("BloomVolume");
        obj.transform.SetParent(transform, false);
        volume = obj.AddComponent<Volume>();
        volume.isGlobal = true;
        //Above the project's default profile (which has bloom at 0)
        volume.priority = 10f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        bloom = profile.Add<Bloom>(true);
        volume.sharedProfile = profile;
        Apply();
    }

    //Live while playing, so it can be tuned in the Inspector
    void Update()
    {
        Apply();
    }

    void Apply()
    {
        if(volume == null) return;
        volume.weight = bloomOn ? 1f : 0f;
        bloom.threshold.value = threshold;
        bloom.intensity.value = intensity;
        bloom.scatter.value = scatter;
        bloom.tint.value = tint;
        //Cheaper filtering keeps WebGL smooth
        bloom.highQualityFiltering.value = false;
    }
}
