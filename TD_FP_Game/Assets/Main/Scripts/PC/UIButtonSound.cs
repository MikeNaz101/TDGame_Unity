using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Attach this script to any UI Button to make it play a sound on click.
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
        button = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.clip = clickSound;
        button.onClick.AddListener(PlaySound);
    }

    void OnDestroy()
    {
        button.onClick.RemoveListener(PlaySound);
    }
    public void PlaySound()
    {
        if (audioSource.clip != null)
        {
            audioSource.PlayOneShot(audioSource.clip);
        }
    }
}