using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyChaseController : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField]
    private Transform targetOverride;
    [SerializeField]
    private float detectionRadius = 100f;
    [SerializeField]
    private bool requireLineOfSight = false;

    [Header("Pathing")]
    [SerializeField]
    private float repathInterval = 0.05f;
    [SerializeField]
    private float targetPredictionTime = 0.2f;
    [SerializeField]
    private float stoppingDistance = 1.25f;
    [SerializeField]
    private float destinationSampleRadius = 2f;

    [Header("Vision")]
    [SerializeField]
    private float eyeHeight = 1.2f;
    [SerializeField]
    private float targetEyeHeight = 1f;
    [SerializeField]
    private float visionRayRadius = 0.2f;
    [SerializeField]
    private LayerMask visibilityMask = ~0;

    [Header("Ranged Attack")]
    [SerializeField]
    private bool enableRangedAttack;
    [SerializeField]
    private GameObject fireProjectilePrefab;
    [SerializeField]
    private float fireRange = 5f;
    [SerializeField]
    private float fireCooldown = 3f;
    [SerializeField]
    private float fireProjectileSpeed = 12f;
    [SerializeField]
    private float fireProjectileLifetime = 5f;
    [SerializeField]
    private int fireDamage = 10;
    [SerializeField]
    private float fireSpawnHeight = 1f;

    private NavMeshAgent _agent;
    private Transform _target;
    private Collider[] _selfColliders;
    private Renderer[] _renderers;
    private Vector3 _lastTargetPosition;
    private Vector3 _targetVelocity;
    private float _nextRepathTime;
    private float _nextFireTime;
    private bool _useLongRangeFireSound;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _selfColliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        ConfigureAgent();
    }

    private void Update()
    {
        // Keep the NavMeshAgent settings synced with the current attack mode.
        ConfigureAgent();
        // Find or reuse the current player target.
        ResolveTarget();

        if (_target == null)
        {
            StopChasing();
            return;
        }
        // Estimate the player's recent movement so the enemy can chase slightly ahead.
        UpdateTargetVelocity();

        // Stop if the player is outside detection range or blocked by line of sight.
        if (!CanDetectTarget(_target))
        {
            StopChasing();
            return;
        }

        //nake sure spawned enemy is on the nav mesh
        if (!_agent.isOnNavMesh)
        {
            return;
        }

        //ranged enemies stop chasing and attack when player is close enough.
        if (enableRangedAttack && TryHandleRangedAttack())
        {
            return;
        }

        //make the enemy chase the player again after a set amount of time
        //adjusting to lomger time intervals seems to make movement smoother and less skippy
        if (Time.time >= _nextRepathTime)
        {
            //get players position to walk towards
            Vector3 chasePoint = _target.position + _targetVelocity * targetPredictionTime;

            //verifies player is on  walkable ground in the game environemnt
            //needed bec the players next position being located considers velocity which gets a position slightly ahead of his legitmate location
            if (TryGetDestination(chasePoint, out Vector3 destination))
            {
                _agent.isStopped = false;
                //moves enemy to the player
                _agent.SetDestination(destination);
            }
            else
            {
                //stops chasing if no valid position is found
                StopChasing();
            }
            //updates time  interval
            _nextRepathTime = Time.time + repathInterval;
        }
    }

    public void ConfigureRangedAttack(GameObject projectilePrefab, float range, float projectileSpeed, float cooldown, float lifetime, int damage)
    {
        fireProjectilePrefab = projectilePrefab;
        fireRange = Mathf.Max(0.1f, range);
        fireProjectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        fireCooldown = Mathf.Max(0.1f, cooldown);
        fireProjectileLifetime = Mathf.Max(0.1f, lifetime);
        fireDamage = Mathf.Max(1, damage);
        enableRangedAttack = projectilePrefab != null;
    }

    public void ConfigureFireSoundType(bool useLongRangeSound)
    {
        _useLongRangeFireSound = useLongRangeSound;
    }

    private void ConfigureAgent()
    {
        if (_agent == null)
        {
            return;
        }

        _agent.stoppingDistance = enableRangedAttack ? Mathf.Max(stoppingDistance, fireRange - 0.5f) : stoppingDistance;
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

        PlayerStats[] playerStatsComponents = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        for (int i = 0; i < playerStatsComponents.Length; i++)
        {
            PlayerStats playerStats = playerStatsComponents[i];
            if (playerStats != null && playerStats.GetComponent<CharacterController>() != null)
            {
                _target = playerStats.transform;
                return;
            }
        }

        PlayerStats playerStatsFallback = FindFirstObjectByType<PlayerStats>();
        if (playerStatsFallback != null)
        {
            _target = playerStatsFallback.transform;
            return;
        }

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            _target = playerObject.transform;
        }
    }

    private bool CanDetectTarget(Transform candidate)
    {
        Vector3 flatOffset = candidate.position - transform.position;
        flatOffset.y = 0f;

        float distance = flatOffset.magnitude;
        if (distance > detectionRadius)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            return true;
        }

        return HasLineOfSight(candidate);
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

    private bool TryHandleRangedAttack()
    {
        if (fireProjectilePrefab == null || _target == null)
        {
            return false;
        }

        Vector3 flatOffset = _target.position - transform.position;
        flatOffset.y = 0f;
        float distanceToTarget = flatOffset.magnitude;
        if (distanceToTarget > fireRange)
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
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 12f * Time.deltaTime);
        }

        if (Time.time < _nextFireTime)
        {
            return true;
        }

        FireProjectile();
        _nextFireTime = Time.time + fireCooldown;
        return true;
    }

    private void FireProjectile()
    {
        Vector3 targetPosition = _target.position + Vector3.up * targetEyeHeight;
        Vector3 aimDirection = targetPosition - transform.position;
        Vector3 spawnPosition = GetFireSpawnPosition(aimDirection);
        Vector3 direction = targetPosition - spawnPosition;
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

        projectile.Configure(gameObject, direction, fireProjectileSpeed, fireDamage, fireProjectileLifetime);

        if (_useLongRangeFireSound)
        {
            GameAudioManager.PlayLongEnemyFire(spawnPosition);
        }
        else
        {
            GameAudioManager.PlayShortEnemyFire(spawnPosition);
        }
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
            Renderer currentRenderer = _renderers[i];
            if (currentRenderer != null)
            {
                combinedBounds.Encapsulate(currentRenderer.bounds);
            }
        }

        Vector3 center = combinedBounds.center;
        center.y = Mathf.Max(center.y, combinedBounds.min.y + fireSpawnHeight);
        return center + forward * Mathf.Max(0.35f, combinedBounds.extents.z + 0.1f);
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
