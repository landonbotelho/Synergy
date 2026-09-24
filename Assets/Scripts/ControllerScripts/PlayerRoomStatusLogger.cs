using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRoomStatusLogger : MonoBehaviour
{
    [SerializeField]
    private bool enableLogs = true;

    private BSPSpawner _bspSpawner;
    private RoomEnemySpawnManager _roomEnemySpawnManager;
    private Transform _trackedTransform;
    private int _lastLoggedRoomId = -999;
    private int _currentRoomId = -1;
    private readonly System.Collections.Generic.Dictionary<int, int> _aliveEnemiesByRoom = new System.Collections.Generic.Dictionary<int, int>();
    private bool _enemyCountsInitialized;

    private void Start()
    {
        _bspSpawner = FindFirstObjectByType<BSPSpawner>();
        _roomEnemySpawnManager = FindFirstObjectByType<RoomEnemySpawnManager>();
        ResolveTrackedTransform();
        InitializeEnemyCounts();
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
        if (!enableLogs)
        {
            return;
        }

        if (_bspSpawner == null)
        {
            _bspSpawner = FindFirstObjectByType<BSPSpawner>();
            if (_bspSpawner == null)
            {
                return;
            }
        }

        if (!_enemyCountsInitialized)
        {
            InitializeEnemyCounts();
        }

        if (_trackedTransform == null)
        {
            ResolveTrackedTransform();
            if (_trackedTransform == null)
            {
                return;
            }
        }

        int roomId = _bspSpawner.GetRoomIdForPosition(_trackedTransform.position);
        if (roomId < 0 || roomId == _lastLoggedRoomId)
        {
            _currentRoomId = roomId;
            return;
        }

        _currentRoomId = roomId;
        _lastLoggedRoomId = roomId;
        Debug.Log(BuildRoomStatusMessage(roomId), this);
    }

    private string BuildRoomStatusMessage(int roomId, int countAdjustment = 0)
    {
        int enemyCount = Mathf.Max(0, GetEnemyCountInRoom(roomId) + countAdjustment);
        int enemyTier = _bspSpawner.GetEnemyTierForRoom(roomId);

        if (_bspSpawner.IsMainPathRoom(roomId))
        {
            int mainPathIndex = _bspSpawner.GetMainPathIndex(roomId);
            int mainPathCount = _bspSpawner.GetMainPathRoomCount();
            return $"Currently in room {mainPathIndex + 1}/{mainPathCount} rooms on main path. Enemy tier: {enemyTier}. Enemies in room: {enemyCount}.";
        }

        int branchDepth = _bspSpawner.GetBranchDepth(roomId);
        if (branchDepth < 0)
        {
            return $"Currently in room {roomId}. Enemy tier: {enemyTier}. Enemies in room: {enemyCount}.";
        }

        string roomWord = branchDepth == 1 ? "room" : "rooms";
        return $"Currently in side room: {branchDepth} {roomWord} from nearest main path dungeon. Enemy tier: {enemyTier}. Enemies in room: {enemyCount}.";
    }

    private void ResolveTrackedTransform()
    {
        controlledPlayerCombat playerCombat = GetComponentInChildren<controlledPlayerCombat>();
        if (playerCombat != null)
        {
            _trackedTransform = playerCombat.transform;
            return;
        }

        CharacterController characterController = GetComponentInChildren<CharacterController>();
        if (characterController != null)
        {
            _trackedTransform = characterController.transform;
            return;
        }

        _trackedTransform = transform;
    }

    private int GetEnemyCountInRoom(int roomId)
    {
        if (_aliveEnemiesByRoom.TryGetValue(roomId, out int count))
        {
            return count;
        }

        return 0;
    }

    private void HandleEnemyDied(EnemyVitals enemyVitals)
    {
        if (!enableLogs || enemyVitals == null)
        {
            return;
        }

        if (_currentRoomId < 0 || enemyVitals.SpawnedRoomId != _currentRoomId)
        {
            DecrementEnemyCount(enemyVitals.SpawnedRoomId);
            return;
        }

        DecrementEnemyCount(enemyVitals.SpawnedRoomId);
        Debug.Log(BuildRoomStatusMessage(_currentRoomId), this);
    }

    private void InitializeEnemyCounts()
    {
        _roomEnemySpawnManager = FindFirstObjectByType<RoomEnemySpawnManager>();
        if (_roomEnemySpawnManager != null)
        {
            if (!_roomEnemySpawnManager.RegistrationComplete)
            {
                _enemyCountsInitialized = false;
                return;
            }

            _roomEnemySpawnManager.CopyAliveEnemyCounts(_aliveEnemiesByRoom);
            _enemyCountsInitialized = true;
            return;
        }

        EnemyVitals[] enemies = FindObjectsByType<EnemyVitals>(FindObjectsSortMode.None);
        _aliveEnemiesByRoom.Clear();

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

        _enemyCountsInitialized = true;
    }

    private void DecrementEnemyCount(int roomId)
    {
        if (roomId < 0 || !_aliveEnemiesByRoom.ContainsKey(roomId))
        {
            return;
        }

        _aliveEnemiesByRoom[roomId] = Mathf.Max(0, _aliveEnemiesByRoom[roomId] - 1);
    }
}
