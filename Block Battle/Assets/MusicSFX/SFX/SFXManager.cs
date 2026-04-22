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

    [Header("Menu SFX")]
    [SerializeField] private AudioClip _sfxTabChange;

    [Header("Music")]
    // Looping intro/menu track. Lives on its own AudioSource (added at runtime)
    // so PlayOneShot SFX can layer on top without cutting the music off.
    [SerializeField] private AudioClip _introMusic;
    [SerializeField, Range(0f, 1f)] private float _introMusicVolume = 0.6f;

    private AudioSource _audioSource;
    private AudioSource _musicSource;

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

        // Dedicated music source - we need loop + steady volume while the SFX
        // source stays free to fire one-shots. Adding it programmatically keeps
        // the Inspector setup unchanged for anyone who already placed SFXManager.
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.volume = _introMusicVolume;
    }

    // -----------------------PUBLIC PLAY METHODS-----------------------

    public void PlayMove()      => PlayClip(_sfxMove);
    public void PlayRotate()    => PlayClip(_sfxRotate);
    public void PlayHold()      => PlayClip(_sfxHold);
    public void PlaySoftDrop()  => PlayClip(_sfxSoftDrop);
    public void PlayHardDrop()  => PlayClip(_sfxHardDrop);
    public void PlaySingle()    => PlayClip(_sfxSingle);
    public void PlayDouble()    => PlayClip(_sfxDouble);
    public void PlayTriple()    => PlayClip(_sfxTriple);
    public void PlayTetris()    => PlayClip(_sfxTetris);
    public void PlaySpecial()   => PlayClip(_sfxSpecial);
    public void PlayTabChange() => PlayClip(_sfxTabChange);

    /// <summary>
    /// Starts the menu/intro track on the dedicated music source. No-op if the
    /// clip slot is empty (so the menu can call this unconditionally even
    /// before Cam has dropped an audio file into the Inspector).
    /// </summary>
    public void PlayIntroMusic()
    {
        if (_introMusic == null) return;
        // Only (re)start if we're not already playing this exact clip - avoids
        // restarting the song if the scene calls PlayIntroMusic twice.
        if (_musicSource.isPlaying && _musicSource.clip == _introMusic) return;
        _musicSource.clip = _introMusic;
        _musicSource.volume = _introMusicVolume;
        _musicSource.Play();
    }

    public void StopIntroMusic() => _musicSource.Stop();

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
            Debug.LogWarning("SFXManager: AudioClip is null - check Inspector assignments.");
            return;
        }
        // PlayOneShot allows multiple SFX to overlap (e.g. hard-drop + line clear)
        _audioSource.PlayOneShot(clip);
    }
}
