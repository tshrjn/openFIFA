using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Speaks <see cref="CommentaryEngine"/> cues with the pre-rendered clips in
    /// StreamingAssets/Commentary/audio/&lt;lang&gt;/ (see tools/commentary/render_tts.py). Two voices, each with two
    /// AudioSources so a name clip and its line clip are scheduled back to back on the DSP clock (no gap or click).
    /// Stop cues fade out quickly. Missing or not-yet-loaded clips are skipped silently: the subtitles still show.
    ///
    /// Timing never feeds back into the engine (it times lines from the index), so the commentary a seed produces is
    /// the same with or without an audio device. <see cref="Finished"/> is informational.
    ///
    /// Memory: clips load compressed (decoded while playing), several at a time, the most needed first: this match's
    /// names and clubs, then kick-off, goal, shot and name-call lines, then the rest. Name and club clips are unloaded
    /// per match. When <see cref="Enabled"/> is false (muted, batch mode, capture, no audio output) nothing is loaded
    /// or played.
    /// </summary>
    public sealed class CommentaryAudioPlayer : MonoBehaviour
    {
        private const double ScheduleLead = 0.03;
        private const double ClipGap = 0.06;
        private const float FadeSeconds = 0.12f;
        private const int ParallelLoads = 8;

        [SerializeField] private float _volume = 1f;

        private sealed class Voice
        {
            public readonly AudioSource[] Sources = new AudioSource[2];
            public int Next;
            /// <summary>End on the real-time clock (works even when the DSP clock is not running).</summary>
            public double EndReal = -1;
            public float Fade = -1f;
            public bool Active;
        }

        private readonly Voice[] _voices = { new Voice(), new Voice() };
        private readonly Dictionary<string, AudioClip>[] _clips =
        {
            new Dictionary<string, AudioClip>(StringComparer.Ordinal),
            new Dictionary<string, AudioClip>(StringComparer.Ordinal)
        };
        private readonly HashSet<string>[] _requested =
        {
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal)
        };
        private readonly List<KeyValuePair<CommentaryVoice, string>> _urgent = new List<KeyValuePair<CommentaryVoice, string>>();
        private readonly Queue<KeyValuePair<CommentaryVoice, string>> _loadQueue = new Queue<KeyValuePair<CommentaryVoice, string>>();
        private int _inFlight;
        private string _folder;
        private bool _enabled = true;

        /// <summary>The rendered-clip index (the engine's catalog), or null when no audio is bundled.</summary>
        public CommentaryAudioIndex Index { get; private set; }

        /// <summary>A voice's last scheduled clip ended (informational; not fed back to the engine).</summary>
        public event Action<CommentaryVoice> Finished;

        /// <summary>0..1, high while someone is speaking: other audio (crowd beds) can duck under it.</summary>
        public float DuckAmount { get; private set; }

        /// <summary>Clips loaded so far (both voices).</summary>
        public int LoadedCount => _clips[0].Count + _clips[1].Count;

        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Clamp01(value);
        }

        /// <summary>
        /// False: load and play nothing (the index still serves as the engine's catalog). Turning it off stops speech.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                if (!value) StopAll();
            }
        }

        /// <summary>True when Unity has an audio output to play to (false in batch mode or without a device).</summary>
        public static bool AudioOutputAvailable()
        {
            if (Application.isBatchMode) return false;
            try { return AudioSettings.outputSampleRate > 0; }
            catch (Exception) { return false; }
        }

        private void Awake()
        {
            for (int v = 0; v < 2; v++)
                for (int i = 0; i < 2; i++)
                {
                    var s = gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.spatialBlend = 0f;
                    s.priority = 8; // above crowd and effects
                    s.bypassReverbZones = true;
                    _voices[v].Sources[i] = s;
                }
            _volume = PlayerPrefs.GetFloat("commentary.volume", _volume);
        }

        /// <summary>
        /// Reads the index for a language (a plain file read, done even when disabled so the engine's choices do not
        /// depend on audio). Returns false (subtitles only) when no audio is bundled.
        /// </summary>
        public bool LoadIndex(string language = "en")
        {
            _folder = Path.Combine(Application.streamingAssetsPath, "Commentary", "audio", language);
            string path = Path.Combine(_folder, "index.json");
            try
            {
                // StreamingAssets is a plain folder on desktop and iOS (Android would need UnityWebRequest here).
                if (!File.Exists(path)) return false;
                Index = CommentaryAudioIndex.Parse(File.ReadAllText(path), out string error);
                if (Index == null) Debug.LogWarning("[Commentary] audio index rejected: " + error);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Commentary] audio unavailable: " + e.Message);
                Index = null;
            }
            return Index != null;
        }

        /// <summary>
        /// Queues this match's player and club names first, then the line clips (kick-off, goals, shots and name calls
        /// ahead of the rest). Line clips stay loaded across matches. No-op when disabled or without an index.
        /// </summary>
        public void Preload(IEnumerable<string> playerNames, IEnumerable<string> teamNames)
        {
            if (Index == null || !_enabled) return;
            if (playerNames != null)
                foreach (var n in playerNames)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    foreach (CommentaryIntonation t in Enum.GetValues(typeof(CommentaryIntonation)))
                        Request(CommentaryVoice.PlayByPlay, CommentaryClip.NameClipId(n, t), true);
                }
            if (teamNames != null)
                foreach (var n in teamNames)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    foreach (CommentaryIntonation t in Enum.GetValues(typeof(CommentaryIntonation)))
                    {
                        Request(CommentaryVoice.PlayByPlay, CommentaryClip.TeamClipId(n, t), true);
                        Request(CommentaryVoice.Analyst, CommentaryClip.TeamClipId(n, t), true);
                    }
                }
            foreach (var voice in new[] { CommentaryVoice.PlayByPlay, CommentaryVoice.Analyst })
                foreach (var id in Index.ClipIds(voice))
                    if (id.StartsWith("line.", StringComparison.Ordinal)) Request(voice, id, IsEarlyLine(id));
            Pump();
        }

        /// <summary>Lines needed in the first seconds of a match or at its biggest moments.</summary>
        private static bool IsEarlyLine(string id) =>
            id.StartsWith("line.kickoff", StringComparison.Ordinal) || id.StartsWith("line.goal.", StringComparison.Ordinal) ||
            id.StartsWith("line.shot.", StringComparison.Ordinal) || id.StartsWith("line.on_the_ball", StringComparison.Ordinal);

        private void Request(CommentaryVoice voice, string clipId, bool urgent)
        {
            if (!Index.Has(voice, clipId) || !_requested[(int)voice].Add(clipId)) return;
            var item = new KeyValuePair<CommentaryVoice, string>(voice, clipId);
            if (urgent) _urgent.Add(item);
            else _loadQueue.Enqueue(item);
        }

        private void Pump()
        {
            while (_inFlight < ParallelLoads && (_urgent.Count > 0 || _loadQueue.Count > 0))
            {
                KeyValuePair<CommentaryVoice, string> item;
                if (_urgent.Count > 0)
                {
                    item = _urgent[0];
                    _urgent.RemoveAt(0);
                }
                else item = _loadQueue.Dequeue();
                _inFlight++;
                StartCoroutine(LoadOne(item.Key, item.Value));
            }
        }

        private IEnumerator LoadOne(CommentaryVoice voice, string clipId)
        {
            string file = Path.Combine(_folder, CommentaryAudioIndex.RelativePath(voice, clipId));
            UnityWebRequest req = null;
            try
            {
                req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, AudioType.OGGVORBIS);
                if (req.downloadHandler is DownloadHandlerAudioClip dh) dh.compressed = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Commentary] cannot load " + clipId + ": " + e.Message);
                req?.Dispose();
                req = null;
            }
            if (req != null)
            {
                yield return req.SendWebRequest();
                try
                {
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(req); // null without a working audio device
                        if (clip != null)
                        {
                            clip.name = clipId;
                            if (_requested[(int)voice].Contains(clipId)) _clips[(int)voice][clipId] = clip;
                            else Destroy(clip); // unloaded while in flight
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Commentary] cannot decode " + clipId + ": " + e.Message);
                }
                finally
                {
                    req.Dispose();
                }
            }
            _inFlight--;
            Pump();
        }

        /// <summary>Drops this match's player and club name clips (call when a match ends). Line clips stay loaded.</summary>
        public void UnloadMatchClips()
        {
            for (int v = 0; v < 2; v++)
            {
                var drop = new List<string>();
                foreach (var id in _requested[v])
                    if (!id.StartsWith("line.", StringComparison.Ordinal)) drop.Add(id);
                foreach (var id in drop)
                {
                    _requested[v].Remove(id);
                    if (_clips[v].TryGetValue(id, out var clip))
                    {
                        _clips[v].Remove(id);
                        if (clip != null) Destroy(clip);
                    }
                }
            }
            _urgent.RemoveAll(i => !i.Value.StartsWith("line.", StringComparison.Ordinal));
        }

        /// <summary>Acts on engine cues: Start schedules the utterance's clips, Stop fades the voice out.</summary>
        public void Handle(IReadOnlyList<CommentaryCue> cues)
        {
            if (!_enabled) return;
            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (cue.Utterance == null) continue;
                if (cue.Kind == CommentaryCueKind.Start) Play(cue.Utterance);
                else StopVoice(cue.Utterance.Voice, true);
            }
        }

        private void Play(CommentaryUtterance u)
        {
            int v = (int)u.Voice;
            var voice = _voices[v];
            StopVoice(u.Voice, false);
            if (Index == null) return;
            double dsp0 = AudioSettings.dspTime + ScheduleLead;
            double t = dsp0;
            bool any = false;
            foreach (var c in u.Clips)
            {
                if (!_clips[v].TryGetValue(c.ClipId, out var clip) || clip == null) continue;
                var src = voice.Sources[voice.Next];
                voice.Next = (voice.Next + 1) % voice.Sources.Length;
                src.clip = clip;
                src.volume = _volume;
                src.PlayScheduled(t);
                t += clip.length + ClipGap;
                any = true;
            }
            voice.Active = any;
            voice.EndReal = any ? Time.unscaledTimeAsDouble + (t - ClipGap - dsp0) + ScheduleLead : -1;
            voice.Fade = -1f;
        }

        private void StopVoice(CommentaryVoice which, bool fade)
        {
            var voice = _voices[(int)which];
            if (!voice.Active) return;
            if (fade) voice.Fade = FadeSeconds;
            else
            {
                foreach (var s in voice.Sources) s.Stop();
                voice.Active = false;
                voice.Fade = -1f;
            }
        }

        /// <summary>Stops both voices at once (pause menu, end of match).</summary>
        public void StopAll()
        {
            StopVoice(CommentaryVoice.PlayByPlay, false);
            StopVoice(CommentaryVoice.Analyst, false);
        }

        private void Update()
        {
            bool speaking = false;
            double now = Time.unscaledTimeAsDouble;
            for (int v = 0; v < _voices.Length; v++)
            {
                var voice = _voices[v];
                if (!voice.Active) continue;
                if (voice.Fade >= 0f)
                {
                    voice.Fade -= Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(voice.Fade / FadeSeconds);
                    foreach (var s in voice.Sources) s.volume = _volume * k;
                    if (voice.Fade <= 0f)
                    {
                        foreach (var s in voice.Sources) s.Stop();
                        voice.Active = false;
                    }
                    continue;
                }
                if (now >= voice.EndReal)
                {
                    voice.Active = false;
                    Finished?.Invoke((CommentaryVoice)v);
                    continue;
                }
                speaking = true;
            }
            DuckAmount = Mathf.MoveTowards(DuckAmount, speaking ? 1f : 0f, Time.unscaledDeltaTime * (speaking ? 8f : 2f));
        }

        private void OnDestroy()
        {
            foreach (var d in _clips)
                foreach (var c in d.Values)
                    if (c != null) Destroy(c);
        }
    }
}
