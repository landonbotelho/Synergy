using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerDash : MonoBehaviour
{
    [SerializeField] private float dashDistance = 7f;
    [SerializeField] private float wallStopOffset = 1f;
    [SerializeField] private float navMeshCheckStep = 0.25f;
    [SerializeField] private LayerMask blockingLayers = ~0;

    private CharacterController _controller;

    private void Awake()
    {
        _controller = GetComponentInChildren<CharacterController>();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
        {
            return;
        }

        TryDash();
    }

    private void TryDash()
    {
        if (_controller == null)
        {
            _controller = GetComponentInChildren<CharacterController>();
            if (_controller == null)
            {
                return;
            }
        }

        Vector3 direction = _controller.transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.Normalize();
        float distance = GetOpenDashDistance(direction);
        if (TryFindNavMeshDestination(_controller.transform.position, direction, distance, out Vector3 destination))
        {
            _controller.enabled = false;
            _controller.transform.position = destination;
            _controller.enabled = true;
            GameAudioManager.PlayDash(destination);
        }
    }

    private float GetOpenDashDistance(Vector3 direction)
    {
        Vector3 origin = _controller.transform.position + Vector3.up * (_controller.height * 0.5f);
        float radius = Mathf.Max(0.05f, _controller.radius * 0.8f);
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, dashDistance, blockingLayers, QueryTriggerInteraction.Ignore);
        float distance = dashDistance;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (hit == null || hit.transform.IsChildOf(_controller.transform) || hit.GetComponentInParent<EnemyVitals>() != null || hit.GetComponentInParent<ItemPickup>() != null || hit.GetComponentInParent<StoreInteract>() != null)
            {
                continue;
            }

            distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - wallStopOffset));
        }

        return distance;
    }

    private bool TryFindNavMeshDestination(Vector3 start, Vector3 direction, float maxDistance, out Vector3 destination)
    {
        for (float distance = maxDistance; distance > 0f; distance -= navMeshCheckStep)
        {
            Vector3 point = start + direction * distance;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, navMeshCheckStep, NavMesh.AllAreas))
            {
                destination = hit.position;
                return true;
            }
        }

        destination = start;
        return false;
    }
}
