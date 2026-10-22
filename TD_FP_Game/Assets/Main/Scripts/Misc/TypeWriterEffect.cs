using System.Collections;
using TMPro;
using UnityEngine;

public class TypeWriterEffect : MonoBehaviour
{
    public float typingSpeed = 0.05f;
    [Tooltip("Optional blip played per character.")]
    public AudioSource audioSource;
    public AudioClip typeSound;

    private Coroutine typingCoroutine;
    private TMP_Text activeTextComponent;

    public bool IsTyping => typingCoroutine != null;

    public void DisplayText(TMP_Text textComponent, string text)
    {
        if (textComponent == null) return;
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

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
            activeTextComponent.text = "";
            activeTextComponent.maxVisibleCharacters = int.MaxValue;
            activeTextComponent = null;
        }
    }

    /// <summary>Instantly reveals the whole message.</summary>
    public void Skip()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (activeTextComponent != null) activeTextComponent.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator TypeText(TMP_Text textComponent, string text)
    {
        // Set the full string once and reveal characters; no per-character string allocations.
        textComponent.text = text;
        textComponent.maxVisibleCharacters = 0;
        textComponent.ForceMeshUpdate();

        int total = textComponent.textInfo.characterCount;
        var wait = new WaitForSeconds(typingSpeed);
        for (int i = 1; i <= total; i++)
        {
            textComponent.maxVisibleCharacters = i;
            if (audioSource != null && typeSound != null && !char.IsWhiteSpace(text[Mathf.Min(i - 1, text.Length - 1)]))
            {
                audioSource.PlayOneShot(typeSound);
            }
            yield return wait;
        }
        textComponent.maxVisibleCharacters = int.MaxValue;
        typingCoroutine = null;
    }
}
