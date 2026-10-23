using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>번개에 맞은 적의 몸체 중심을 따라가는 피격 애니메이션입니다.</summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class LightningImpactVFX : MonoBehaviour
    {
        [SerializeField] private AnimationClip animationClip;
        private SpriteRenderer effectRenderer;
        private Animator effectAnimator;
        private EnemyBase target;
        private SpriteRenderer bodyRenderer;
        private ObjectPoolManager pool;
        private float elapsed;
        private int spawnFrame;
        private bool playing;

        public void Play(ObjectPoolManager ownerPool, EnemyBase enemy)
        {
            if (effectRenderer == null) effectRenderer = GetComponent<SpriteRenderer>();
            if (effectAnimator == null) effectAnimator = GetComponent<Animator>();
            pool = ownerPool;
            target = enemy;
            bodyRenderer = enemy != null ? enemy.GetComponentInChildren<SpriteRenderer>() : null;
            if (enemy == null || animationClip == null || effectAnimator.runtimeAnimatorController == null)
            {
                ReturnEffect();
                return;
            }
            playing = true;
            elapsed = 0f;
            spawnFrame = Time.frameCount;
            effectRenderer.enabled = true;
            effectRenderer.color = Color.white;
            effectAnimator.enabled = false;
            effectAnimator.Rebind();
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
            FollowTarget();
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                ReturnEffect();
                return;
            }
            if (Time.frameCount != spawnFrame)
            {
                elapsed += Time.deltaTime;
                effectAnimator.Update(Time.deltaTime);
                if (elapsed >= animationClip.length)
                {
                    ReturnEffect();
                    return;
                }
            }
            FollowTarget();
        }

        private void FollowTarget()
        {
            Vector3 center = bodyRenderer != null ? bodyRenderer.bounds.center : target.transform.position;
            center.z = 0f;
            transform.SetPositionAndRotation(center, Quaternion.identity);
            effectRenderer.sortingLayerID = bodyRenderer != null ? bodyRenderer.sortingLayerID : 0;
            effectRenderer.sortingOrder = Mathf.Max(WeaponVFXHelper.HitEffectSortingOrder,
                bodyRenderer != null ? bodyRenderer.sortingOrder + 1 : 0);
        }

        private void ReturnEffect()
        {
            ObjectPoolManager owner = pool;
            OnDisable();
            if (owner != null && owner.TryGetPoolTag(gameObject, out string tag)) owner.ReturnToPool(tag, gameObject);
            else Destroy(gameObject);
        }

        private void OnDisable()
        {
            playing = false;
            target = null;
            bodyRenderer = null;
            pool = null;
            elapsed = 0f;
            if (effectRenderer != null) effectRenderer.enabled = false;
        }
    }
}
