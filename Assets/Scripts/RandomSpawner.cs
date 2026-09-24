using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RandomSpawner : MonoBehaviour
{
    private RoomEnemySpawnManager _roomEnemySpawnManager;

    [Header("Short Range Enemy Prefabs (Blue)")]
    public GameObject shortTier1_LightBlue;
    public GameObject shortTier2_Blue;
    public GameObject shortTier3_DarkBlue;

    [Header("Long Range Enemy Prefabs (Green)")]
    public GameObject longTier1_LightGreen;
    public GameObject longTier2_Green;
    public GameObject longTier3_DarkGreen;

    [Header("Item Prefabs (Random Drop)")]
    public GameObject[] itemPrefabs;

    [Header("Pickup Mapping")]
    public GameObject healthPickupPrefab;
    public int healthPickupAmount = 10;
    public GameObject strengthPickupPrefab;
    public int strengthPickupAmount = 25;
    public GameObject moneyPickupPrefab;
    public int moneyPickupAmount = 100;
    public GameObject chestPickupPrefab;
    public int chestPickupAmount = 500;
    public GameObject keyPickupPrefab;
    public GameObject storePrefab;

    [Header("Branch Room Loot")]
    [Tooltip("Guaranteed loot for rooms that branch off the main path by 1 room.")]
    public GameObject[] branchLootPrefabs;
    [Tooltip("Guaranteed rarer loot for rooms that are 2+ steps away from the main path.")]
    public GameObject[] rareBranchLootPrefabs;

    [Tooltip("0 = never, 1 = always. Example: 0.35 means 35% chance per room.")]
    [Range(0f, 1f)]
    public float itemSpawnChancePerRoom = 0.35f;

    [Header("Spawn Settings")]
    public float spawnYOffset = 1f;

    [Header("Enemy Density")]
    [Min(1f)]
    public float roomAreaPerEnemy = 25f;
    [Tooltip("Absolute cap on the number of enemies allowed in a room after scaling.")]
    [Min(1)]
    public int maxEnemiesPerRoom = 4;

    [Header("Enemy Navigation")]
    public float enemyMoveSpeed = 3.5f;
    public float enemyAngularSpeed = 720f;
    public float enemyAcceleration = 10f;
    public float enemyStoppingDistance = 1.5f;

    [Header("Enemy Tier Health")]
    public int tier1EnemyHealth = 100;
    public int tier2EnemyHealth = 300;
    public int tier3EnemyHealth = 500;

    [Header("Enemy Fire Attacks")]
    public GameObject fireProjectilePrefab;
    public float tier1EnemyFireProjectileSpeed = 8f;
    public float tier2EnemyFireProjectileSpeed = 10f;
    public float tier3EnemyFireProjectileSpeed = 12f;
    public float shortEnemyFireRange = 5f;
    public float longEnemyFireRange = 10f;
    public float enemyFireCooldown = 4f;
    public float enemyFireLifetime = 5f;

    [Header("Room Blocking")]
    public GameObject hallwayBlockerPrefab;
    public Vector3 hallwayBlockerOffset = new Vector3(0f, 1f, 0f);

    [Header("Boss Teleporter")]
    public GameObject bossTeleporterPrefab;
    public Vector3 bossTeleporterOffset = new Vector3(0f, 1f, 0f);
    public GameObject bossRoomPrefab;
    public Vector3 bossRoomWorldPosition = new Vector3(0f, 0f, 250f);
    public string bossRoomSpawnPointName = "SpawnPoint";

    IEnumerator Start()
    {
        // Ensure BSPSpawner finished generating first
        yield return null;

        BSPSpawner spawner = GetComponent<BSPSpawner>();
        if (spawner == null)
        {
            Debug.LogError("BSPSpawner not found on this GameObject.");
            yield break;
        }

        _roomEnemySpawnManager = GetComponent<RoomEnemySpawnManager>();
        if (_roomEnemySpawnManager == null)
        {
            _roomEnemySpawnManager = gameObject.AddComponent<RoomEnemySpawnManager>();
        }

        _roomEnemySpawnManager.Initialize(spawner, this);

        List<Bounds> rooms = spawner.GetLeafRooms();

        foreach (Bounds room in rooms)
        {
            SpawnRoomContent(room, spawner);
        }

        if (_roomEnemySpawnManager != null)
        {
            _roomEnemySpawnManager.CompleteRegistration();
        }

        if (hallwayBlockerPrefab != null)
        {
            RoomEntranceBlockerManager blockerManager = GetComponent<RoomEntranceBlockerManager>();
            if (blockerManager == null)
            {
                blockerManager = gameObject.AddComponent<RoomEntranceBlockerManager>();
            }

            blockerManager.Initialize(spawner, hallwayBlockerPrefab, hallwayBlockerOffset);
        }

        SpawnBossTeleporter(spawner);
    }

    void SpawnRoomContent(Bounds roomBounds, BSPSpawner spawner)
    {
        //calcs room inner psoition
        int xMin = Mathf.RoundToInt(roomBounds.min.x) + 1;
        int xMax = Mathf.RoundToInt(roomBounds.max.x) - 1;
        int zMin = Mathf.RoundToInt(roomBounds.min.z) + 1;
        int zMax = Mathf.RoundToInt(roomBounds.max.z) - 1;
        //gets w/h
        int width = xMax - xMin + 1;
        int height = zMax - zMin + 1;

        if (width <= 2 || height <= 2) return;

        int area = width * height;

        //determines number of enemies based on room size
        int totalEnemies = Mathf.Clamp(
            Mathf.FloorToInt(area / Mathf.Max(1f, roomAreaPerEnemy)),
            0,
            maxEnemiesPerRoom
        );

        //randomly gets possible spawn position based on room position and size in world
        List<Vector3> validPositions = GenerateRandomSpawnPositions(
            xMin,
            xMax,
            zMin,
            zMax,
            roomBounds.center.y + spawnYOffset
        );

        if (validPositions.Count == 0) return;

        totalEnemies = Mathf.Min(totalEnemies, validPositions.Count);

        //applies randomness to spawn positions
        Shuffle(validPositions);

        int roomId;
        if (spawner != null)
        {
            roomId = spawner.GetRoomIdForBounds(roomBounds);
        }
        else
        {
            roomId = -1;
        }
        int roomTier;
        if (spawner != null)
        {
            roomTier = spawner.GetEnemyTierForRoom(roomId);
        }
        else
        {
            roomTier = 1;
        }
        bool isBranchRoom = spawner != null && spawner.IsBranchRoom(roomId);
        bool isFarthestBranchRoom = spawner != null && spawner.GetFarthestBranchRoomId() == roomId;

        int used = 0;

        //determines number of short vs long enemies per room
        if (totalEnemies > 0)
        {
            int shortCount = Random.Range(0, totalEnemies + 1);
            int longCount = totalEnemies - shortCount;

            GameObject shortPrefab = GetShortPrefab(roomTier);
            for (int i = 0; i < shortCount; i++)
            {
                RegisterEnemySpawn(shortPrefab, validPositions[used], roomTier, roomId, shortEnemyFireRange);
                used++;
            }

            GameObject longPrefab = GetLongPrefab(roomTier);
            for (int i = 0; i < longCount; i++)
            {
                RegisterEnemySpawn(longPrefab, validPositions[used], roomTier, roomId, longEnemyFireRange);
                used++;
            }
        }

        //spawning for side rooms
        bool spawnedBranchLoot = false;
        if (isBranchRoom)
        {
            spawnedBranchLoot = TrySpawnBranchLoot(validPositions, ref used, isFarthestBranchRoom, roomId, spawner);
        }

        if (!spawnedBranchLoot)
        {
            TrySpawnRandomItem(validPositions, used);
        }
    }

    void TrySpawnRandomItem(List<Vector3> validPositions, int startIndex)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return;
        if (startIndex >= validPositions.Count) return;

        if (Random.value > itemSpawnChancePerRoom) return;

        GameObject itemPrefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];
        Vector3 pos = validPositions[startIndex];
        pos.y -= GetItemSpawnLowering(itemPrefab);

        GameObject itemInstance = Instantiate(itemPrefab, pos, Quaternion.identity);
        EnsureItemPickup(itemPrefab, itemInstance);
    }

    bool TrySpawnBranchLoot(List<Vector3> validPositions, ref int startIndex, bool isFarthestBranchRoom, int roomId, BSPSpawner spawner)
    {
        if (startIndex >= validPositions.Count) return false;

        GameObject[] lootPool = isFarthestBranchRoom ? rareBranchLootPrefabs : branchLootPrefabs;

        if (lootPool == null || lootPool.Length == 0)
        {
            lootPool = itemPrefabs;
        }

        if (lootPool == null || lootPool.Length == 0)
            return false;

        GameObject itemPrefab = lootPool[Random.Range(0, lootPool.Length)];
        Vector3 pos = validPositions[startIndex];
        pos.y -= GetItemSpawnLowering(itemPrefab);

        GameObject itemInstance = Instantiate(itemPrefab, pos, Quaternion.identity);
        EnsureItemPickup(itemPrefab, itemInstance);

        if (isFarthestBranchRoom && spawner != null)
        {
            int nearestMainPathRoomId = spawner.GetNearestMainPathRoomId(roomId);
            int mainPathIndex = spawner.GetMainPathIndex(nearestMainPathRoomId);
            int mainPathCount = spawner.GetMainPathRoomCount();
            Debug.Log($"Key spawn location in side room off of main path room {mainPathIndex + 1}/{mainPathCount}.");
        }

        startIndex++;
        return true;
    }

    void RegisterEnemySpawn(GameObject prefab, Vector3 position, int tier, int roomId, float fireRange)
    {
        if (_roomEnemySpawnManager != null)
        {
            _roomEnemySpawnManager.RegisterEnemySpawn(prefab, position, tier, roomId, fireRange);
            return;
        }

        SpawnEnemyInstance(prefab, position, tier, roomId, fireRange);
    }

    public void SpawnEnemyInstance(GameObject prefab, Vector3 position, int tier, int roomId, float fireRange)
    {
        if (prefab == null)
            return;

        GameObject enemy = Instantiate(prefab, position, Quaternion.identity);
        EnsureEnemyNavigation(enemy, tier, roomId, fireRange);
    }

    void EnsureEnemyNavigation(GameObject enemy, int tier, int roomId, float fireRange)
    {
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = enemy.AddComponent<NavMeshAgent>();
        }

        agent.speed = enemyMoveSpeed;
        agent.angularSpeed = enemyAngularSpeed;
        agent.acceleration = enemyAcceleration;
        agent.stoppingDistance = enemyStoppingDistance;
        agent.autoBraking = false;
        agent.autoRepath = true;
        agent.updateRotation = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(20, 60);

        if (NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
        {
            enemy.transform.position = navHit.position;

            if (agent.isOnNavMesh)
            {
                agent.Warp(navHit.position);
            }
        }
        else
        {
            Debug.LogWarning($"Enemy '{enemy.name}' could not find a nearby NavMesh position when spawned.", enemy);
        }

        EnemyVitals enemyVitals = enemy.GetComponent<EnemyVitals>();
        if (enemyVitals == null)
        {
            Debug.LogWarning($"Enemy '{enemy.name}' is missing EnemyVitals.", enemy);
            return;
        }

        enemyVitals.Configure(GetEnemyHealthForTier(tier));
        enemyVitals.SetSpawnedRoomId(roomId);

        EnemyChaseController chaseController = enemy.GetComponent<EnemyChaseController>();
        if (chaseController == null)
        {
            chaseController = enemy.AddComponent<EnemyChaseController>();
        }

        chaseController.ConfigureRangedAttack(
            fireProjectilePrefab,
            fireRange,
            GetEnemyProjectileSpeedForTier(tier),
            enemyFireCooldown,
            enemyFireLifetime,
            GetEnemyDamageForTier(tier)
        );

        chaseController.ConfigureFireSoundType(Mathf.Approximately(fireRange, longEnemyFireRange));
    }

    void EnsureItemPickup(GameObject sourcePrefab, GameObject itemInstance)
    {
        if (sourcePrefab == null || itemInstance == null)
            return;

        if (sourcePrefab == storePrefab)
        {
            StoreInteract storeInteract = itemInstance.GetComponent<StoreInteract>();
            if (storeInteract == null)
            {
                storeInteract = itemInstance.AddComponent<StoreInteract>();
            }

            return;
        }

        ItemPickup pickup = itemInstance.GetComponent<ItemPickup>();
        if (pickup == null)
        {
            pickup = itemInstance.AddComponent<ItemPickup>();
        }

        if (sourcePrefab == strengthPickupPrefab)
        {
            pickup.Configure(ItemPickup.PickupType.Strength, strengthPickupAmount);
        }
        else if (sourcePrefab == healthPickupPrefab)
        {
            pickup.Configure(ItemPickup.PickupType.Health, healthPickupAmount);
        }
        else if (sourcePrefab == moneyPickupPrefab)
        {
            pickup.Configure(ItemPickup.PickupType.Money, moneyPickupAmount);
        }
        else if (sourcePrefab == chestPickupPrefab)
        {
            pickup.Configure(ItemPickup.PickupType.Chest, chestPickupAmount);
            SphereCollider chestTrigger = itemInstance.GetComponent<SphereCollider>();
            if (chestTrigger == null)
            {
                chestTrigger = itemInstance.AddComponent<SphereCollider>();
            }

            chestTrigger.radius = 1.4f;
            chestTrigger.center = new Vector3(0f, 0.6f, 0f);
            chestTrigger.isTrigger = true;
        }
        else if (sourcePrefab == keyPickupPrefab)
        {
            pickup.Configure(ItemPickup.PickupType.Key, 1);
        }
        else
        {
            return;
        }

        Collider itemCollider = itemInstance.GetComponent<Collider>();
        if (itemCollider == null)
        {
            SphereCollider sphereCollider = itemInstance.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;
            itemCollider = sphereCollider;
        }

        itemCollider.isTrigger = true;

        Rigidbody rb = itemInstance.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = itemInstance.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.isKinematic = true;
    }

    float GetItemSpawnLowering(GameObject itemPrefab)
    {
        if (itemPrefab == chestPickupPrefab)
        {
            return 0.5f;
        }

        if (itemPrefab == storePrefab)
        {
            return 0.5f;
        }

        return 0.1f;
    }

    int GetEnemyHealthForTier(int tier)
    {
        switch (tier)
        {
            case 1:
                return tier1EnemyHealth;
            case 2:
                return tier2EnemyHealth;
            default:
                return tier3EnemyHealth;
        }
    }

    int GetEnemyDamageForTier(int tier)
    {
        switch (tier)
        {
            case 1:
                return 10;
            case 2:
                return 20;
            default:
                return 30;
        }
    }

    float GetEnemyProjectileSpeedForTier(int tier)
    {
        switch (tier)
        {
            case 1:
                return tier1EnemyFireProjectileSpeed;
            case 2:
                return tier2EnemyFireProjectileSpeed;
            default:
                return tier3EnemyFireProjectileSpeed;
        }
    }

    List<Vector3> GenerateRandomSpawnPositions(int xMin, int xMax, int zMin, int zMax, float y)
    {
        List<Vector3> positions = new List<Vector3>();
        for (int x = xMin; x <= xMax; x++)
        {
            for (int z = zMin; z <= zMax; z++)
            {
                positions.Add(new Vector3(x, y, z));
            }
        }

        Shuffle(positions);
        return positions;
    }

    void Shuffle(List<Vector3> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    GameObject GetShortPrefab(int tier)
    {
        switch (tier)
        {
            case 1: return shortTier1_LightBlue;
            case 2: return shortTier2_Blue;
            default: return shortTier3_DarkBlue;
        }
    }

    GameObject GetLongPrefab(int tier)
    {
        switch (tier)
        {
            case 1: return longTier1_LightGreen;
            case 2: return longTier2_Green;
            default: return longTier3_DarkGreen;
        }
    }

    void SpawnBossTeleporter(BSPSpawner spawner)
    {
        if (spawner == null || spawner.endRoom == null || bossTeleporterPrefab == null || bossRoomPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = spawner.endRoom.center + bossTeleporterOffset;
        GameObject teleporter = Instantiate(bossTeleporterPrefab, spawnPosition, Quaternion.identity);
        BossRoomTeleporter bossTeleporter = teleporter.GetComponent<BossRoomTeleporter>();
        if (bossTeleporter == null)
        {
            bossTeleporter = teleporter.AddComponent<BossRoomTeleporter>();
        }

        bossTeleporter.Configure(bossRoomPrefab, bossRoomWorldPosition, bossRoomSpawnPointName);
    }
}
