using UnityEngine;
using System;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        public static event Action<float, float> OnHealthChanged; // 현재 HP, 최대 HP

        [SerializeField] private float maxHealth = 100f;
        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void Die()
        {
            Debug.Log("플레이어 사망");
            // 사망 애니메이션, 재시작 처리 등
        }
    }
}
