using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyVitals))]
public class enemyFinalBoss : MonoBehaviour
{
    private enum BossPhase
    {
        PhaseOne,
        PhaseTwo,
        PhaseThree
    }

    [System.Serializable]
    private class PhaseSettings
    {
        public float fireRange = 10f;
        public float fireCooldown = 3f;
        public float fireProjectileSpeed = 12f;
        public float fireProjectileLifetime = 5f;
        public int fireDamage = 10;
        public float angularTurnSpeed = 720f;
    }

    [Header("Targeting")]
    [SerializeField] private Transform targetOverride;
    [SerializeField] private float detectionRadius = 100f;
    [SerializeField] private bool requireLineOfSight = false;

    [Header("Pathing")]
    [SerializeField] private float repathInterval = 0.05f;
    [SerializeField] private float targetPredictionTime = 0.2f;
    [SerializeField] private float stoppingDistance = 1.25f;
    [SerializeField] private float destinationSampleRadius = 2f;

    [Header("Vision")]
    [SerializeField] private float eyeHeight = 1.2f;
    [SerializeField] private float targetEyeHeight = 1f;
    [SerializeField] private float visionRayRadius = 0.2f;
    [SerializeField] private LayerMask visibilityMask = ~0;

    [Header("Boss Health")]
    [SerializeField] private int maxHealth = 1500;
    [SerializeField] private int phaseTwoHealthThreshold = 1000;
    [SerializeField] private int phaseThreeHealthThreshold = 500;

    [Header("Projectile")]
    [SerializeField] private GameObject fireProjectilePrefab;
    [SerializeField] private float fireSpawnHeight = 1f;

    [Header("Phase One")]
    [SerializeField] private PhaseSettings phaseOne = new PhaseSettings
    {
        fireRange = 10f,
        fireCooldown = 4f,
        fireProjectileSpeed = 8f,
        fireProjectileLifetime = 5f,
        fireDamage = 15,
        angularTurnSpeed = 720f
    };

    [Header("Phase Two")]
    [SerializeField] private PhaseSettings phaseTwo = new PhaseSettings
    {
        fireRange = 10f,
        fireCooldown = 2.5f,
        fireProjectileSpeed = 12f,
        fireProjectileLifetime = 5f,
        fireDamage = 20,
        angularTurnSpeed = 900f
    };

    [Header("Phase Three")]
    [SerializeField] private PhaseSettings phaseThree = new PhaseSettings
    {
        fireRange = 10f,
        fireCooldown = 1.75f,
        fireProjectileSpeed = 16f,
        fireProjectileLifetime = 5f,
        fireDamage = 25,
        angularTurnSpeed = 1080f
    };
    [SerializeField] private int phaseThreeProjectileCount = 8;
    [SerializeField] private GameObject shortRangeEnemyPrefab;
    [SerializeField] private int phaseThreeSpawnCount = 4;
    [SerializeField] private Transform[] phaseThreeAddSpawnPoints;

    private NavMeshAgent _agent;
    private EnemyVitals _enemyVitals;
    private Transform _target;
    private Collider[] _selfColliders;
    private Renderer[] _renderers;
    private Vector3 _lastTargetPosition;
    private Vector3 _targetVelocity;
    private float _nextRepathTime;
    private float _nextFireTime;
    private bool _spawnedPhaseThreeAdds;
    private BossPhase _currentPhase;
    private RandomSpawner _enemySpawner;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _enemyVitals = GetComponent<EnemyVitals>();
        _selfColliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _enemySpawner = FindFirstObjectByType<RandomSpawner>();

