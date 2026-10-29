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
    public DialogueController dialogueController;

    //Only the body you're in right now counts (leaving the Major takes his keys with him)
    public bool CurrentCanUnlockDoors => currentBody != null && currentBody.customBodySO != null && currentBody.customBodySO.canUnlockDoors;

    void Start()
    {
        //Not assigned in the scene: use the body marked as current (the Player prefab is)
        if(currentBody == null)
        {
            foreach(var body in FindObjectsByType<BodyScript>())
            {
                if(body.isCurrentBody)
                {
                    currentBody = body;
                    break;
                }
            }
        }
        //Follow the starting body without having to set Tracking Target by hand
        if(cinemachineCamera != null && currentBody != null)
        {
            cinemachineCamera.Target.TrackingTarget = currentBody.transform;
        }
    }

    void Update()
    {
        //Paused by swap screen or game over
        if (Time.timeScale == 0f) return;
        //Lying still, no swapping or talking until you get up
        if (currentBody != null && currentBody.isPlayingDead) return;
        //In the daughter's body there's no jumping back out
        if (Input.GetKeyDown(KeyCode.E) && !DaughterTransfer.Active)
        {
            TrySwapBody();
        }

        if (Input.GetKeyDown(KeyCode.F)) 
        {
            TryShowDialogue();
        }
    }

    void TryShowDialogue()
    {
        if(currentBody == null) return;

        BodyScript target = FindNearestBody();

        if(target != null)
        {
            DialogueData dialogueData = target.GetComponent<DialogueData>();
            if (dialogueData == null || dialogueData.dialogueLines.Length == 0)
            {
                Debug.Log("No Dialogue");
                return;
            }
            dialogueController.StartDialogue(dialogueData.dialogueLines);
        }
        else
        {
            Debug.Log("No Body");
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

    //Nearest body in range that isn't us and hasn't been used up already. Public for the interact prompt
    public BodyScript FindNearestBody()
    {
        if(currentBody == null) return null;
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


    //Swap into a specific body from outside (DaughterTransfer)
    public void SwapInto(BodyScript targetBody)
    {
        if(currentBody == null || targetBody == null || targetBody == currentBody) return;
        DoSwapBody(currentBody, targetBody);
    }

    void DoSwapBody(BodyScript currentBody, BodyScript targetBody)
    {
        SFXManager.Play("bodyswap");
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
