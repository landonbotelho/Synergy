using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
    private struct PersistedProgress
    {
        public int baseStrength;
        public int baseHealth;
        public int currentHealth;
        public int money;
        public int deathCount;
    }

    private static bool _hasPersistedProgress;
    private static PersistedProgress _persistedProgress;

    [Header("Base Stats")]
    [SerializeField]
    private int baseStrength = 100;
    [SerializeField]
    private int baseHealth = 100;

    [Header("Runtime")]
    [SerializeField]
    private int deathCount;
    [SerializeField]
    private int currentHealth;
    [SerializeField]
    private int money;
    [SerializeField]
    private bool hasKey;

    public event Action<int> DamageTaken;

    public int Strength => baseStrength;
    public int MaxHealth => baseHealth;
    public int CurrentHealth => currentHealth;
    public int DeathCount => deathCount;
    public int Money => money;
    public bool HasKey => hasKey;

    private void Awake()
    {
        if (_hasPersistedProgress)
        {
            baseStrength = _persistedProgress.baseStrength;
            baseHealth = _persistedProgress.baseHealth;
            currentHealth = _persistedProgress.currentHealth;
            money = _persistedProgress.money;
            deathCount = _persistedProgress.deathCount;
            hasKey = false;
            _hasPersistedProgress = false;
        }

        baseStrength = Mathf.Max(0, baseStrength);
        baseHealth = Mathf.Max(1, baseHealth);

        if (currentHealth <= 0 || currentHealth > baseHealth)
        {
            currentHealth = baseHealth;
        }

        Debug.Log(GetStatsSummary());
    }

    public void AddStrength(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        baseStrength += amount;
        NotifyStatsChanged();
    }

    public void AddMaxHealth(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        baseHealth += amount;
        currentHealth = Mathf.Min(baseHealth, currentHealth + amount);
        NotifyStatsChanged();
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Min(baseHealth, currentHealth + amount);
        NotifyStatsChanged();
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        money += amount;
        NotifyStatsChanged();
    }

    public bool SpendMoney(int amount)
    {
        if (amount <= 0 || money < amount)
        {
            return false;
        }

        money -= amount;
        NotifyStatsChanged();
        return true;
    }

    public void FillHealthToFull()
    {
        currentHealth = baseHealth;
        NotifyStatsChanged();
    }

    public void CollectKey()
    {
        if (hasKey)
        {
            return;
        }

        hasKey = true;
        NotifyStatsChanged();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        DamageTaken?.Invoke(amount);
        GameAudioManager.PlayPlayerDamage(transform.position);
        NotifyStatsChanged();

        if (currentHealth == 0)
        {
            Die();
        }
    }

    public void Die()
    {
        GameAudioManager.PlayPlayerDeath(transform.position);
        GameAudioManager.PlayDungeonMusic();
        deathCount++;
        currentHealth = baseHealth;
        PersistProgressForReload();
        Story.QueueDeathMessage();
        NotifyStatsChanged();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void CopyStateTo(PlayerStats target)
    {
        if (target == null)
        {
            return;
        }

        target.baseStrength = baseStrength;
        target.baseHealth = baseHealth;
        target.currentHealth = Mathf.Clamp(currentHealth, 0, baseHealth);
        target.money = money;
        target.hasKey = hasKey;
        target.deathCount = deathCount;
        target.NotifyStatsChanged();
    }

    private void NotifyStatsChanged()
    {
        Debug.Log(GetStatsSummary());
    }

    private void PersistProgressForReload()
    {
        _persistedProgress = new PersistedProgress
        {
            baseStrength = baseStrength,
            baseHealth = baseHealth,
            currentHealth = baseHealth,
            money = money,
            deathCount = deathCount
        };

        _hasPersistedProgress = true;
    }

    private string GetStatsSummary()
    {
        return $"Player Stats -> Health: {currentHealth}/{baseHealth}, Strength: {baseStrength}, Money: {money}, Key: {hasKey}, Deaths: {deathCount}";
    }
}
