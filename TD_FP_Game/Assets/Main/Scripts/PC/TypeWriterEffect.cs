using System.Collections;
using TMPro;
using UnityEngine;

public class TypeWriterEffect : MonoBehaviour
{
    public float typingSpeed = 0.05f; // Speed of text appearing
    private Coroutine typingCoroutine;
    private TMP_Text activeTextComponent; // Track current text component

    /// <summary>
    /// Starts the typewriter effect and returns the Coroutine.
    /// </summary>
    public Coroutine DisplayText(TMP_Text textComponent, string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        activeTextComponent = textComponent;
        typingCoroutine = StartCoroutine(TypeText(textComponent, text));
        
        // This is the critical change:
        return typingCoroutine;
    }

    /// <summary>
    /// Stops any active typing coroutine and clears the text.
    /// </summary>
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

    /// <summary>
    /// The private coroutine that actually types out the text.
    /// </summary>
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