        if (_enemyVitals != null)
        {
            _enemyVitals.Configure(maxHealth);
        }
    }

    private void OnEnable()
    {
        _currentPhase = GetCurrentPhase();
        ConfigureAgent(GetSettingsForPhase(_currentPhase));
    }

    private void Update()
    {
        PhaseSettings settings = GetSettingsForPhase(GetCurrentPhase());
        ConfigureAgent(settings);
        ResolveTarget();

        if (_target == null)
        {
            StopChasing();
            return;
        }

        UpdateTargetVelocity();

        if (!CanDetectTarget(_target))
        {
            StopChasing();
            return;
        }

        if (!_agent.isOnNavMesh)
        {
            return;
        }

        if (TryHandleRangedAttack(settings))
        {
            return;
        }

        if (Time.time < _nextRepathTime)
        {
            return;
        }

        Vector3 chasePoint = _target.position + _targetVelocity * targetPredictionTime;
        if (TryGetDestination(chasePoint, out Vector3 destination))
        {
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }
        else
        {
            StopChasing();
        }

        _nextRepathTime = Time.time + repathInterval;
    }

    private BossPhase GetCurrentPhase()
    {
        if (_enemyVitals == null)
        {
            return BossPhase.PhaseOne;
        }

        BossPhase nextPhase = _enemyVitals.CurrentHealth <= phaseThreeHealthThreshold
            ? BossPhase.PhaseThree
            : _enemyVitals.CurrentHealth <= phaseTwoHealthThreshold
                ? BossPhase.PhaseTwo
                : BossPhase.PhaseOne;

        if (nextPhase != _currentPhase)
        {
            _currentPhase = nextPhase;
            if (_currentPhase == BossPhase.PhaseThree)
            {
                SpawnPhaseThreeAdds();
            }
        }

        return _currentPhase;
    }

    private PhaseSettings GetSettingsForPhase(BossPhase phase)
    {
        switch (phase)
        {
            case BossPhase.PhaseTwo:
                return phaseTwo;
            case BossPhase.PhaseThree:
                return phaseThree;
            default:
                return phaseOne;
        }
    }

    private void ConfigureAgent(PhaseSettings settings)
    {
        if (_agent == null || settings == null)
        {
            return;
        }

        _agent.stoppingDistance = Mathf.Max(stoppingDistance, settings.fireRange - 0.5f);
        _agent.angularSpeed = settings.angularTurnSpeed;
        _agent.autoBraking = false;
        _agent.autoRepath = true;
        _agent.updateRotation = true;
        _agent.updateUpAxis = true;
    }

    private void ResolveTarget()
    {
        if (targetOverride != null)
        {
            _target = targetOverride;
            return;
        }

        if (_target != null)
        {
            return;
        }

        controlledPlayerCombat playerCombat = FindFirstObjectByType<controlledPlayerCombat>();
        if (playerCombat != null)
        {
            _target = playerCombat.transform;
            return;
        }

        CharacterController playerController = FindFirstObjectByType<CharacterController>();
        if (playerController != null)
        {
            _target = playerController.transform;
            return;
        }

        PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            _target = playerStats.transform;
        }
    }

    private bool CanDetectTarget(Transform candidate)
    {
        Vector3 flatOffset = candidate.position - transform.position;
        flatOffset.y = 0f;

        if (flatOffset.magnitude > detectionRadius)
        {
            return false;
        }

        return !requireLineOfSight || HasLineOfSight(candidate);
    }

    private bool HasLineOfSight(Transform candidate)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = candidate.position + Vector3.up * targetEyeHeight;
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;

        if (distance <= Mathf.Epsilon)
        {
            return true;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            visionRayRadius,
            direction.normalized,
            distance,
            visibilityMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
        {
            return true;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || IsSelf(hitTransform))
            {
                continue;
            }

            return hitTransform == candidate || hitTransform.IsChildOf(candidate);
        }

        return true;
    }

    private bool TryHandleRangedAttack(PhaseSettings settings)
    {
        if (fireProjectilePrefab == null || _target == null || settings == null)
        {
            return false;
        }

        Vector3 flatOffset = _target.position - transform.position;
        flatOffset.y = 0f;
        if (flatOffset.magnitude > settings.fireRange)
        {
            return false;
        }

        _agent.isStopped = true;
        if (_agent.isOnNavMesh)
        {
            _agent.ResetPath();
        }

        if (flatOffset.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(flatOffset.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, settings.angularTurnSpeed * Time.deltaTime);
        }

        if (Time.time < _nextFireTime)
        {
            return true;
        }

        if (_currentPhase == BossPhase.PhaseThree)
        {
            FireBurst(settings);
        }
        else
        {
            FireAtTarget(settings);
        }

        _nextFireTime = Time.time + settings.fireCooldown;
        return true;
    }

    private void FireAtTarget(PhaseSettings settings)
    {
        Vector3 targetPosition = _target.position + Vector3.up * targetEyeHeight;
        Vector3 aimDirection = targetPosition - transform.position;
        Vector3 spawnPosition = GetFireSpawnPosition(aimDirection);
        SpawnProjectile(spawnPosition, targetPosition - spawnPosition, settings);
    }

    private void FireBurst(PhaseSettings settings)
    {
        Vector3 flatForward = _target != null ? _target.position - transform.position : transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude <= 0.001f)
        {
            flatForward = transform.forward;
            flatForward.y = 0f;
        }

        flatForward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;
        Vector3 targetPosition = _target.position + Vector3.up * targetEyeHeight;
        Vector3 spawnPosition = GetFireSpawnPosition(targetPosition - transform.position);
        float horizontalAimDistance = Mathf.Max(1f, Vector3.Distance(
            new Vector3(spawnPosition.x, 0f, spawnPosition.z),
            new Vector3(targetPosition.x, 0f, targetPosition.z)
        ));
        float verticalAimOffset = targetPosition.y - spawnPosition.y;

        Vector3[] directions =
        {
            GetPhaseThreeBurstDirection(flatForward, horizontalAimDistance, verticalAimOffset),
            GetPhaseThreeBurstDirection(-flatForward, horizontalAimDistance, verticalAimOffset),
            GetPhaseThreeBurstDirection(right, horizontalAimDistance, verticalAimOffset),
            GetPhaseThreeBurstDirection(-right, horizontalAimDistance, verticalAimOffset),
            GetPhaseThreeBurstDirection((flatForward + right).normalized, horizontalAimDistance, verticalAimOffset),
            GetPhaseThreeBurstDirection((flatForward - right).normalized, horizontalAimDistance, verticalAimOffset),
            GetPhaseThreeBurstDirection((-flatForward + right).normalized, horizontalAimDistance, verticalAimOffset),
            GetPhaseThreeBurstDirection((-flatForward - right).normalized, horizontalAimDistance, verticalAimOffset)
        };

        int projectileCount = Mathf.Clamp(phaseThreeProjectileCount, 1, directions.Length);
        for (int i = 0; i < projectileCount; i++)
        {
            SpawnProjectile(spawnPosition, directions[i], settings);
        }
    }

    private Vector3 GetPhaseThreeBurstDirection(Vector3 flatDirection, float horizontalDistance, float verticalOffset)
    {
        return (flatDirection.normalized * horizontalDistance + Vector3.up * verticalOffset).normalized;
    }

    private void SpawnProjectile(Vector3 spawnPosition, Vector3 direction, PhaseSettings settings)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        GameObject projectileInstance = Instantiate(
            fireProjectilePrefab,
            spawnPosition,
            Quaternion.LookRotation(direction.normalized)
        );

        EnemyFireProjectile projectile = projectileInstance.GetComponent<EnemyFireProjectile>();
        if (projectile == null)
        {
            projectile = projectileInstance.AddComponent<EnemyFireProjectile>();
        }

        projectile.Configure(gameObject, direction, settings.fireProjectileSpeed, settings.fireDamage, settings.fireProjectileLifetime);
    }

    private Vector3 GetFireSpawnPosition(Vector3 aimDirection)
    {
        Vector3 forward = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : transform.forward;
        Vector3 spawnPosition = transform.position + Vector3.up * fireSpawnHeight + forward * 0.5f;

        if (_renderers == null || _renderers.Length == 0)
        {
            return spawnPosition;
        }

        Bounds combinedBounds = _renderers[0].bounds;
        for (int i = 1; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                combinedBounds.Encapsulate(_renderers[i].bounds);
            }
        }

        Vector3 center = combinedBounds.center;
        center.y = Mathf.Max(center.y, combinedBounds.min.y + fireSpawnHeight);
        return center + forward * Mathf.Max(0.35f, combinedBounds.extents.z + 0.1f);
    }

    private void SpawnPhaseThreeAdds()
    {
        if (_spawnedPhaseThreeAdds || shortRangeEnemyPrefab == null || phaseThreeSpawnCount <= 0)
        {
            return;
        }

        _spawnedPhaseThreeAdds = true;

        for (int i = 0; i < phaseThreeSpawnCount; i++)
        {
            if (TryGetPhaseThreeAddSpawnPosition(out Vector3 spawnPosition))
            {
                SpawnShortRangeAdd(spawnPosition);
            }
        }
    }

    private bool TryGetPhaseThreeAddSpawnPosition(out Vector3 spawnPosition)
    {
        spawnPosition = transform.position;
        if (phaseThreeAddSpawnPoints == null || phaseThreeAddSpawnPoints.Length == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, phaseThreeAddSpawnPoints.Length);
        for (int i = 0; i < phaseThreeAddSpawnPoints.Length; i++)
        {
            Transform spawnPoint = phaseThreeAddSpawnPoints[(startIndex + i) % phaseThreeAddSpawnPoints.Length];
            if (spawnPoint == null)
            {
                continue;
            }

            spawnPosition = spawnPoint.position;
            return true;
        }

        return false;
    }

    private void SpawnShortRangeAdd(Vector3 position)
    {
        if (shortRangeEnemyPrefab == null)
        {
            return;
        }

        if (_enemySpawner != null)
        {
            _enemySpawner.SpawnEnemyInstance(shortRangeEnemyPrefab, position, 1, -1, 5f);
            return;
        }

        GameObject enemy = Instantiate(shortRangeEnemyPrefab, position, Quaternion.identity);
        NavMeshAgent addAgent = enemy.GetComponent<NavMeshAgent>();
        if (addAgent == null)
        {
            addAgent = enemy.AddComponent<NavMeshAgent>();
        }

        EnemyVitals addVitals = enemy.GetComponent<EnemyVitals>();
        if (addVitals == null)
        {
            Debug.LogWarning($"Boss add prefab '{shortRangeEnemyPrefab.name}' is missing EnemyVitals.", enemy);
        }

        EnemyChaseController chaseController = enemy.GetComponent<EnemyChaseController>();
        if (chaseController == null)
        {
            chaseController = enemy.AddComponent<EnemyChaseController>();
        }
    }

    private bool TryGetDestination(Vector3 desiredPoint, out Vector3 sampledDestination)
    {
        sampledDestination = transform.position;

        if (!NavMesh.SamplePosition(desiredPoint, out NavMeshHit navHit, destinationSampleRadius, NavMesh.AllAreas))
        {
            return false;
        }

        sampledDestination = navHit.position;
        return true;
    }

    private void UpdateTargetVelocity()
    {
        if (_target == null)
        {
            _targetVelocity = Vector3.zero;
            _lastTargetPosition = Vector3.zero;
            return;
        }

        if (_lastTargetPosition == Vector3.zero)
        {
            _lastTargetPosition = _target.position;
            _targetVelocity = Vector3.zero;
            return;
        }

        Vector3 velocity = (_target.position - _lastTargetPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        velocity.y = 0f;
        _targetVelocity = Vector3.Lerp(_targetVelocity, velocity, 12f * Time.deltaTime);
        _lastTargetPosition = _target.position;
    }

    private void StopChasing()
    {
        if (_agent == null || !_agent.enabled)
        {
            return;
        }

        _agent.isStopped = true;
        if (_agent.isOnNavMesh)
        {
            _agent.ResetPath();
        }
    }

    private bool IsSelf(Transform hitTransform)
    {
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
        {
            return true;
        }

        if (_selfColliders == null)
        {
            return false;
        }

        for (int i = 0; i < _selfColliders.Length; i++)
        {
            Collider selfCollider = _selfColliders[i];
            if (selfCollider != null && selfCollider.transform == hitTransform)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
