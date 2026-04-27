/*=============================================================================
 IntroSFXManager
 Description:   Audio for the title / menu scene. Owns:
                  - Two intro music tracks that alternate back-to-back so the
                    menu never repeats the same loop twice in a row (one is a
                    chiller variant of the other).
                  - Menu SFX (currently just tab change, room to grow).

 Each clip has its own volume slider in the Inspector so the mix can be
 tweaked without re-encoding wavs.

 Setup:         1. Empty GameObject in the menu scene (e.g. "IntroSFXManager")
                2. Attach this script
                3. Drag the two intro clips + tab change SFX into the slots
                The two AudioSources are added at runtime — no manual setup.
=============================================================================*/
using UnityEngine;

public class IntroSFXManager : MonoBehaviour
{
    public static IntroSFXManager Instance { get; private set; }

    [Header("Intro Music (alternates back-to-back)")]
    [SerializeField] private AudioClip _introTrack1;                    // primary / hype
    [SerializeField, Range(0f, 1f)] private float _introTrack1Volume = 0.6f;
    [SerializeField] private AudioClip _introTrack2;                    // chill variant
    [SerializeField, Range(0f, 1f)] private float _introTrack2Volume = 0.6f;

    [Header("Menu SFX")]
    [SerializeField] private AudioClip _sfxTabChange;
    [SerializeField, Range(0f, 1f)] private float _sfxTabChangeVolume = 1f;

    private AudioSource _musicSource;   // non-looping; we manually swap clips
    private AudioSource _sfxSource;     // PlayOneShot target so SFX layer cleanly
    private int _nextTrackIndex;        // 0 = play track1 next, 1 = play track2 next

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Music source — loop disabled because we want each track to play
        // through once and then hand off to the OTHER track, alternating.
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = false;

        // SFX source — separate AudioSource so PlayOneShot can layer on top
        // of the music without clipping or restarting it.
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
    }

    private void Update()
    {
        // Detect end-of-clip and roll into the next track. Cheap to poll — one
        // bool check per frame — and avoids needing AudioSource.PlayScheduled
        // which gets fiddly when the clip lengths don't match.
        if (_musicSource.clip != null && !_musicSource.isPlaying)
            PlayNextIntroTrack();
    }

    // -----------------------PUBLIC API-----------------------

    /// <summary>
    /// Kicks off the intro music. Safe to call multiple times — won't restart
    /// if a track is already playing. No-op if both clip slots are empty.
    /// </summary>
    public void PlayIntroMusic()
    {
        if (_musicSource.isPlaying) return;
        if (_introTrack1 == null && _introTrack2 == null) return;
        PlayNextIntroTrack();
    }

    public void StopIntroMusic() => _musicSource.Stop();

    public void PlayTabChange() => PlayClip(_sfxTabChange, _sfxTabChangeVolume);

    // -----------------------PRIVATE HELPERS-----------------------

    private void PlayNextIntroTrack()
    {
        // Pick the clip indicated by _nextTrackIndex, but fall back to the
        // other slot if it's empty so a half-configured Inspector still plays
        // something instead of going silent.
        AudioClip next;
        float volume;
        if (_nextTrackIndex == 0)
        {
            next   = _introTrack1 != null ? _introTrack1 : _introTrack2;
            volume = _introTrack1 != null ? _introTrack1Volume : _introTrack2Volume;
        }
        else
        {
            next   = _introTrack2 != null ? _introTrack2 : _introTrack1;
            volume = _introTrack2 != null ? _introTrack2Volume : _introTrack1Volume;
        }

        // Always toggle so the bookkeeping matches a true alternation, even if
        // we just played the fallback. When the empty slot gets filled later
        // it'll slot right in.
        _nextTrackIndex = 1 - _nextTrackIndex;

        if (next == null) return;

        _musicSource.clip   = next;
        _musicSource.volume = volume;
        _musicSource.Play();
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            Debug.LogWarning("IntroSFXManager: AudioClip is null — check Inspector assignments.");
            return;
        }
        _sfxSource.PlayOneShot(clip, volume);
    }
}
