using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyVitals : MonoBehaviour
{
    [SerializeField]
    private int maxHealth = 100;
    [SerializeField]
    private int currentHealth = 100;
    [SerializeField]
    private int spawnedRoomId = -1;

    public static event Action<EnemyVitals> EnemyDied;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int SpawnedRoomId => spawnedRoomId;

    public void Configure(int healthAmount)
    {
        maxHealth = Mathf.Max(1, healthAmount);
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    [SerializeField] healthBar healthBar;

    private void Awake()
    {
        healthBar = GetComponentInChildren<healthBar>(true);
    }

    private void Start()
    {
        UpdateHealthBar();
    }

    public void SetSpawnedRoomId(int roomId)
    {
        spawnedRoomId = roomId;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        UpdateHealthBar();

        if (currentHealth == 0)
        {
            bool isBoss = GetComponent<enemyFinalBoss>() != null;
            if (isBoss)
            {
                GameAudioManager.PlayBossDeath(transform.position);
            }
            else
            {
                GameAudioManager.PlayEnemyDeath(transform.position);
            }

            if (isBoss)
            {
                Story.ShowEnding();
                GameAudioManager.StopMusic();
            }

            EnemyDied?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.updateHealthBar(currentHealth, maxHealth);
        }
    }
}
