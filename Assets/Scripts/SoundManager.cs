using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    // ------------------ ИСТОЧНИКИ ------------------
    [Header("Основной источник звука (эффекты)")]
    public AudioSource sfxSource;

    [Header("Источник музыки")]
    public AudioSource musicSource;

    // ------------------ МУЗЫКА ------------------
    [Header("Музыка")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;

    // ------------------ SFX: Пушка ------------------
    [Header("Звуки пушки")]
    public AudioClip[] shootClips;

    // ------------------ SFX: UI ------------------
    [Header("Звуки UI (кнопки)")]
    public AudioClip[] uiClickClips;

    // ------------------ SFX: Зомби ------------------
    [Header("Звуки зомби")]
    public AudioClip[] zombieDieClips;
    public AudioClip[] zombieHitClips;
    public AudioClip[] zombieSpawnClips;


    // ===================== ИНИЦИАЛИЗАЦИЯ =====================
    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    // ===================== ВСПОМОГАТЕЛЬНЫЙ МЕТОД =====================
    private void PlayRandom(AudioClip[] clips)
    {
        if (sfxSource == null || clips == null || clips.Length == 0) 
            return;

        int index = Random.Range(0, clips.Length);
        sfxSource.PlayOneShot(clips[index]);
    }


    // ===================== SFX =====================
    public void PlayShoot()       => PlayRandom(shootClips);
    public void PlayUIClick()     => PlayRandom(uiClickClips);
    public void PlayZombieDie()   => PlayRandom(zombieDieClips);
    public void PlayZombieHit()   => PlayRandom(zombieHitClips);
    public void PlayZombieSpawn() => PlayRandom(zombieSpawnClips);


    // ===================== МУЗЫКА =====================
    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic, true);
    }

    public void PlayGameMusic()
    {
        PlayMusic(gameMusic, true);
    }

    private void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null)
            return;

        // если уже эта музыка играет — не переключаем
        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }
}
