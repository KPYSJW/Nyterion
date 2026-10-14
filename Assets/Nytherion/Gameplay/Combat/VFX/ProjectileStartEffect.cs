using System;
using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public class ProjectileStartEffect : MonoBehaviour
    {
        [SerializeField] private AnimationClip animationClip;
        public float Duration => animationClip != null ? animationClip.length : 0f;
        private ObjectPoolManager effectPool;
        private Action pendingFire;
        private Func<bool> canContinue;

        public void Play(ObjectPoolManager pool, Action fire, Func<bool> canContinue)
        {
            ClearPendingFire();
            Animator animator = GetComponent<Animator>();
            animator.Rebind();
            effectPool = pool;
            pendingFire = fire;
            this.canContinue = canContinue;
            animator.Update(0f);
        }

        private void Update()
        {
            if (pendingFire != null && !canContinue()) ReturnEffect();
        }

        // 시작 애니메이션 마지막의 Animation Event가 호출합니다.
        public void OnAnimationComplete()
        {
            if (pendingFire == null) return;
            Action fire = canContinue() ? pendingFire : null;
            ReturnEffect();
            fire?.Invoke();
        }

        private void ReturnEffect()
        {
            ObjectPoolManager pool = effectPool;
            ClearPendingFire();
            if (pool != null) pool.ReturnToPool(gameObject.name.Replace("(Clone)", "").Trim(), gameObject);
            else gameObject.SetActive(false);
        }

        private void ClearPendingFire()
        {
            pendingFire = null;
            canContinue = null;
            effectPool = null;
        }

        private void OnDisable() => ClearPendingFire();
    }
}
