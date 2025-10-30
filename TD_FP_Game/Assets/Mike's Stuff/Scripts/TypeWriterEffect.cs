using System.Collections;
using TMPro;
using UnityEngine;

public class TypeWriterEffect : MonoBehaviour
{
    public float typingSpeed = 0.05f; // Speed of text appearing
    private Coroutine typingCoroutine;
    private TMP_Text activeTextComponent; // Track current text component

    public void DisplayText(TMP_Text textComponent, string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        activeTextComponent = textComponent;
        typingCoroutine = StartCoroutine(TypeText(textComponent, text));
    }

    public void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (activeTextComponent != null)
        {
            activeTextComponent.text = ""; // Clear the text instantly
            activeTextComponent = null;
        }
    }

    private IEnumerator TypeText(TMP_Text textComponent, string text)
    {
        textComponent.text = "";
        foreach (char letter in text.ToCharArray())
        {
            textComponent.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
    }
}