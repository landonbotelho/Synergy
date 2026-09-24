using UnityEngine;

[DisallowMultipleComponent]
public class ItemPickup : MonoBehaviour
{
    public enum PickupType
    {
        Strength,
        Health,
        Money,
        Chest,
        Key
    }

    [SerializeField]
    private PickupType pickupType;
    [SerializeField]
    private int amount = 10;

    public void Configure(PickupType type, int value)
    {
        pickupType = type;
        amount = Mathf.Max(1, value);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
        if (playerStats == null)
        {
            return;
        }

        switch (pickupType)
        {
            case PickupType.Strength:
                playerStats.AddStrength(amount);
                GameAudioManager.PlayDamagePickup(transform.position);
                break;
            case PickupType.Health:
                playerStats.AddMaxHealth(amount);
                GameAudioManager.PlayHealthPickup(transform.position);
                break;
            case PickupType.Money:
                playerStats.AddMoney(amount);
                GameAudioManager.PlayMoneyPickup(transform.position);
                break;
            case PickupType.Chest:
                playerStats.AddMoney(amount);
                GameAudioManager.PlayChestOpen(transform.position);
                break;
            case PickupType.Key:
                playerStats.CollectKey();
                break;
        }

        Destroy(gameObject);
    }
}
