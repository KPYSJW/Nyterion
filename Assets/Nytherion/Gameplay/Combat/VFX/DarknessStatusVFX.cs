using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>어둠 상태인 적의 몸체를 따라 반복되는 이펙트를 표시합니다.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class DarknessStatusVFX : MonoBehaviour
    {
        [SerializeField, Min(0f), InspectorName("몸체 높이 대비 이펙트 크기")]
        private float heightMultiplier = 1.2f;
        [SerializeField, InspectorName("몸체 크기 대비 위치 보정")]
        [Tooltip("적 몸체 크기를 기준으로 X는 좌우, Y는 상하 위치를 조정합니다.")]
        private Vector2 positionOffset = Vector2.zero;

        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private SpriteRenderer body;
        private SpriteRenderer effectRenderer;
        private Animator animator;

        private void Awake()
        {
            effectRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
        }

        public void Play(SpriteRenderer source)
        {
            if (source == null || source.sprite == null || effectRenderer == null) return;
            body = source;
            enabled = true;
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }
            UpdateVisual();
        }

        private void LateUpdate()
        {
            if (body == null || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            bool visible = body.sprite != null && body.enabled && effectRenderer.sprite != null;
            effectRenderer.enabled = visible;
            if (!visible) return;

            Bounds bounds = bodyBounds.GetBounds(body);
            float frameHeight = effectRenderer.sprite.bounds.size.y;
            float scale = bounds.size.y * Mathf.Max(0f, heightMultiplier) / Mathf.Max(0.001f, frameHeight);
            transform.position = bounds.center + new Vector3(
                bounds.size.x * positionOffset.x, bounds.size.y * positionOffset.y, 0f);
            transform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale / SafeScale(parentScale.x),
                scale / SafeScale(parentScale.y), 1f / SafeScale(parentScale.z));
            effectRenderer.sortingLayerID = body.sortingLayerID;
            effectRenderer.sortingOrder = body.sortingOrder + 1;
        }

        private static float SafeScale(float value) => Mathf.Abs(value) < 0.001f ? 0.001f : value;

        public void Stop()
        {
            body = null;
            if (effectRenderer != null) effectRenderer.enabled = false;
            if (animator != null) animator.enabled = false;
            enabled = false;
        }

        private void OnDisable() => Stop();
    }
}
