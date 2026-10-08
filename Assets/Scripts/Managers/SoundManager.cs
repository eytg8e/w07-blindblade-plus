using System.Collections.Generic;
using UnityEngine;

public class SoundManager
{
    private const int MaxEffectVoices = 16;
    private readonly Transform _host;
    private readonly ResourceManager _resource;
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private readonly List<AudioSource> _effects = new List<AudioSource>();
    private Transform _root;
    private AudioSource _bgm;
    private int _replacementIndex;
    private float _bgmVolume = 1f;
    private float _effectVolume = 0.3f;

    internal SoundManager(Transform host, ResourceManager resource)
    {
        _host = host;
        _resource = resource;
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp01(value);
            if (_bgm != null) _bgm.volume = _bgmVolume;
        }
    }

    public float EffectVolume
    {
        get => _effectVolume;
        set
        {
            _effectVolume = Mathf.Clamp01(value);
            foreach (AudioSource source in _effects)
                if (source != null) source.volume = _effectVolume;
        }
    }

    public void Play(string path, SoundType type = SoundType.Effect, float pitch = 1f)
    {
        string fullPath = ResourceManager.WithPrefix(path, "Sounds/");
        if (!_clips.TryGetValue(fullPath, out AudioClip clip) || clip == null)
        {
            clip = _resource.Load<AudioClip>(fullPath);
            if (clip == null)
            {
                Debug.LogError("AudioClip not found in Resources: " + fullPath);
                return;
            }
            _clips[fullPath] = clip;
        }
        Play(clip, type, pitch);
    }
    
    private float lastBreakSoundTime;
    [SerializeField] private float breakSoundCooldown = 0.25f;
    public void PlayBreakSound()
    {
        if (Time.time - lastBreakSoundTime < breakSoundCooldown)
            return;

        lastBreakSoundTime = Time.time;

        if (Random.Range(0, 2) == 0)
        {
            float randomPitch = Random.Range(0.92f, 1.08f);

            Play("Break_01", pitch: randomPitch);
        }
        else
        {
            float randomPitch = Random.Range(0.92f, 1.08f);

            Play("Break_02", pitch: randomPitch);
        }

    }
    
    public void Play(AudioClip clip, SoundType type = SoundType.Effect, float pitch = 1f)
    {
        if (clip == null)
            return;
        EnsureRoot();
        AudioSource source = type == SoundType.BGM ? _bgm : GetEffectSource();
        source.Stop();
        source.clip = clip;
        source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
        source.volume = type == SoundType.BGM ? _bgmVolume : _effectVolume;
        source.Play();
    }

    public void Stop(SoundType type = SoundType.Effect)
    {
        if (type == SoundType.BGM)
            StopSource(_bgm);
        else
            foreach (AudioSource source in _effects) StopSource(source);
    }

    public void Clear()
    {
        Stop(SoundType.BGM);
        Stop(SoundType.Effect);
        _clips.Clear();
        _replacementIndex = 0;
    }

    private void EnsureRoot()
    {
        if (_root == null)
        {
            _root = new GameObject("@Sound").transform;
            _root.SetParent(_host, false);
            _effects.Clear();
            _bgm = null;
        }
        if (_bgm == null)
            _bgm = CreateSource("Bgm", true);
    }

    private AudioSource CreateSource(string name, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_root, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = 0.5f;
        return source;
    }

    private AudioSource GetEffectSource()
    {
        _effects.RemoveAll(source => source == null);
        foreach (AudioSource source in _effects)
            if (!source.isPlaying) return source;
        if (_effects.Count < MaxEffectVoices)
        {
            AudioSource source = CreateSource("Effect_" + _effects.Count, false);
            _effects.Add(source);
            return source;
        }
        AudioSource replacement = _effects[_replacementIndex % _effects.Count];
        _replacementIndex = (_replacementIndex + 1) % _effects.Count;
        return replacement;
    }

    private static void StopSource(AudioSource source)
    {
        if (source == null)
            return;
        source.Stop();
        source.clip = null;
    }
}
