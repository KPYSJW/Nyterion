using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>피격 방향의 실드 표면을 따라가며 한 번 재생한 뒤 재사용 대기한다.</summary>
    [DefaultExecutionOrder(1200)]
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class BubbleShieldHitEffect : MonoBehaviour
    {
        [SerializeField] private AnimationClip animationClip;
        private Animator effectAnimator;
        private SpriteRenderer effectRenderer;
        private SpriteRenderer shieldRenderer;
        private Vector2 direction;
        private float surfaceRadius;
        private float size;
        private float endsAt;

        public void Play(SpriteRenderer shield, Vector2 hitDirection, float radius, float scale)
        {
            if (effectAnimator == null) effectAnimator = GetComponent<Animator>();
            if (effectRenderer == null) effectRenderer = GetComponent<SpriteRenderer>();
            if (animationClip == null || effectAnimator.runtimeAnimatorController == null || shield == null)
            { gameObject.SetActive(false); return; }
            shieldRenderer = shield;
            direction = hitDirection;
            surfaceRadius = radius;
            size = scale;
            endsAt = Time.time + animationClip.length;
            gameObject.SetActive(true);
            effectAnimator.Rebind();
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
            FollowShield();
        }

        private void LateUpdate()
        {
            if (shieldRenderer == null || !shieldRenderer.enabled ||
                !shieldRenderer.gameObject.activeInHierarchy || Time.time >= endsAt)
            { gameObject.SetActive(false); return; }
            FollowShield();
        }

        private void FollowShield()
        {
            Vector3 shieldScale = shieldRenderer.transform.lossyScale;
            float diameter = shieldRenderer.sprite != null ? shieldRenderer.sprite.bounds.size.x : 1f;
            // 생성·파괴 프레임의 투명 여백도 동일하므로 표면 위치가 흔들리지 않는다.
            float radius = diameter * Mathf.Abs(shieldScale.x) * surfaceRadius;
            transform.position = shieldRenderer.transform.position + (Vector3)(direction * radius);
            transform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                Mathf.Abs(shieldScale.x) * size / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                Mathf.Abs(shieldScale.y) * size / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            effectRenderer.sortingLayerID = shieldRenderer.sortingLayerID;
            effectRenderer.sortingOrder = shieldRenderer.sortingOrder + 1;
        }

        private void OnDisable() => shieldRenderer = null;
    }
}
