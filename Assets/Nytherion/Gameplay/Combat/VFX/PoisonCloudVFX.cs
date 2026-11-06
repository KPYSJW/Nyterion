using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>중독된 적의 몸체 크기에 맞춰 독 입자 애니메이션을 반복합니다.</summary>
    [DefaultExecutionOrder(1100)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PoisonCloudVFX : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        [SerializeField, InspectorName("몸체 대비 이펙트 크기")]
        private Vector2 bodyCoverage = Vector2.one;

        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private SpriteRenderer effectRenderer;
        private SpriteRenderer body;
        private float elapsed;
        private bool playing;

        public void Play(SpriteRenderer source)
        {
            if (playing || source == null || source.sprite == null || frames == null || frames.Length == 0) return;
            if (effectRenderer == null) effectRenderer = GetComponent<SpriteRenderer>();
            body = source;
            elapsed = 0f;
            playing = true;
            gameObject.SetActive(true);
            enabled = true;
            UpdateVisual();
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (body == null || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }

            elapsed += Time.deltaTime;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            int frameIndex = Mathf.FloorToInt(elapsed * Mathf.Max(1f, framesPerSecond)) % frames.Length;
            Sprite frame = frames[frameIndex];
            if (frame == null || body.sprite == null || !body.enabled)
            {
                effectRenderer.enabled = false;
                return;
            }

            Bounds bounds = bodyBounds.GetBounds(body);
            Vector3 frameSize = frame.bounds.size;
            float width = bounds.size.x * Mathf.Max(0f, bodyCoverage.x);
            float height = bounds.size.y * Mathf.Max(0f, bodyCoverage.y);
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.position = new Vector3(bounds.center.x, bounds.center.y, body.transform.position.z);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(
                width / Mathf.Max(0.001f, frameSize.x) / SafeScale(parentScale.x),
                height / Mathf.Max(0.001f, frameSize.y) / SafeScale(parentScale.y),
                1f);
            effectRenderer.sprite = frame;
            effectRenderer.sortingLayerID = body.sortingLayerID;
            effectRenderer.sortingOrder = body.sortingOrder + 1;
            effectRenderer.enabled = true;
        }

        private static float SafeScale(float value) => Mathf.Abs(value) < 0.001f ? 0.001f : value;

        public void Stop()
        {
            playing = false;
            body = null;
            if (effectRenderer != null) effectRenderer.enabled = false;
            enabled = false;
        }

        private void OnDisable() => Stop();
    }
}
