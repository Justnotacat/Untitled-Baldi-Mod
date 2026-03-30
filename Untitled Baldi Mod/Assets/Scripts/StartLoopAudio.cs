using System.Collections;
using UnityEngine;

/// <summary>
/// Plays an optional intro clip once, then loops a second clip indefinitely.
/// Uses PlayScheduled to eliminate the buffer between the two clips.
/// Call Play() to start and Stop() to stop at any time.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class StartLoopAudio : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("Played once at the beginning before the loop starts. Leave empty to go straight to the loop.")]
    public AudioClip startClip;

    [Tooltip("Looped indefinitely after the start clip finishes (or immediately if no start clip is assigned).")]
    public AudioClip loopClip;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private AudioSource _startSource;   // Plays the intro clip
    private AudioSource _loopSource;    // Plays the looping clip
    private Coroutine _playbackCoroutine;
    private bool _isPlaying;

    // Small DSP look-ahead (seconds) so the audio thread has time to prepare.
    private const double ScheduleLeadTime = 0.1;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // The RequireComponent AudioSource is used as the start source.
        _startSource = GetComponent<AudioSource>();
        _startSource.playOnAwake = false;
        _startSource.loop = false;

        // A second AudioSource on the same GameObject handles the loop.
        _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;

        // Mirror mixer output so both sources route the same way.
        _loopSource.outputAudioMixerGroup = _startSource.outputAudioMixerGroup;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Starts playback from the beginning.
    /// Safe to call while already playing — restarts cleanly.
    /// </summary>
    public void Play()
    {
        if (_playbackCoroutine != null)
            StopCoroutine(_playbackCoroutine);

        _startSource.Stop();
        _loopSource.Stop();

        _isPlaying = true;
        _playbackCoroutine = StartCoroutine(PlaybackRoutine());
    }

    /// <summary>
    /// Stops all playback immediately.
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;

        if (_playbackCoroutine != null)
        {
            StopCoroutine(_playbackCoroutine);
            _playbackCoroutine = null;
        }

        _startSource.Stop();
        _loopSource.Stop();
    }

    /// <summary>
    /// Returns true while audio is actively playing.
    /// </summary>
    public bool IsPlaying => _isPlaying;

    // -------------------------------------------------------------------------
    // Internal playback logic
    // -------------------------------------------------------------------------

    private IEnumerator PlaybackRoutine()
    {
        if (startClip != null && loopClip != null)
        {
            // Schedule both clips back-to-back on the DSP timeline.
            // This hands the transition off to the audio thread, eliminating
            // any gap that coroutine timing or frame rate would otherwise cause.
            double startTime = AudioSettings.dspTime + ScheduleLeadTime;
            double loopTime = startTime + startClip.length;

            _startSource.clip = startClip;
            _startSource.loop = false;
            _startSource.PlayScheduled(startTime);

            _loopSource.clip = loopClip;
            _loopSource.loop = true;
            _loopSource.PlayScheduled(loopTime);

            // Wait on the coroutine side until the start clip is done.
            // We wait slightly less than the full length so we don't overshoot.
            yield return new WaitForSeconds((float)(loopTime - AudioSettings.dspTime - 0.05));

            if (!_isPlaying)
            {
                _loopSource.Stop();
                yield break;
            }

            // Start clip is finished — loop source is already running seamlessly.
        }
        else if (startClip != null)
        {
            // No loop clip: just play the start clip once.
            _startSource.clip = startClip;
            _startSource.loop = false;
            _startSource.Play();

            yield return new WaitUntil(() => !_startSource.isPlaying);
            _isPlaying = false;
        }
        else if (loopClip != null)
        {
            // No start clip: go straight to looping.
            _loopSource.clip = loopClip;
            _loopSource.loop = true;
            _loopSource.Play();
        }
    }
}