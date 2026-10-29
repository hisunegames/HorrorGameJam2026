using System.Collections;
using Febucci.UI;
using TMPro;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public int index = 0;
    private string[] dialogueLines;
    private bool isTyping = false;
    //Text Animator types the lines (with its appear effects)
    private TypewriterByCharacter typewriter;

    void Awake()
    {
        //The game's font (VCR OSD Mono) in the dialogue box too
        TMP_FontAsset font = GameFont.GetTMP();
        if(font != null && dialogueText != null) dialogueText.font = font;
        if(dialogueText != null) typewriter = GameFont.Animate(dialogueText, 0.05f);
    }

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
            if (typewriter != null)
            {
                //Still typing: show the whole line, otherwise on to the next
                if (typewriter.isShowingText) typewriter.SkipTypewriter();
                else NextLine();
                return;
            }
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
        if (typewriter != null)
        {
            typewriter.ShowText(line);
            yield break;
        }
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
