using UnityEngine;

[DisallowMultipleComponent]
public class BossRoomTeleporter : MonoBehaviour
{
    [SerializeField] private GameObject bossRoomPrefab;
    [SerializeField] private Vector3 bossRoomWorldPosition = new Vector3(0f, 0f, 250f);
    [SerializeField] private string bossRoomSpawnPointName = "SpawnPoint";

    private GameObject _bossRoomInstance;

    public void Configure(GameObject roomPrefab, Vector3 worldPosition, string spawnPointName)
    {
        bossRoomPrefab = roomPrefab;
        bossRoomWorldPosition = worldPosition;
        bossRoomSpawnPointName = string.IsNullOrWhiteSpace(spawnPointName) ? "SpawnPoint" : spawnPointName;
        AttachTriggers();
    }

    private void Awake()
    {
        AttachTriggers();
    }

    private void AttachTriggers()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider triggerCollider = colliders[i];
            if (triggerCollider == null || !triggerCollider.isTrigger)
            {
                continue;
            }

            BossRoomTeleporterTrigger trigger = triggerCollider.GetComponent<BossRoomTeleporterTrigger>();
            if (trigger == null)
            {
                trigger = triggerCollider.gameObject.AddComponent<BossRoomTeleporterTrigger>();
            }

            trigger.Initialize(this);
        }
    }

    public void TryTeleport(PlayerStats playerStats)
    {
        if (playerStats == null || !playerStats.HasKey || bossRoomPrefab == null)
        {
            return;
        }

        if (_bossRoomInstance == null)
        {
            _bossRoomInstance = Instantiate(bossRoomPrefab, bossRoomWorldPosition, Quaternion.identity);
        }

        Transform destination = FindSpawnPoint(_bossRoomInstance.transform);
        Transform playerRoot = playerStats.transform.root;
        if (playerRoot == null)
        {
            return;
        }

        PlayerSpawner playerSpawner = FindFirstObjectByType<PlayerSpawner>();
        GameObject playerPrefab = playerSpawner != null && playerSpawner.playerPrefab != null
            ? playerSpawner.playerPrefab
            : playerRoot.gameObject;
        Vector3 spawnOffset = playerSpawner != null ? playerSpawner.spawnOffset : Vector3.zero;
        Vector3 spawnPosition = destination.position + spawnOffset;

        GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, destination.rotation);
        EnsurePlayerSetup(newPlayer);

        PlayerStats newPlayerStats = newPlayer.GetComponentInChildren<PlayerStats>();
        if (newPlayerStats != null)
        {
            playerStats.CopyStateTo(newPlayerStats);
        }

        Story.ShowBossMessage();
        GameAudioManager.PlayBossMusic();
        Destroy(playerRoot.gameObject);
    }

    private Transform FindSpawnPoint(Transform roomRoot)
    {
        if (roomRoot == null)
        {
            return transform;
        }

        Transform namedPoint = roomRoot.Find(bossRoomSpawnPointName);
        if (namedPoint != null)
        {
            return namedPoint;
        }

        Transform[] children = roomRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == bossRoomSpawnPointName)
            {
                return children[i];
            }
        }

        return roomRoot;
    }

    private void EnsurePlayerSetup(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        if (player.GetComponentInChildren<PlayerStats>() == null)
        {
            player.AddComponent<PlayerStats>();
        }

        if (player.GetComponentInChildren<PlayerRoomStatusLogger>() == null)
        {
            player.AddComponent<PlayerRoomStatusLogger>();
        }

        if (player.GetComponentInChildren<PlayerDamageScreenShake>() == null)
        {
            player.AddComponent<PlayerDamageScreenShake>();
        }

        if (player.GetComponentInChildren<DungeonMapUI>() == null)
        {
            player.AddComponent<DungeonMapUI>();
        }

        if (player.GetComponentInChildren<PlayerStatsHUD>() == null)
        {
            player.AddComponent<PlayerStatsHUD>();
        }

        if (player.GetComponentInChildren<PlayerDash>() == null)
        {
            player.AddComponent<PlayerDash>();
        }
    }
}
