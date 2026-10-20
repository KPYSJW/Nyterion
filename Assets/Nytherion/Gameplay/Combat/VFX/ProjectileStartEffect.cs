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
        private float fireAt;

        public void Play(ObjectPoolManager pool, Action fire, Func<bool> canContinue, float preparationTime = 0f)
        {
            ClearPendingFire();
            Animator animator = GetComponent<Animator>();
            animator.Rebind();
            animator.speed = 1f;
            animator.Update(0f);
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            float stateSpeed = Mathf.Max(0.01f, Mathf.Abs(state.speed * state.speedMultiplier));
            float duration = preparationTime > 0f ? preparationTime : Duration / stateSpeed;
            if (preparationTime > 0f && Duration > 0f)
                animator.speed = Duration / (preparationTime * stateSpeed);
            fireAt = Time.time + duration;
            effectPool = pool;
            pendingFire = fire;
            this.canContinue = canContinue;
        }

        private void Update()
        {
            if (pendingFire == null) return;
            if (!canContinue()) ReturnEffect();
            // 짧은 연출이나 낮은 프레임에서 종료 이벤트가 누락돼도 실제 발사를 보장합니다.
            else if (Time.time >= fireAt) OnAnimationComplete();
        }

        // 시작 애니메이션 마지막의 Animation Event가 호출합니다.
        public void OnAnimationComplete()
        {
            if (pendingFire == null || Time.time < fireAt) return;
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

        private void OnDisable()
        {
            ClearPendingFire();
            GetComponent<Animator>().speed = 1f;
        }
    }
}
