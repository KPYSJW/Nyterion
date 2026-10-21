using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>열충격 폭발을 발동 위치에서 한 번 재생하고 풀에 반환합니다.</summary>
    [DefaultExecutionOrder(1100)]
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class ThermalShockExplosionVFX : MonoBehaviour
    {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField, Min(0.01f)] private float nativeRadius = 0.5f;

        private Animator effectAnimator;
        private SpriteRenderer effectRenderer;
        private ObjectPoolManager pool;
        private bool playing;
        private float elapsed;
        private int spawnFrame;

        public void Play(ObjectPoolManager effectPool, float radius, SpriteRenderer sortingSource)
        {
            if (effectAnimator == null) effectAnimator = GetComponent<Animator>();
            if (effectRenderer == null) effectRenderer = GetComponent<SpriteRenderer>();
            pool = effectPool;
            if (animationClip == null || effectAnimator.runtimeAnimatorController == null)
            {
                ReturnEffect();
                return;
            }

            transform.localScale = Vector3.one * (Mathf.Max(0.01f, radius) / Mathf.Max(0.01f, nativeRadius));
            effectRenderer.sortingLayerID = sortingSource != null ? sortingSource.sortingLayerID : 0;
            effectRenderer.sortingOrder = sortingSource != null ? Mathf.Max(100, sortingSource.sortingOrder + 10) : 100;
            effectRenderer.color = Color.white;
            effectRenderer.enabled = true;
            elapsed = 0f;
            spawnFrame = Time.frameCount;
            playing = true;
            // 풀에서 꺼낸 프레임에는 첫 이미지를 유지하고 이후 직접 애니메이션을 진행합니다.
            effectAnimator.enabled = false;
            effectAnimator.speed = 1f;
            effectAnimator.Rebind();
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
        }

        private void LateUpdate()
        {
            if (!playing || Time.frameCount == spawnFrame) return;
            elapsed += Time.deltaTime;
            effectAnimator.Update(Time.deltaTime);
            if (elapsed >= animationClip.length) ReturnEffect();
        }

        private void ReturnEffect()
        {
            ObjectPoolManager effectPool = pool;
            playing = false;
            pool = null;
            if (effectPool != null && effectPool.TryGetPoolTag(gameObject, out string tag))
                effectPool.ReturnToPool(tag, gameObject);
            else Destroy(gameObject);
        }

        private void OnDisable()
        {
            playing = false;
            pool = null;
        }
    }
}
