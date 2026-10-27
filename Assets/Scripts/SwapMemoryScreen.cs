using System.Collections;
using System.Text;
using UnityEngine;

//On each body swap: black screen, reveal one more letter of the daughter's name and a memory line.
//The last letter is held back for the accept ending (RevealFinalLetter)
public class SwapMemoryScreen : MonoBehaviour
{
    public BlackScreenUI blackScreen;
    public BodyManager bodyManager;

    [Header("Name")]
    public string daughterName = "NADIA";
    private int revealedCount = 0;

    [Header("Memories")]
    [TextArea] public string[] memoryLines;
    [TextArea] public string recurringLine = "";
    public float holdTime = 3f;

    void Awake()
    {
        if(blackScreen == null) blackScreen = GetComponent<BlackScreenUI>();
        if(bodyManager == null) bodyManager = FindAnyObjectByType<BodyManager>();
    }

    void OnEnable()
    {
        if(bodyManager != null) bodyManager.OnBodySwapped += HandleSwap;
    }

    void OnDisable()
    {
        if(bodyManager != null) bodyManager.OnBodySwapped -= HandleSwap;
    }

    void HandleSwap(BodyScript oldBody, BodyScript newBody, int swapCount)
    {
        if(revealedCount < daughterName.Length - 1)
        {
            revealedCount++;
        }
        string memory = "";
        if(memoryLines != null && memoryLines.Length > 0)
        {
            memory = memoryLines[Mathf.Min(swapCount - 1, memoryLines.Length - 1)];
        }
        StartCoroutine(ShowScreen(memory));
    }

    public void RevealFinalLetter()
    {
        revealedCount = daughterName.Length;
    }

    public bool IsNameComplete => revealedCount >= daughterName.Length;

    //"N A _ _ _"
    public string GetMaskedName()
    {
        StringBuilder sb = new StringBuilder();
        for(int i = 0; i < daughterName.Length; i++)
        {
            if(i > 0) sb.Append(' ');
            sb.Append(i < revealedCount ? char.ToUpper(daughterName[i]) : '_');
        }
        return sb.ToString();
    }

    IEnumerator ShowScreen(string memory)
    {
        Time.timeScale = 0f;
        string body = memory;
        if(!string.IsNullOrEmpty(recurringLine))
        {
            body += "\n\n<i>" + recurringLine + "</i>";
        }
        blackScreen.SetText(GetMaskedName(), body);
        yield return blackScreen.FadeIn();
        yield return new WaitForSecondsRealtime(holdTime);
        yield return blackScreen.FadeOut();
        Time.timeScale = 1f;
    }
}
