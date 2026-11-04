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
    /// Stop cues fade out quickly. When a voice's last clip ends, <see cref="Finished"/> fires so the caller can tell the
    /// engine (<see cref="CommentaryEngine.NotifyFinished"/>). Missing or not-yet-loaded clips are skipped silently: the
    /// subtitles still show, and the engine skips lines whose clips are absent from the index.
    ///
    /// Memory: clips load compressed (decoded while playing). Every line clip is preloaded; player names only for the
    /// 22 players in the match, club names only for the two clubs.
    /// </summary>
    public sealed class CommentaryAudioPlayer : MonoBehaviour
    {
        private const double ScheduleLead = 0.03;
        private const float FadeSeconds = 0.12f;

        [SerializeField] private float _volume = 1f;

        private sealed class Voice
        {
            public readonly AudioSource[] Sources = new AudioSource[2];
            public int Next;
            public double EndDsp = -1;
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
        private readonly Queue<KeyValuePair<CommentaryVoice, string>> _loadQueue = new Queue<KeyValuePair<CommentaryVoice, string>>();
        private string _folder;
        private bool _loading;

        /// <summary>The rendered-clip index, or null when no audio is bundled (subtitles only).</summary>
        public CommentaryAudioIndex Index { get; private set; }

        /// <summary>A voice finished speaking (its last scheduled clip ended).</summary>
        public event Action<CommentaryVoice> Finished;

        /// <summary>0..1, high while someone is speaking: other audio (crowd beds) can duck under it.</summary>
        public float DuckAmount { get; private set; }

        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Clamp01(value);
        }

        public bool Muted { get; set; }

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

        /// <summary>Reads the index for a language. Returns false (subtitles only) when no audio is bundled.</summary>
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

        /// <summary>Queues every line clip plus this match's player and club names for loading.</summary>
        public void Preload(IEnumerable<string> playerNames, IEnumerable<string> teamNames)
        {
            if (Index == null) return;
            if (playerNames != null)
                foreach (var n in playerNames)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    foreach (CommentaryIntonation t in Enum.GetValues(typeof(CommentaryIntonation)))
                        Request(CommentaryVoice.PlayByPlay, CommentaryClip.NameClipId(n, t));
                }
            if (teamNames != null)
                foreach (var n in teamNames)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    foreach (CommentaryIntonation t in Enum.GetValues(typeof(CommentaryIntonation)))
                    {
                        Request(CommentaryVoice.PlayByPlay, CommentaryClip.TeamClipId(n, t));
                        Request(CommentaryVoice.Analyst, CommentaryClip.TeamClipId(n, t));
                    }
                }
            foreach (var voice in new[] { CommentaryVoice.PlayByPlay, CommentaryVoice.Analyst })
                foreach (var id in Index.ClipIds(voice))
                    if (id.StartsWith("line.", StringComparison.Ordinal)) Request(voice, id);
            if (!_loading) StartCoroutine(LoadLoop());
        }

        private void Request(CommentaryVoice voice, string clipId)
        {
            if (!Index.Has(voice, clipId) || !_requested[(int)voice].Add(clipId)) return;
            _loadQueue.Enqueue(new KeyValuePair<CommentaryVoice, string>(voice, clipId));
        }

        private IEnumerator LoadLoop()
        {
            _loading = true;
            while (_loadQueue.Count > 0)
            {
                var item = _loadQueue.Dequeue();
                string file = Path.Combine(_folder, CommentaryAudioIndex.RelativePath(item.Key, item.Value));
                using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, AudioType.OGGVORBIS))
                {
                    if (req.downloadHandler is DownloadHandlerAudioClip dh) dh.compressed = true;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(req);
                        clip.name = item.Value;
                        _clips[(int)item.Key][item.Value] = clip;
                    }
                }
            }
            _loading = false;
        }

        /// <summary>Acts on engine cues: Start schedules the utterance's clips, Stop fades the voice out.</summary>
        public void Handle(IReadOnlyList<CommentaryCue> cues)
        {
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
            if (Muted || Index == null) return;
            double t = AudioSettings.dspTime + ScheduleLead;
            bool any = false;
            foreach (var c in u.Clips)
            {
                if (!_clips[v].TryGetValue(c.ClipId, out var clip) || clip == null) continue;
                var src = voice.Sources[voice.Next];
                voice.Next = (voice.Next + 1) % voice.Sources.Length;
                src.clip = clip;
                src.volume = _volume;
                src.PlayScheduled(t);
                t += clip.length + 0.06;
                any = true;
            }
            voice.Active = any;
            voice.EndDsp = any ? t - 0.06 : -1;
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
                if (AudioSettings.dspTime >= voice.EndDsp)
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
