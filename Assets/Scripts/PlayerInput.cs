using UnityEngine;

//Movement is WASD only: the arrow keys pick items (PlayerInventory) and do the nuke sequence
public static class PlayerInput
{
    public static Vector2 RawMove()
    {
        float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        return new Vector2(x, y);
    }

    //Eases towards the raw input like Unity's axes did (reaches full speed in a third of a second,
    //snaps to 0 when you reverse)
    public static Vector2 Smooth(Vector2 current, Vector2 raw, float deltaTime, float sensitivity = 3f)
    {
        float x = SmoothAxis(current.x, raw.x, deltaTime * sensitivity);
        float y = SmoothAxis(current.y, raw.y, deltaTime * sensitivity);
        return new Vector2(x, y);
    }

    static float SmoothAxis(float current, float target, float step)
    {
        if(target != 0f && Mathf.Sign(target) != Mathf.Sign(current)) current = 0f;
        return Mathf.MoveTowards(current, target, step);
    }
}
