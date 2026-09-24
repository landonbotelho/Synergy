using UnityEngine;

[DisallowMultipleComponent]
public class GameAudioManager : MonoBehaviour
{
    [System.Serializable]
    private class Sound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public static GameAudioManager Instance { get; private set; }

    [Header("Enemy")]
    [SerializeField] private Sound shortEnemyFire = new Sound();
    [SerializeField] private Sound longEnemyFire = new Sound();
    [SerializeField] private Sound enemyHit = new Sound();
    [SerializeField] private Sound enemyDeath = new Sound();

    [Header("Player")]
    [SerializeField] private Sound swordSwing = new Sound();
    [SerializeField] private Sound sprint = new Sound();
    [SerializeField] private Sound dash = new Sound();
    [SerializeField] private Sound playerDamage = new Sound();
    [SerializeField] private Sound playerDeath = new Sound();
    [SerializeField] private Sound playerRespawn = new Sound();

    [Header("Boss")]
    [SerializeField] private Sound bossSpawn = new Sound();
    [SerializeField] private Sound bossDeath = new Sound();

    [Header("Pickups And Store")]
    [SerializeField] private Sound moneyPickup = new Sound();
    [SerializeField] private Sound chestOpen = new Sound();
    [SerializeField] private Sound healthPickup = new Sound();
    [SerializeField] private Sound damagePickup = new Sound();
    [SerializeField] private Sound storeOpen = new Sound();

    [Header("Music")]
    [SerializeField] private Sound dungeonMusic = new Sound();
    [SerializeField] private Sound bossMusic = new Sound();

    private AudioSource _musicSource;
    private AudioSource _sprintSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        PlayDungeonMusic();
    }

    public static void PlayShortEnemyFire(Vector3 position) => Instance?.PlayOneShot(Instance.shortEnemyFire, position);
    public static void PlayLongEnemyFire(Vector3 position) => Instance?.PlayOneShot(Instance.longEnemyFire, position);
    public static void PlayEnemyHit(Vector3 position) => Instance?.PlayOneShot(Instance.enemyHit, position);
    public static void PlayEnemyDeath(Vector3 position) => Instance?.PlayOneShot(Instance.enemyDeath, position);
    public static void PlaySwordSwing(Vector3 position) => Instance?.PlayOneShot(Instance.swordSwing, position);
    public static void PlayPlayerDamage(Vector3 position) => Instance?.PlayOneShot(Instance.playerDamage, position);
    public static void PlayPlayerDeath(Vector3 position) => Instance?.PlayOneShot(Instance.playerDeath, position);
    public static void PlayPlayerRespawn(Vector3 position) => Instance?.PlayOneShot(Instance.playerRespawn, position);
    public static void PlayDash(Vector3 position) => Instance?.PlayOneShot(Instance.dash, position);
    public static void PlayBossSpawn(Vector3 position) => Instance?.PlayOneShot(Instance.bossSpawn, position);
    public static void PlayBossDeath(Vector3 position) => Instance?.PlayOneShot(Instance.bossDeath, position);
    public static void PlayMoneyPickup(Vector3 position) => Instance?.PlayOneShot(Instance.moneyPickup, position);
    public static void PlayChestOpen(Vector3 position) => Instance?.PlayOneShot(Instance.chestOpen, position);
    public static void PlayHealthPickup(Vector3 position) => Instance?.PlayOneShot(Instance.healthPickup, position);
    public static void PlayDamagePickup(Vector3 position) => Instance?.PlayOneShot(Instance.damagePickup, position);
    public static void PlayStoreOpen(Vector3 position) => Instance?.PlayOneShot(Instance.storeOpen, position);
    public static void StartSprintSound()
    {
        if (Instance != null)
        {
            Instance.PlayLoop(Instance.sprint, ref Instance._sprintSource);
        }
    }

    public static void StopSprintSound() => Instance?.StopLoop(Instance._sprintSource);
    public static void PlayDungeonMusic()
    {
        if (Instance != null)
        {
            Instance.PlayLoop(Instance.dungeonMusic, ref Instance._musicSource);
        }
    }

    public static void PlayBossMusic()
    {
        if (Instance != null)
        {
            Instance.PlayLoop(Instance.bossMusic, ref Instance._musicSource);
        }
    }

    public static void StopMusic() => Instance?.StopLoop(Instance._musicSource);

    private void PlayOneShot(Sound sound, Vector3 position)
    {
        if (sound == null || sound.clip == null)
        {
            return;
        }

        GameObject audioObject = new GameObject("One Shot Audio");
        audioObject.transform.position = position;
        DontDestroyOnLoad(audioObject);

        AudioSource source = audioObject.AddComponent<AudioSource>();
        if (source.clip != sound.clip)
        {
            source.Stop();
            source.clip = sound.clip;
        }

        source.volume = sound.volume;
        source.spatialBlend = 1f;
        source.Play();

        Destroy(audioObject, sound.clip.length);
    }

    private void PlayLoop(Sound sound, ref AudioSource source)
    {
        if (sound == null || sound.clip == null)
        {
            StopLoop(source);
            return;
        }

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        if (source.clip != sound.clip)
        {
            source.Stop();
            source.clip = sound.clip;
        }

        source.volume = sound.volume;

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private void StopLoop(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
    }
}
