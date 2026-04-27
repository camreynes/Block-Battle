/*=============================================================================
 GameplaySFXManager
 Description:   Singleton audio manager for in-game sound effects (movement,
                rotation, hold, soft / hard drop, line clears). Each clip
                carries its own volume slider so the mix can be balanced
                without re-encoding wavs.

                Intentionally has NO music — adaptive gameplay music lives
                in AdaptiveMusic.cs, and the menu/intro audio lives in
                IntroSFXManager.cs. Keeping them separate means you can swap
                or mute either without touching the other.

 Setup:         1. GameObject in the gameplay scene (e.g. "GameplaySFXManager")
                2. Attach this script (an AudioSource is auto-required)
                3. Drag each clip into its slot and tune the per-clip volume
=============================================================================*/
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GameplaySFXManager : MonoBehaviour
{
    public static GameplaySFXManager Instance { get; private set; }

    [Header("Line Clear SFX")]
    [SerializeField] private AudioClip _sfxSingle;
    [SerializeField, Range(0f, 1f)] private float _sfxSingleVolume = 1f;

    [SerializeField] private AudioClip _sfxDouble;
    [SerializeField, Range(0f, 1f)] private float _sfxDoubleVolume = 1f;

    [SerializeField] private AudioClip _sfxTriple;
    [SerializeField, Range(0f, 1f)] private float _sfxTripleVolume = 1f;

    [SerializeField] private AudioClip _sfxTetris;
    [SerializeField, Range(0f, 1f)] private float _sfxTetrisVolume = 1f;

    [SerializeField] private AudioClip _sfxSpecial; // T-Spins
    [SerializeField, Range(0f, 1f)] private float _sfxSpecialVolume = 1f;

    [Header("Action SFX")]
    [SerializeField] private AudioClip _sfxMove;
    [SerializeField, Range(0f, 1f)] private float _sfxMoveVolume = 1f;

    [SerializeField] private AudioClip _sfxRotate;
    [SerializeField, Range(0f, 1f)] private float _sfxRotateVolume = 1f;

    [SerializeField] private AudioClip _sfxHold;
    [SerializeField, Range(0f, 1f)] private float _sfxHoldVolume = 1f;

    [SerializeField] private AudioClip _sfxSoftDrop;
    [SerializeField, Range(0f, 1f)] private float _sfxSoftDropVolume = 1f;

    [SerializeField] private AudioClip _sfxHardDrop;
    [SerializeField, Range(0f, 1f)] private float _sfxHardDropVolume = 1f;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    // -----------------------PUBLIC PLAY METHODS-----------------------

    public void PlayMove()      => PlayClip(_sfxMove,     _sfxMoveVolume);
    public void PlayRotate()    => PlayClip(_sfxRotate,   _sfxRotateVolume);
    public void PlayHold()      => PlayClip(_sfxHold,     _sfxHoldVolume);
    public void PlaySoftDrop()  => PlayClip(_sfxSoftDrop, _sfxSoftDropVolume);
    public void PlayHardDrop()  => PlayClip(_sfxHardDrop, _sfxHardDropVolume);
    public void PlaySingle()    => PlayClip(_sfxSingle,   _sfxSingleVolume);
    public void PlayDouble()    => PlayClip(_sfxDouble,   _sfxDoubleVolume);
    public void PlayTriple()    => PlayClip(_sfxTriple,   _sfxTripleVolume);
    public void PlayTetris()    => PlayClip(_sfxTetris,   _sfxTetrisVolume);
    public void PlaySpecial()   => PlayClip(_sfxSpecial,  _sfxSpecialVolume);

    /// <summary>
    /// Maps clearType strings produced by BlockGrid.CalculcateScore() (e.g.
    /// "SINGLE", "B2B TETRIS", "T-SPIN DOUBLE", "PERFECT B2B TETRIS") onto
    /// the right clip. "B2B " and "PERFECT " are scoring modifiers, not
    /// distinct clear types — they're stripped before switching.
    /// </summary>
    public void PlayClearType(string clearType)
    {
        if (string.IsNullOrEmpty(clearType)) return;

        // T-Spin clears (any variant) get the special SFX
        if (clearType.Contains("T-SPIN"))
        {
            PlaySpecial();
            return;
        }

        // Strip optional "PERFECT " and "B2B " prefixes so the switch below
        // stays clean. The order doesn't matter — both Replace calls are no-ops
        // if the substring isn't present.
        string baseType = clearType.Replace("PERFECT ", "").Replace("B2B ", "");

        switch (baseType)
        {
            case "SINGLE": PlaySingle(); break;
            case "DOUBLE": PlayDouble(); break;
            case "TRIPLE": PlayTriple(); break;
            case "TETRIS": PlayTetris(); break;
            default:
                Debug.LogWarning($"GameplaySFXManager: unrecognised clearType '{clearType}'");
                break;
        }
    }

    // -----------------------PRIVATE HELPERS-----------------------

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            Debug.LogWarning("GameplaySFXManager: AudioClip is null — check Inspector assignments.");
            return;
        }
        // PlayOneShot lets multiple SFX overlap (hard-drop + line clear, etc.)
        _audioSource.PlayOneShot(clip, volume);
    }
}
