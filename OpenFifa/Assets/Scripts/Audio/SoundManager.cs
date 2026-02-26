using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Audio
{
    /// <summary>
    /// Singleton sound manager that centralizes audio playback.
    /// Plays whistle, kick, crowd ambient, and goal celebration sounds.
    /// Uses placeholder AudioClips (generated tones) until real assets.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;

        [SerializeField] private AudioClip _whistleClip;
        [SerializeField] private AudioClip _kickClip;
        [SerializeField] private AudioClip _crowdAmbientClip;
        [SerializeField] private AudioClip _goalCheerClip;

        private AudioSource _sfxSource;
        private AudioSource _ambientSource;
        private SoundEventMapper _mapper;

        /// <summary>Singleton instance.</summary>
        public static SoundManager Instance => _instance;

        /// <summary>Whistle clip reference (for test verification).</summary>
        public AudioClip WhistleClip => _whistleClip;

        /// <summary>Kick clip reference (for test verification).</summary>
        public AudioClip KickClip => _kickClip;

        /// <summary>Crowd ambient clip reference.</summary>
        public AudioClip CrowdAmbientClip => _crowdAmbientClip;

        /// <summary>Goal cheer clip reference.</summary>
        public AudioClip GoalCheerClip => _goalCheerClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _mapper = new SoundEventMapper();

            // Create audio sources
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.playOnAwake = false;
            _ambientSource.loop = true;

            // Generate placeholder clips if not assigned
            GeneratePlaceholderClips();

            // Start crowd ambient
            StartCrowdAmbient();
        }

        private void OnEnable()
        {
            // Subscribe to kick contact events
            Gameplay.PlayerKicker.OnKickContactEvent += PlayKick;
        }

        private void OnDisable()
        {
            Gameplay.PlayerKicker.OnKickContactEvent -= PlayKick;
        }

        /// <summary>
        /// Handle match state changes for whistle/goal sounds.
        /// </summary>
        public void OnMatchStateChanged(MatchState oldState, MatchState newState)
        {
            var soundEvent = _mapper.MapMatchStateChange(oldState, newState);

            switch (soundEvent)
            {
                case SoundEventType.Whistle:
                    PlayWhistle();
                    break;
                case SoundEventType.GoalCheer:
                    PlayGoalCheer();
                    break;
            }
        }

        /// <summary>Play whistle sound.</summary>
        public void PlayWhistle()
        {
            if (_whistleClip != null)
                _sfxSource.PlayOneShot(_whistleClip);
        }

        /// <summary>Play kick sound.</summary>
        public void PlayKick()
        {
            if (_kickClip != null)
                _sfxSource.PlayOneShot(_kickClip);
        }

        /// <summary>Play goal celebration cheer.</summary>
        public void PlayGoalCheer()
        {
            if (_goalCheerClip != null)
                _sfxSource.PlayOneShot(_goalCheerClip);
        }

        /// <summary>Start crowd ambient loop.</summary>
        public void StartCrowdAmbient()
        {
            if (_crowdAmbientClip != null)
            {
                _ambientSource.clip = _crowdAmbientClip;
                _ambientSource.Play();
            }
        }

        /// <summary>Stop crowd ambient loop.</summary>
        public void StopCrowdAmbient()
        {
            _ambientSource.Stop();
        }

        private void GeneratePlaceholderClips()
        {
            if (_whistleClip == null)
                _whistleClip = CreateSineWaveClip(880f, 0.5f, "PlaceholderWhistle");

            if (_kickClip == null)
                _kickClip = CreateSineWaveClip(200f, 0.1f, "PlaceholderKick");

            if (_crowdAmbientClip == null)
                _crowdAmbientClip = CreateNoiseClip(2f, "PlaceholderCrowd");

            if (_goalCheerClip == null)
                _goalCheerClip = CreateSineWaveClip(440f, 1f, "PlaceholderGoalCheer");
        }

        private static AudioClip CreateSineWaveClip(float frequency, float duration, string name)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.3f;
                // Fade out
                float fade = 1f - (float)i / sampleCount;
                samples[i] *= fade;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateNoiseClip(float duration, string name)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            System.Random rng = new System.Random(42);
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = ((float)rng.NextDouble() * 2f - 1f) * 0.1f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
