using UnityEngine;
using System;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        public static event Action<float, float> OnHealthChanged;
        public static event Action<float> OnPlayerDamaged;
        public static event Action OnPlayerDied;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }

        public bool IsInvulnerable { get; private set; }

        private IProgressionManager progressionManager;

        [Inject]
        public void Construct(IProgressionManager progressionManager)
        {
            this.progressionManager = progressionManager;
        }

        public void InitializeHealth(float health)
        {
            MaxHealth = health;
            CurrentHealth = MaxHealth;
            IsInvulnerable = false;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetInvulnerable(bool value)
        {
            IsInvulnerable = value;
        }

        public void TakeDamage(float amount)
        {
            if (IsInvulnerable || amount <= 0f) return;

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            float appliedDamage = previousHealth - CurrentHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (appliedDamage > 0f)
            {
                OnPlayerDamaged?.Invoke(appliedDamage);
            }

            // 받은 데미지 진척도 업데이트
            progressionManager?.ProcessAction(ProgressionType.TakeDamage, (int)appliedDamage);

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
