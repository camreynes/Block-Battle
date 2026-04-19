using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class LoopingMusic : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        _audioSource.clip = musicClip;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (musicClip != null)
        {
            _audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No music clip assigned to LoopingMusic.");
        }
    }
}
