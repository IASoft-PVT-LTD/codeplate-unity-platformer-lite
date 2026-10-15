using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Sound effects and music by name: Play("coin") plays Assets/Resources/Audio/coin.wav,
    /// PlayMusic("music_world1") loops a track with a short crossfade. Volumes come from the
    /// saved settings. To add a sound: drop the .wav in Audio/, list it in assets.json, call
    /// Play("name") (docs/recipes/music-and-sound.md).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const int Voices = 10;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _voices = new List<AudioSource>();
        AudioSource _musicA, _musicB;
        string _music;
        float _fade = 1;
        int _next;

        /// <summary>The music track playing now (tests read it).</summary>
        public string CurrentMusic => _music;

        void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _voices.Add(s);
            }
            _musicA = gameObject.AddComponent<AudioSource>();
            _musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { _musicA, _musicB })
            {
                m.loop = true;
                m.playOnAwake = false;
            }
        }

        float SfxVolume => App.Ready ? (float)App.Progress.Settings.SfxVolume : 1f;
        float MusicVolume => App.Ready ? (float)App.Progress.Settings.MusicVolume : 1f;

        AudioClip Clip(string name)
        {
            if (_clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) Debug.LogWarning($"[CodePlate] missing sound: {Game.Core.ProjectPaths.Audio}/{name}.wav");
            _clips[name] = c;
            return c;
        }

        /// <summary>Plays a sound effect. pitch 1 = as recorded; volumeDb lowers it (e.g. -6).</summary>
        public void Play(string name, float pitch = 1f, float volumeDb = 0f)
        {
            var clip = Clip(name);
            if (clip == null) return;
            var s = _voices[_next];
            _next = (_next + 1) % _voices.Count;
            s.pitch = pitch;
            s.volume = SfxVolume * Mathf.Pow(10f, volumeDb / 20f);
            s.PlayOneShot(clip);
        }

        /// <summary>Loops a music track (crossfades from the current one). The same track keeps playing.</summary>
        public void PlayMusic(string name)
        {
            if (name == _music) return;
            _music = name;
            var clip = string.IsNullOrEmpty(name) ? null : Clip(name);
            (_musicA, _musicB) = (_musicB, _musicA);
            _musicA.clip = clip;
            _musicA.volume = 0;
            if (clip != null) _musicA.Play();
            _fade = 0;
        }

        public void StopMusic() => PlayMusic(null);

        void Update()
        {
            _fade = Mathf.Min(1, _fade + Time.unscaledDeltaTime / 0.6f);
            _musicA.volume = MusicVolume * _fade;
            _musicB.volume = MusicVolume * (1 - _fade);
            if (_fade >= 1 && _musicB.isPlaying) _musicB.Stop();
        }
    }
}
