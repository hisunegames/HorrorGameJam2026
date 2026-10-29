using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public int index = 0;
    private string[] dialogueLines;
    private bool isTyping = false;

    // void Start()
    // {
    //     StartDialogue(new string[] { "Welcome to the game!", "This is a sample dialogue.", "Press Space to continue." });
    // }

    void Update()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
            return;
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                isTyping = false; // Stop typing if interrupted
            }
            else
            {
                NextLine();
            }
        }
    }

    private void NextLine()
    {
        index++;
        if (index < dialogueLines.Length)
        {
            StartCoroutine(DisplayLine(dialogueLines[index]));
        }
        else
        {
            dialogueText.text = string.Empty;
            index = 0;
            dialogueLines = null;
            dialoguePanel.SetActive(false);
        }
    }

    public void StartDialogue(string[] lines)
    {
        //A new conversation cuts off one that's still typing (otherwise both type into the box at once)
        StopAllCoroutines();
        isTyping = false;
        index = 0;
        dialogueLines = lines;
        dialoguePanel.SetActive(true);
        dialogueText.text = string.Empty;
        StartCoroutine(DisplayLine(dialogueLines[index]));
    }

    private IEnumerator DisplayLine(string line)
    {
        isTyping = true;
        dialogueText.text = string.Empty;
        foreach (char letter in line)
        {
            if (!isTyping)
            {
                dialogueText.text = line;
                yield break;
            }
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.05f);
        }
        isTyping = false;
    }
}
