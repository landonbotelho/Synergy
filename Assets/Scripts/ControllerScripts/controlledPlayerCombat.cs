using System.Collections.Generic;
using UnityEngine;

public class controlledPlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private Transform lightAttackHitOrigin;

    [Header("Attack State")]
    [Tooltip("Animator state name used for the light attack.")]
    [SerializeField]
    private string lightAttackStateName = "A_Attack_LightCombo01A_Sword";

    [Header("Hit Detection")]
    [SerializeField]
    private float lightAttackHitRadius = 1.1f;
    [SerializeField]
    [Range(0f, 1f)]
    private float lightAttackHitWindowStart = 0.2f;
    [SerializeField]
    [Range(0f, 1f)]
    private float lightAttackHitWindowEnd = 0.65f;
    [SerializeField]
    private LayerMask enemyHitLayers = ~0;

    private PlayerStats _playerStats;
    private readonly Collider[] _hitResults = new Collider[16];
    private readonly HashSet<GameObject> _hitEnemiesThisAttack = new HashSet<GameObject>();
    private int _lightAttackStateHash;
    private bool _wasInLightAttack;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        _playerStats = GetComponent<PlayerStats>();

        if (lightAttackHitOrigin == null)
        {
            Transform prop = transform.Find("PolygonSyntyCharacter/Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R/Hand_R/Prop_R");
            if (prop != null)
            {
                lightAttackHitOrigin = prop;
            }
        }

        _lightAttackStateHash = Animator.StringToHash(lightAttackStateName);
    }

    private void Update()
    {
        if (_playerStats == null)
        {
            _playerStats = GetComponent<PlayerStats>();
        }

        if (animator == null || lightAttackHitOrigin == null)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool isInLightAttack = stateInfo.shortNameHash == _lightAttackStateHash || stateInfo.IsName("Base Layer." + lightAttackStateName);

        if (isInLightAttack && !_wasInLightAttack)
        {
            _hitEnemiesThisAttack.Clear();
        }

        if (isInLightAttack)
        {
            float normalizedTime = stateInfo.normalizedTime % 1f;
            if (normalizedTime >= lightAttackHitWindowStart && normalizedTime <= lightAttackHitWindowEnd)
            {
                TryHitEnemies();
            }
        }

        _wasInLightAttack = isInLightAttack;
    }

    private void TryHitEnemies()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            lightAttackHitOrigin.position,
            lightAttackHitRadius,
            _hitResults,
            enemyHitLayers,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _hitResults[i];
            if (hit == null)
            {
                continue;
            }

            EnemyVitals enemyVitals = hit.GetComponentInParent<EnemyVitals>();
            if (enemyVitals == null)
            {
                continue;
            }

            GameObject enemyRoot = enemyVitals.gameObject;
            if (_hitEnemiesThisAttack.Contains(enemyRoot))
            {
                continue;
            }

            _hitEnemiesThisAttack.Add(enemyRoot);
            int damage = _playerStats != null ? _playerStats.Strength : 100;
            enemyVitals.TakeDamage(damage);
            GameAudioManager.PlayEnemyHit(enemyVitals.transform.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (lightAttackHitOrigin == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(lightAttackHitOrigin.position, lightAttackHitRadius);
    }
}
