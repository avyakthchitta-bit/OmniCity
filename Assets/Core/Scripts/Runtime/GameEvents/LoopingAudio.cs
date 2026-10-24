
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class LoopingAudio : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (audioClip != null)
        {
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("LoopingAudio: No audio clip assigned!", this);
        }
    }
}
