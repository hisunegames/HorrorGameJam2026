using UnityEngine;

[CreateAssetMenu(fileName = "CustomBodySO", menuName = "Scriptable Objects/CustomBodySO")]
public class CustomBodySO : ScriptableObject
{
    [Header("Abilites")]
    public bool canUnlockDoors = false;
    public bool canSwim = false;
    public bool canCrawl = false;

    [Header("Cons")]
    public bool isNearsighted = false;
    public bool isSlow = false;

    [Header("Stats")]
    public float moveSpeed = 5f;
}
