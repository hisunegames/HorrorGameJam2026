using UnityEngine;
using UnityEngine.AI;

public class GlassTrigger : MonoBehaviour
{
    public NavMeshObstacle invisibleWall;
    public Enemy enemy;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            if(invisibleWall != null)
            {
                invisibleWall.enabled = false;
            }
            enemy.SoundTrigger(transform.position);
        }
    }
}
