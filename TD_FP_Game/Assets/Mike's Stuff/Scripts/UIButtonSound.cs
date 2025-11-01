using UnityEngine;
using UnityEngine.UI; // Required for Button
using System.Collections;

/// <summary>
/// Attach this script to any UI Button to make it play a sound on click.
/// It will automatically find the Button and AudioSource components.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(AudioSource))]
public class UIButtonSound : MonoBehaviour
{
    [Tooltip("The annoying sound you want to play.")]
    public AudioClip clickSound;

    private Button button;
    private AudioSource audioSource;

    void Start()
    {
        // 1. Get the components on this GameObject
        button = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();

        // 2. Configure the AudioSource
        // We don't want the sound playing on awake, only on click
        audioSource.playOnAwake = false;
        // Make sure the sound is assigned
        audioSource.clip = clickSound;

        // 3. Add a "listener" to the button's onClick event.
        // This tells the button: "When you are clicked, call my PlaySound method."
        button.onClick.AddListener(PlaySound);
    }

    void OnDestroy()
    {
        // Clean up the listener when the object is destroyed
        button.onClick.RemoveListener(PlaySound);
    }

    /// <summary>
    /// This is the method the button will call.
    /// </summary>
    public void PlaySound()
    {
        if (audioSource.clip != null)
        {
            // Play the sound one time
            audioSource.PlayOneShot(audioSource.clip);
        }
    }
}