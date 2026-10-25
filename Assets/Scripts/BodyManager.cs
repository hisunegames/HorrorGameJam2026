using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class BodyManager : MonoBehaviour
{
    public BodyScript currentBody;
    public CinemachineCamera cinemachineCamera;
    public LayerMask bodyLayer;
    public float interactradius;
    [Header("Abilities Keeper")]
    public List<CustomBodySO> activeAbilities = new List<CustomBodySO>();
    /*[Header("NearsightPostprocess")]
    Add postprocess*/
    public int maxActive = 3;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TrySwapBody();
        }
    }

    void TrySwapBody()
    {
        if(currentBody == null) return;

        Collider2D detectBody = Physics2D.OverlapCircle(currentBody.transform.position,interactradius,bodyLayer);


        if(detectBody != null)
        {
            BodyScript target = detectBody.GetComponent<BodyScript>();
            if(target != currentBody)
            {
                DoSwapBody(currentBody,target);
            }
            else
            {
                Debug.Log("Same body");
            }
        }
        else
        {
            Debug.Log("No Body");
        }
    }


    void DoSwapBody(BodyScript currentBody, BodyScript targetBody)
    {
        currentBody.LeftOldBody();

        targetBody.ChangeToNewBody();
        this.currentBody = targetBody;

        if(cinemachineCamera != null)
        {
            cinemachineCamera.Target.TrackingTarget = targetBody.transform;
        }
        else
        {
            Debug.Log("No Cinemachine");
        }

        if(targetBody.customBodySO != null)
        {
            activeAbilities.Add(targetBody.customBodySO);
            if(activeAbilities.Count > maxActive)
            {
                activeAbilities.RemoveAt(0);
            }
        }
        else
        {
            Debug.Log("TargetBody has no CustomSO");
        }

        //TODO loop in activeAbilities list to check if there is nearsight bool true so it activate post process of nearsight
    }

}
