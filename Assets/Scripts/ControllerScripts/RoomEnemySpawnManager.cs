using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomEnemySpawnManager : MonoBehaviour
{
    private class PendingEnemySpawn
    {
        public GameObject prefab;
        public Vector3 position;
        public int tier;
        public int roomId;
        public float fireRange;
    }

    private BSPSpawner _bspSpawner;
    private RandomSpawner _enemySpawner;
    private Transform _trackedPlayer;
    private readonly Dictionary<int, List<PendingEnemySpawn>> _pendingSpawnsByRoom = new Dictionary<int, List<PendingEnemySpawn>>();
    private readonly Dictionary<int, int> _aliveEnemiesByRoom = new Dictionary<int, int>();
    private readonly HashSet<int> _spawnedRooms = new HashSet<int>();

    public bool RegistrationComplete { get; private set; }

    public void Initialize(BSPSpawner bspSpawner, RandomSpawner enemySpawner)
    {
        _bspSpawner = bspSpawner;
        _enemySpawner = enemySpawner;
        _pendingSpawnsByRoom.Clear();
        _aliveEnemiesByRoom.Clear();
        _spawnedRooms.Clear();
        RegistrationComplete = false;
    }

    public void RegisterEnemySpawn(GameObject prefab, Vector3 position, int tier, int roomId, float fireRange)
    {
        if (prefab == null || roomId < 0)
        {
            return;
        }

        if (!_pendingSpawnsByRoom.TryGetValue(roomId, out List<PendingEnemySpawn> pendingSpawns))
        {
            pendingSpawns = new List<PendingEnemySpawn>();
            _pendingSpawnsByRoom[roomId] = pendingSpawns;
        }

        pendingSpawns.Add(new PendingEnemySpawn
        {
            prefab = prefab,
            position = position,
            tier = tier,
            roomId = roomId,
            fireRange = fireRange
        });

        if (!_aliveEnemiesByRoom.ContainsKey(roomId))
        {
            _aliveEnemiesByRoom[roomId] = 0;
        }

        _aliveEnemiesByRoom[roomId]++;
    }

    public void CopyAliveEnemyCounts(Dictionary<int, int> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();

        foreach (KeyValuePair<int, int> pair in _aliveEnemiesByRoom)
        {
            destination[pair.Key] = pair.Value;
        }
    }

    public void CompleteRegistration()
    {
        RegistrationComplete = true;
    }

    private void Update()
    {
        if (_bspSpawner == null || _enemySpawner == null)
        {
            return;
        }

        if (_trackedPlayer == null)
        {
            ResolveTrackedPlayer();
            if (_trackedPlayer == null)
            {
                return;
            }
        }

        int roomId = _bspSpawner.GetRoomIdForPosition(_trackedPlayer.position);
        if (roomId < 0 || _spawnedRooms.Contains(roomId))
        {
            return;
        }

        SpawnRoomEnemies(roomId);
    }

    private void SpawnRoomEnemies(int roomId)
    {
        _spawnedRooms.Add(roomId);

        if (!_pendingSpawnsByRoom.TryGetValue(roomId, out List<PendingEnemySpawn> pendingSpawns))
        {
            return;
        }

        for (int i = 0; i < pendingSpawns.Count; i++)
        {
            PendingEnemySpawn pendingSpawn = pendingSpawns[i];
            _enemySpawner.SpawnEnemyInstance(
                pendingSpawn.prefab,
                pendingSpawn.position,
                pendingSpawn.tier,
                pendingSpawn.roomId,
                pendingSpawn.fireRange
            );
        }
    }

    private void ResolveTrackedPlayer()
    {
        controlledPlayerCombat playerCombat = FindFirstObjectByType<controlledPlayerCombat>();
        if (playerCombat != null)
        {
            _trackedPlayer = playerCombat.transform;
            return;
        }

        CharacterController characterController = FindFirstObjectByType<CharacterController>();
        if (characterController != null)
        {
            _trackedPlayer = characterController.transform;
        }
    }
}
