using UnityEngine;

public class LockedDoor : MonoBehaviour
{
    public BodyManager bodyManager;
    public Rigidbody2D doorrb;
    public float interactrange = 2f;
    public bool isDoorClosed = true;
    void Start()
    {
        doorrb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    void Update()
    {
        if(isDoorClosed && Input.GetKeyDown(KeyCode.F))
        {
            TryOpenDoor();
        }
    }   

    void TryOpenDoor()
    {
        if(bodyManager == null || bodyManager.currentBody == null) return;
        float distance = Vector2.Distance(transform.position,bodyManager.currentBody.transform.position);
        if(distance > interactrange) return;
        
        bool canUnlockDoors = false;
        foreach(var x in bodyManager.activeAbilities)
        {
            if (x == null) continue;
            if (x.canUnlockDoors)
            {
                canUnlockDoors = true;
                break;
            }
        }
        if (!canUnlockDoors)
        {
            Debug.Log("Don't have Unlock Door Ability");
            return;
        }
        OpenDoor();
    }
    
    void OpenDoor()
    {
        isDoorClosed = false;
        Debug.Log("Door Open");
        gameObject.SetActive(false);
    }
}
