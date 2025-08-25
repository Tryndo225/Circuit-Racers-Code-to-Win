using Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : Singleton<SoundManager>
{
    [Header("Audio Settings")]
    [Tooltip("Volume for background music (0.0 to 1.0)")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

    [Tooltip("Volume for sound effects (0.0 to 1.0)")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.5f;

    [Tooltip("Audio Mixer Group for background music")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;

    [Tooltip("Audio Mixer Group for sound effects")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    private AudioSource _musicSource;
    private Camera _camera;

    // Singleton pattern to ensure only one instance of SoundManager exists
    protected override void Awake()
    {
        base.Awake();

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.volume = musicVolume;
        _musicSource.outputAudioMixerGroup = musicMixerGroup;
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (_musicSource.clip != clip)
        {
            _musicSource.clip = clip;
            _musicSource.Play();
        }
        else if (!_musicSource.isPlaying)
        {
            _musicSource.Play();
        }
    }

    public void StopMusic()
    {
        if (_musicSource.isPlaying)
        {
            _musicSource.Stop();
        }
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;
        _musicSource.volume = musicVolume;
    }

    public void PlaySFXClip(AudioClip clip, Transform transform, float volume, float pitch)
    {
        if (clip == null) return;

        GameObject sfxObject = new GameObject("SFX_" + clip.name);
        sfxObject.transform.position = transform.position;
        AudioSource sfxSource = sfxObject.AddComponent<AudioSource>();
        sfxSource.outputAudioMixerGroup = sfxMixerGroup;
        sfxSource.spatialBlend = 1f;
        sfxSource.dopplerLevel = 0f;
        sfxSource.rolloffMode = AudioRolloffMode.Logarithmic;
        sfxSource.pitch = pitch;
        sfxSource.clip = clip;
        sfxSource.volume = sfxVolume * volume;
        sfxSource.Play();
        Destroy(sfxObject, clip.length);
    }

    public void PlaySFXClip(AudioClip clip, Transform transform)
    {
        PlaySFXClip(clip, transform, 1f, 1f);
    }

    public void PlaySFXClip(AudioClip clip)
    {
        PlaySFXClip(clip, transform, 1f, 1f);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
    }

    public void Update()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }
        if (_camera != null)
        {
            transform.position = _camera.transform.position;
        }
    }
}