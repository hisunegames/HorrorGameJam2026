using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public string[] dialogueLines;
    public int index = 0;
    public bool isTyping = false;

    // void Start()
    // {
    //     StartDialogue(new string[] { "Welcome to the game!", "This is a sample dialogue.", "Press Space to continue." });
    // }

    void Update()
    {
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
        }
    }

    public void StartDialogue(string[] lines)
    {
        index = 0;
        dialogueLines = lines;
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
