using UnityEngine;

/// <summary>
/// Singleton audio manager for all game sound effects.
/// Attach to a scene GameObject with an AudioSource component.
/// Assign each clip in the Inspector from Assets/MusicSFX/SFX/.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Line Clear SFX")]
    [SerializeField] private AudioClip _sfxSingle;
    [SerializeField] private AudioClip _sfxDouble;
    [SerializeField] private AudioClip _sfxTriple;
    [SerializeField] private AudioClip _sfxTetris;
    [SerializeField] private AudioClip _sfxSpecial; // T-Spins

    [Header("Action SFX")]
    [SerializeField] private AudioClip _sfxMove;
    [SerializeField] private AudioClip _sfxRotate;
    [SerializeField] private AudioClip _sfxHold;
    [SerializeField] private AudioClip _sfxSoftDrop;
    [SerializeField] private AudioClip _sfxHardDrop;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    // -----------------------PUBLIC PLAY METHODS-----------------------

    public void PlayMove()     => PlayClip(_sfxMove);
    public void PlayRotate()   => PlayClip(_sfxRotate);
    public void PlayHold()     => PlayClip(_sfxHold);
    public void PlaySoftDrop() => PlayClip(_sfxSoftDrop);
    public void PlayHardDrop() => PlayClip(_sfxHardDrop);
    public void PlaySingle()   => PlayClip(_sfxSingle);
    public void PlayDouble()   => PlayClip(_sfxDouble);
    public void PlayTriple()   => PlayClip(_sfxTriple);
    public void PlayTetris()   => PlayClip(_sfxTetris);
    public void PlaySpecial()  => PlayClip(_sfxSpecial);

    /// <summary>
    /// Plays the appropriate line-clear SFX based on the clearType string
    /// produced by BlockGrid.CalculcateScore() (e.g. "SINGLE", "B2B TETRIS", "T-SPIN DOUBLE").
    /// </summary>
    public void PlayClearType(string clearType)
    {
        if (string.IsNullOrEmpty(clearType))
            return;

        // T-Spin clears (any variant) get the special SFX
        if (clearType.Contains("T-SPIN"))
        {
            PlaySpecial();
            return;
        }

        // Strip optional "B2B " prefix so the switch below stays clean
        string baseType = clearType.Replace("B2B ", "");

        switch (baseType)
        {
            case "SINGLE": PlaySingle(); break;
            case "DOUBLE": PlayDouble(); break;
            case "TRIPLE": PlayTriple(); break;
            case "TETRIS": PlayTetris(); break;
            default:
                Debug.LogWarning($"SFXManager: unrecognised clearType '{clearType}'");
                break;
        }
    }

    // -----------------------PRIVATE HELPERS-----------------------

    private void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("SFXManager: AudioClip is null — check Inspector assignments.");
            return;
        }
        // PlayOneShot allows multiple SFX to overlap (e.g. hard-drop + line clear)
        _audioSource.PlayOneShot(clip);
    }
}
