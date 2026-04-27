/*=============================================================================
 AdaptiveMusic
 Description:   Drives the gameplay music. Plays one of four intensity tracks
                that escalate with the player's level, swapping every N levels
                (default 5). Crossfades between two AudioSources so the
                transition is seamless, and preserves the playback timestamp
                across switches so tracks 1-4 feel like layered stems of the
                same song. All tracks loop while active.

 Level mapping (with levelsPerIntensity = 5):
   levels  1- 5  -> intensity 1  (chillest)
   levels  6-10  -> intensity 2
   levels 11-15  -> intensity 3
   levels 16-20  -> intensity 4  (most intense)

 Setup:         1. Empty GameObject in the gameplay scene (e.g. "MusicManager")
                2. Attach this script
                3. Drag the four intensity clips into the slots and tune
                   per-clip volume.
                4. Optionally drag a BlockGrid into the Level Source slot —
                   if you leave it empty, this script will FindObjectOfType
                   on first Update so it picks up the grid that
                   InitializeGrids spawns at runtime.

 Note:          Two-way switching — if level ever drops back below a threshold
                (multiplayer reset, restart, etc.) the music falls with it.
=============================================================================*/
using UnityEngine;
using System.Collections;

public class AdaptiveMusic : MonoBehaviour
{
    [Header("Intensity Tracks (1 = chillest, 4 = most intense)")]
    [SerializeField] private AudioClip _track1;
    [SerializeField, Range(0f, 1f)] private float _track1Volume = 1f;
    [SerializeField] private AudioClip _track2;
    [SerializeField, Range(0f, 1f)] private float _track2Volume = 1f;
    [SerializeField] private AudioClip _track3;
    [SerializeField, Range(0f, 1f)] private float _track3Volume = 1f;
    [SerializeField] private AudioClip _track4;
    [SerializeField, Range(0f, 1f)] private float _track4Volume = 1f;

    [Header("Level Source")]
    [Tooltip("Drag the player's BlockGrid here. If empty we'll find one at runtime " +
             "via FindObjectOfType (works fine for singleplayer; revisit for multi).")]
    [SerializeField] private BlockGrid _grid;

    [Header("Behaviour")]
    [Tooltip("How many in-game levels each intensity covers (5 = swap every 5 levels).")]
    [SerializeField, Min(1)] private int _levelsPerIntensity = 5;
    [Tooltip("Seconds to crossfade between intensities.")]
    [SerializeField, Min(0f)] private float _crossfadeDuration = 1f;

    [Header("Audio Sources (auto-created if empty)")]
    [SerializeField] private AudioSource _sourceA;
    [SerializeField] private AudioSource _sourceB;

    private bool _isSwitching;
    private bool _sourceAPlaying;        // tracks which AudioSource currently holds the live track
    private int  _currentIntensity = -1; // -1 means "not started yet"

    private void Awake()
    {
        // Auto-provision the two AudioSources so the Inspector setup stays
        // simple — drop the script on a GameObject and you're done.
        if (_sourceA == null) _sourceA = gameObject.AddComponent<AudioSource>();
        if (_sourceB == null) _sourceB = gameObject.AddComponent<AudioSource>();

        _sourceA.playOnAwake = false; _sourceA.loop = true;
        _sourceB.playOnAwake = false; _sourceB.loop = true;
    }

    private void Update()
    {
        // Lazy grid lookup — InitializeGrids spawns the BlockGrid at runtime
        // so it might not exist yet on Start(). Trying again each frame until
        // we find one is cheap and avoids a coupling between init order.
        if (_grid == null)
        {
            _grid = FindObjectOfType<BlockGrid>();
            if (_grid == null) return;
        }

        // First time we have a level → kick off track 1 (or whichever the
        // current level maps to, in case the grid restarted at a higher level).
        if (_currentIntensity < 0)
        {
            int initial = ComputeIntensity(_grid.GetLevel());
            StartFirstTrack(initial);
            return;
        }

        if (_isSwitching) return;

        int target = ComputeIntensity(_grid.GetLevel());
        if (target != _currentIntensity)
            StartCoroutine(SwitchTrack(target));
    }

    // -----------------------HELPERS-----------------------

    private int ComputeIntensity(int level)
    {
        // 1-based: level 1..N → intensity 1, level N+1..2N → intensity 2, etc.
        int idx = ((Mathf.Max(level, 1) - 1) / Mathf.Max(_levelsPerIntensity, 1)) + 1;
        return Mathf.Clamp(idx, 1, 4);
    }

    private (AudioClip clip, float volume) GetClipAndVolume(int intensity)
    {
        switch (intensity)
        {
            case 2: return (_track2, _track2Volume);
            case 3: return (_track3, _track3Volume);
            case 4: return (_track4, _track4Volume);
            default: return (_track1, _track1Volume); // intensity 1 / fallback
        }
    }

    private void StartFirstTrack(int intensity)
    {
        var (clip, vol) = GetClipAndVolume(intensity);
        if (clip == null) return;

        _sourceA.clip   = clip;
        _sourceA.volume = vol;
        _sourceA.time   = 0f;
        _sourceA.Play();
        _sourceAPlaying = true;
        _currentIntensity = intensity;
    }

    private IEnumerator SwitchTrack(int newIntensity)
    {
        _isSwitching = true;

        var (newClip, newVol) = GetClipAndVolume(newIntensity);
        if (newClip == null)
        {
            // Track slot is empty — bail without changing anything so we keep
            // the current track playing rather than going silent.
            _isSwitching = false;
            yield break;
        }

        // Use the idle source for the incoming track so we can ramp it in
        // without interrupting the outgoing one.
        AudioSource fromSrc = _sourceAPlaying ? _sourceA : _sourceB;
        AudioSource toSrc   = _sourceAPlaying ? _sourceB : _sourceA;

        // Preserve timestamp — this is what makes intensity 1→2→3→4 feel
        // like the same song with more layers, not four different songs.
        // % length keeps us in bounds if the new clip is shorter.
        toSrc.clip   = newClip;
        toSrc.time   = fromSrc.time % newClip.length;
        toSrc.volume = 0f;
        toSrc.Play();

        float fromStartVol = fromSrc.volume;
        float t = 0f;
        while (t < _crossfadeDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / _crossfadeDuration);
            fromSrc.volume = Mathf.Lerp(fromStartVol, 0f, n);
            toSrc.volume   = Mathf.Lerp(0f, newVol, n);
            yield return null;
        }

        // Finalize: stop the outgoing source, lock the incoming source's
        // volume to its configured level, flip the bookkeeping bit.
        fromSrc.Stop();
        fromSrc.volume = fromStartVol; // reset for next swap
        toSrc.volume   = newVol;

        _sourceAPlaying = !_sourceAPlaying;
        _currentIntensity = newIntensity;
        _isSwitching = false;
    }
}
