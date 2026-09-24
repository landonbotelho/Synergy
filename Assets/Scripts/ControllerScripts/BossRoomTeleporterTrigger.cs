using UnityEngine;

[DisallowMultipleComponent]
public class BossRoomTeleporterTrigger : MonoBehaviour
{
    private BossRoomTeleporter _teleporter;

    public void Initialize(BossRoomTeleporter teleporter)
    {
        _teleporter = teleporter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_teleporter == null || other == null)
        {
            return;
        }

        PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
        if (playerStats != null)
        {
            _teleporter.TryTeleport(playerStats);
        }
    }
}
