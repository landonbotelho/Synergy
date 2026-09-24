using UnityEngine;

[DisallowMultipleComponent]
public class EnemyFireProjectile : MonoBehaviour
{
    [SerializeField]
    private float speed = 12f;
    [SerializeField]
    private int damage = 10;
    [SerializeField]
    private float lifetime = 5f;

    private Rigidbody _rigidbody;
    private Collider _projectileCollider;
    private GameObject _owner;
    private Vector3 _direction = Vector3.forward;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = false;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _projectileCollider = GetComponent<Collider>();
        if (_projectileCollider == null)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.25f;
            _projectileCollider = sphereCollider;
        }

        _projectileCollider.isTrigger = true;
    }

    public void Configure(GameObject owner, Vector3 direction, float projectileSpeed, int projectileDamage, float projectileLifetime)
    {
        _owner = owner;
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        speed = Mathf.Max(0.1f, projectileSpeed);
        damage = Mathf.Max(1, projectileDamage);
        lifetime = Mathf.Max(0.1f, projectileLifetime);

        if (_owner != null && _projectileCollider != null)
        {
            Collider[] ownerColliders = _owner.GetComponentsInChildren<Collider>();
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                Collider ownerCollider = ownerColliders[i];
                if (ownerCollider != null)
                {
                    Physics.IgnoreCollision(_projectileCollider, ownerCollider, true);
                }
            }
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = _direction * speed;
        }

        transform.forward = _direction;
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (_owner != null && (other.gameObject == _owner || other.transform.IsChildOf(_owner.transform)))
        {
            return;
        }

        EnemyVitals enemyVitals = other.GetComponentInParent<EnemyVitals>();
        if (enemyVitals != null)
        {
            return;
        }

        PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
        if (playerStats != null)
        {
            SimpleSwordCombatController combatController = playerStats.GetComponentInChildren<SimpleSwordCombatController>();
            if (combatController != null && combatController.IsBlocking)
            {
                Destroy(gameObject);
                return;
            }

            playerStats.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
