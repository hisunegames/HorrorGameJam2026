using System;
using UnityEngine;

//Anything that makes a sound the monster can hear calls Noise.Make
public static class Noise
{
    //(position, loudness) - loudness 1 = sprinting footsteps, Enemy hears it within hearingRange * loudness
    public static event Action<Vector2, float> OnNoise;

    public static void Make(Vector2 position, float loudness)
    {
        OnNoise?.Invoke(position, loudness);
    }
}
