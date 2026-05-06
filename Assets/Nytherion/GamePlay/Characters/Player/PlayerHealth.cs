using UnityEngine;
using System;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        public static event Action<float, float> OnHealthChanged;
        public static event Action OnPlayerDied;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }

        public void InitializeHealth(float health)
        {
            MaxHealth = health;
            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
        public void TakeDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
        public void UpdateMaxHealth(float newMaxHealth)
        {
            if (Mathf.Approximately(MaxHealth, newMaxHealth)) return; // 무한 루프 방지

            MaxHealth = newMaxHealth;
            CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private void Die()
        {
            Debug.Log("플레이어 사망");
            OnPlayerDied?.Invoke();
        }
    }
}
