using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomEntranceBlockerManager : MonoBehaviour
{
    private class RoomEntranceBlockerGroup
    {
        public int linkedRoomId;
        public readonly List<GameObject> blockers = new List<GameObject>();
    }

    [SerializeField]
    private GameObject blockerTilePrefab;
    [SerializeField]
    private Vector3 blockerOffset = new Vector3(0f, 1f, 0f);

    private BSPSpawner _bspSpawner;
    private RoomEnemySpawnManager _roomEnemySpawnManager;
    private Transform _trackedPlayer;
    private readonly Dictionary<int, List<RoomEntranceBlockerGroup>> _blockerGroupsByRoom = new Dictionary<int, List<RoomEntranceBlockerGroup>>();
    private readonly Dictionary<int, int> _aliveEnemiesByRoom = new Dictionary<int, int>();
    private int _currentRoomId = -1;

    public void Initialize(BSPSpawner bspSpawner, GameObject blockerPrefab, Vector3 offset)
    {
        _bspSpawner = bspSpawner;
        _roomEnemySpawnManager = FindFirstObjectByType<RoomEnemySpawnManager>();
        blockerTilePrefab = blockerPrefab;
        blockerOffset = offset;
        BuildRoomBlockers();
        CacheEnemyCounts();
    }

    private void OnEnable()
    {
        EnemyVitals.EnemyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        EnemyVitals.EnemyDied -= HandleEnemyDied;
    }

    private void Update()
    {
        if (_bspSpawner == null || blockerTilePrefab == null)
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
        if (roomId < 0 || roomId == _currentRoomId)
        {
            return;
        }

        if (_currentRoomId >= 0)
        {
            SetRoomBlockersActive(_currentRoomId, false);
        }

        _currentRoomId = roomId;

        if (GetAliveEnemyCount(roomId) > 0)
        {
            UpdateRoomBlockersForCurrentState(roomId);
        }
    }

    private void BuildRoomBlockers()
    {
        _blockerGroupsByRoom.Clear();

        if (_bspSpawner == null || blockerTilePrefab == null)
        {
            return;
        }

        List<HallwayConnection> connections = _bspSpawner.GetHallwayConnections();
        for (int i = 0; i < connections.Count; i++)
        {
            HallwayConnection connection = connections[i];
            if (connection.roomA != null && connection.roomB != null)
            {
                AddBlockerGroup(connection.roomA.id, connection.roomB.id, connection.outsideDoorwayA);
                AddBlockerGroup(connection.roomB.id, connection.roomA.id, connection.outsideDoorwayB);
            }
        }
    }

    private void CacheEnemyCounts()
    {
        _aliveEnemiesByRoom.Clear();

        if (_roomEnemySpawnManager != null)
        {
            if (!_roomEnemySpawnManager.RegistrationComplete)
            {
                return;
            }

            _roomEnemySpawnManager.CopyAliveEnemyCounts(_aliveEnemiesByRoom);
            return;
        }

        EnemyVitals[] enemies = FindObjectsByType<EnemyVitals>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyVitals enemy = enemies[i];
            if (enemy == null || enemy.SpawnedRoomId < 0)
            {
                continue;
            }

            if (!_aliveEnemiesByRoom.ContainsKey(enemy.SpawnedRoomId))
            {
                _aliveEnemiesByRoom[enemy.SpawnedRoomId] = 0;
            }

            _aliveEnemiesByRoom[enemy.SpawnedRoomId]++;
        }
    }

    private void HandleEnemyDied(EnemyVitals enemyVitals)
    {
        if (enemyVitals == null || enemyVitals.SpawnedRoomId < 0)
        {
            return;
        }

        DecrementEnemyCount(enemyVitals.SpawnedRoomId);

        if (enemyVitals.SpawnedRoomId == _currentRoomId && GetAliveEnemyCount(_currentRoomId) == 0)
        {
            SetRoomBlockersActive(_currentRoomId, false);
        }
        else if (enemyVitals.SpawnedRoomId == _currentRoomId)
        {
            UpdateRoomBlockersForCurrentState(_currentRoomId);
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

    private int GetAliveEnemyCount(int roomId)
    {
        return _aliveEnemiesByRoom.TryGetValue(roomId, out int count) ? count : 0;
    }

    private void SetRoomBlockersActive(int roomId, bool isActive)
    {
        if (!_blockerGroupsByRoom.TryGetValue(roomId, out List<RoomEntranceBlockerGroup> groups))
        {
            return;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            SetBlockerGroupActive(groups[i], isActive);
        }
    }

    private void UpdateRoomBlockersForCurrentState(int roomId)
    {
        if (!_blockerGroupsByRoom.TryGetValue(roomId, out List<RoomEntranceBlockerGroup> groups))
        {
            return;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            SetBlockerGroupActive(groups[i], ShouldBlockConnection(roomId, groups[i].linkedRoomId));
        }
    }

    private bool IsRoomCleared(int roomId)
    {
        return GetAliveEnemyCount(roomId) <= 0;
    }

    private bool ShouldBlockConnection(int roomId, int linkedRoomId)
    {
        return GetAliveEnemyCount(roomId) > 0 && !IsRoomCleared(linkedRoomId);
    }

    private void SetBlockerGroupActive(RoomEntranceBlockerGroup group, bool isActive)
    {
        for (int i = 0; i < group.blockers.Count; i++)
        {
            if (group.blockers[i] != null)
            {
                group.blockers[i].SetActive(isActive);
            }
        }
    }

    private void DecrementEnemyCount(int roomId)
    {
        if (_aliveEnemiesByRoom.ContainsKey(roomId))
        {
            _aliveEnemiesByRoom[roomId] = Mathf.Max(0, _aliveEnemiesByRoom[roomId] - 1);
        }
    }

    private void AddBlockerGroup(int roomId, int linkedRoomId, List<Vector3Int> entranceCells)
    {
        if (entranceCells == null || entranceCells.Count == 0)
        {
            return;
        }

        if (!_blockerGroupsByRoom.TryGetValue(roomId, out List<RoomEntranceBlockerGroup> groups))
        {
            groups = new List<RoomEntranceBlockerGroup>();
            _blockerGroupsByRoom[roomId] = groups;
        }

        RoomEntranceBlockerGroup group = new RoomEntranceBlockerGroup
        {
            linkedRoomId = linkedRoomId
        };

        for (int i = 0; i < entranceCells.Count; i++)
        {
            Vector3Int cell = entranceCells[i];
            Vector3 position = new Vector3(cell.x, cell.y, cell.z) + blockerOffset;
            GameObject blocker = Instantiate(blockerTilePrefab, position, Quaternion.identity);
            blocker.name = $"HallwayBlocker_Room_{roomId}_To_{linkedRoomId}_{i}";
            blocker.SetActive(false);
            group.blockers.Add(blocker);
        }

        groups.Add(group);
    }
}
