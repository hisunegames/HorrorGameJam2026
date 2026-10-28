using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public int index;

    void Start()
    {
        StartDialogue(new[] { "Hello there!", "Welcome to our game.", "Enjoy your adventure!" });
    }

    public void StartDialogue(string[] lines)
    {
        index = 0;
        Debug.Log(lines[index]);
        StartCoroutine(DisplayLine(lines[index]));
    }

    private IEnumerator DisplayLine(string lines)
    {
        dialogueText.text = string.Empty;
        foreach (char letter in lines)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.05f);
        }
    }
}
