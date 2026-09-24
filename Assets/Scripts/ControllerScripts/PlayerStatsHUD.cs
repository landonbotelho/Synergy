using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStatsHUD : MonoBehaviour
{
    [SerializeField] private Rect statsRect = new Rect(16f, 16f, 240f, 110f);

    private PlayerStats _playerStats;

    private void OnGUI()
    {
        _playerStats ??= GetComponentInChildren<PlayerStats>();
        if (_playerStats == null)
        {
            return;
        }

        GUI.Box(
            statsRect,
            "Stats\n" +
            $"Health: {_playerStats.CurrentHealth}/{_playerStats.MaxHealth}\n" +
            $"Strength: {_playerStats.Strength}\n" +
            $"Money: {_playerStats.Money}\n" +
            $"Key: {_playerStats.HasKey}\n" +
            $"Deaths: {_playerStats.DeathCount}"
        );
    }
}
