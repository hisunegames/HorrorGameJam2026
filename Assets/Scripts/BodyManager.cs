using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class BodyManager : MonoBehaviour
{
    //(old body, new body, swap count) - for name reveal / fade / audio to listen to
    public event Action<BodyScript, BodyScript, int> OnBodySwapped;
    public int swapCount = 0;

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

        BodyScript target = FindNearestBody();

        if(target != null)
        {
            DoSwapBody(currentBody,target);
        }
        else
        {
            Debug.Log("No Body");
        }
    }

    //Nearest body in range that isn't us and hasn't been used up already
    BodyScript FindNearestBody()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(currentBody.transform.position,interactradius,bodyLayer);
        BodyScript nearest = null;
        float nearestDist = float.MaxValue;
        foreach(var hit in hits)
        {
            BodyScript body = hit.GetComponent<BodyScript>();
            if(body == null || body == currentBody || body.isVacant) continue;

            float dist = Vector2.Distance(currentBody.transform.position,body.transform.position);
            if(dist < nearestDist)
            {
                nearestDist = dist;
                nearest = body;
            }
        }
        return nearest;
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

        swapCount++;
        OnBodySwapped?.Invoke(currentBody,targetBody,swapCount);
    }

}
