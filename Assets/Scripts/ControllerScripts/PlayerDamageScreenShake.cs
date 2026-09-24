using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDamageScreenShake : MonoBehaviour
{
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.12f;

    private Transform _cameraTransform;
    private Vector3 _lastOffset;
    private float _shakeTimeRemaining;

    private void OnEnable()
    {
        PlayerStats playerStats = GetComponentInChildren<PlayerStats>();
        Camera mainCamera = GetComponentInChildren<Camera>();
        _cameraTransform = mainCamera != null ? mainCamera.transform : null;

        if (playerStats != null)
        {
            playerStats.DamageTaken += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        PlayerStats playerStats = GetComponentInChildren<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.DamageTaken -= HandleDamageTaken;
        }

        if (_cameraTransform != null)
        {
            _cameraTransform.localPosition -= _lastOffset;
            _lastOffset = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null)
        {
            return;
        }

        _cameraTransform.localPosition -= _lastOffset;
        _lastOffset = Vector3.zero;

        if (_shakeTimeRemaining <= 0f)
        {
            return;
        }

        _shakeTimeRemaining -= Time.deltaTime;
        _lastOffset = Random.insideUnitSphere * shakeMagnitude;
        _lastOffset.z = 0f;
        _cameraTransform.localPosition += _lastOffset;
    }

    private void HandleDamageTaken(int damageAmount)
    {
        if (damageAmount > 0)
        {
            _shakeTimeRemaining = shakeDuration;
        }
    }
}
