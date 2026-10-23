using System.Collections;
using TMPro;
using UnityEngine;

public class TypeWriterEffect : MonoBehaviour
{
    public float typingSpeed = 0.05f; // Speed of text appearing

    [Header("Optional Typing Sound")]
    [Tooltip("Played every few characters while typing. Leave empty for silent typing.")]
    public AudioClip typeSound;
    public AudioSource typeAudioSource;
    [Range(1, 8)] public int charactersPerSound = 2;

    private Coroutine typingCoroutine;
    private TMP_Text activeTextComponent; // Track current text component
    private WaitForSeconds typingWait;
    private float cachedSpeed = -1f;

    /// <summary>True while text is still being typed out.</summary>
    public bool IsTyping => typingCoroutine != null;

    /// <summary>
    /// Starts the typewriter effect and returns the Coroutine.
    /// </summary>
    public Coroutine DisplayText(TMP_Text textComponent, string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (textComponent == null || !isActiveAndEnabled) return null;

        activeTextComponent = textComponent;
        typingCoroutine = StartCoroutine(TypeText(textComponent, text ?? ""));

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
            activeTextComponent.maxVisibleCharacters = int.MaxValue;
            activeTextComponent = null;
        }
    }

    /// <summary>
    /// Instantly shows the rest of the text that is currently typing.
    /// </summary>
    public void CompleteTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (activeTextComponent != null) activeTextComponent.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>
    /// The private coroutine that actually types out the text.
    /// Reveals characters with maxVisibleCharacters, so rich text tags stay intact and no strings are allocated per letter.
    /// </summary>
    private IEnumerator TypeText(TMP_Text textComponent, string text)
    {
        if (typingWait == null || !Mathf.Approximately(cachedSpeed, typingSpeed))
        {
            cachedSpeed = typingSpeed;
            typingWait = new WaitForSeconds(typingSpeed);
        }

        textComponent.text = text;
        textComponent.maxVisibleCharacters = 0;
        textComponent.ForceMeshUpdate();
        int total = textComponent.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            textComponent.maxVisibleCharacters = i;
            if (typeSound != null && typeAudioSource != null && i % charactersPerSound == 0)
            {
                typeAudioSource.PlayOneShot(typeSound, 0.5f);
            }
            yield return typingWait;
        }

        textComponent.maxVisibleCharacters = int.MaxValue;
        typingCoroutine = null;
    }
}
