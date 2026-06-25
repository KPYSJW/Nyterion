using UnityEngine;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.GamePlay.Combat
{
    public abstract class StatusEffect
    {
        public abstract string EffectId { get; }
        public virtual Color EffectColor => Color.white;
        public Sprite EffectIcon { get; set; }
        public float Duration { get; protected set; }
        public float Timer { get; protected set; }

        protected EnemyBase target;
        protected StatusEffectManager manager;

        public virtual void Initialize(EnemyBase target, StatusEffectManager manager, float duration)
        {
            this.target = target;
            this.manager = manager;
            this.Duration = duration;
            this.Timer = duration;
        }

        public void ModifyDuration(float newDuration)
        {
            this.Duration = newDuration;
            this.Timer = newDuration;
        }

        public virtual void ResetDuration()
        {
            Timer = Duration;
        }

        public virtual void OnStack(StatusEffect newEffect)
        {
            // 스택 중첩 시 처리할 동작을 하위 클래스에서 오버라이드
        }

        public virtual void UpdateTimer(float deltaTime)
        {
            Timer -= deltaTime;
        }

        public abstract void OnApply();
        public abstract void OnUpdate(float deltaTime);
        public abstract void OnRemove();
    }
}
