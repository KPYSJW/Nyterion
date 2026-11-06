using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>감전 피해 대상의 몸체 중앙 근처에서 위치·각도를 바꿔 짧은 전격을 재생합니다.</summary>
    [DefaultExecutionOrder(1100)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ShockPulseVFX : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 18f;
        [SerializeField, Range(0.1f, 1f)] private float bodyCoverage = 0.85f;
        [SerializeField, Min(0.01f)] private float minimumSize = 0.5f;
        [SerializeField, Min(0.01f)] private float maximumSize = 1.4f;
        [Tooltip("몸체의 짧은 변에 대한 중심 이동 거리의 최솟값입니다.")]
        [SerializeField, Range(0f, 1f)] private float minimumCenterOffsetRatio = 0.12f;
        [Tooltip("몸체의 짧은 변에 대한 중심 이동 거리의 최댓값입니다.")]
        [SerializeField, Range(0f, 1f)] private float centerOffsetRatio = 0.48f;

        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private SpriteRenderer effectRenderer;
        private SpriteRenderer body;
        private float elapsed;
        private int spawnFrame;
        private bool playing;
        private Vector2 offsetDirection;
        private float offsetRatio;
        private float rotation;

        public void Play(SpriteRenderer source)
        {
            if (playing || source == null || source.sprite == null || frames == null || frames.Length == 0) return;
            if (effectRenderer == null) effectRenderer = GetComponent<SpriteRenderer>();
            body = source;
            float direction = Random.Range(0f, Mathf.PI * 2f);
            offsetDirection = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction));
            float maximumOffset = Mathf.Clamp01(centerOffsetRatio);
            float minimumOffset = Mathf.Clamp(minimumCenterOffsetRatio, 0f, maximumOffset);
            offsetRatio = Random.Range(minimumOffset, maximumOffset);
            rotation = Random.Range(0f, 360f);
            elapsed = 0f;
            spawnFrame = Time.frameCount;
            gameObject.SetActive(true);
            enabled = true;
            playing = true;
            UpdateVisual();
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (body == null || body.sprite == null || !body.enabled || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            if (Time.frameCount != spawnFrame) elapsed += Time.deltaTime;
            if (elapsed >= frames.Length / framesPerSecond) { Stop(); return; }
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            Sprite frame = frames[Mathf.Min(Mathf.FloorToInt(elapsed * framesPerSecond), frames.Length - 1)];
            Bounds bounds = bodyBounds.GetBounds(body);
            Vector2 offset = offsetDirection * (Mathf.Min(bounds.size.x, bounds.size.y) * offsetRatio);
            float width = Mathf.Clamp(bounds.size.x * bodyCoverage, minimumSize, maximumSize);
            float height = Mathf.Clamp(bounds.size.y * bodyCoverage, minimumSize, maximumSize);
            float cos = Mathf.Abs(Mathf.Cos(rotation * Mathf.Deg2Rad));
            float sin = Mathf.Abs(Mathf.Sin(rotation * Mathf.Deg2Rad));
            // 위치가 중심에서 멀어져도 크기를 줄이지 않고, 회전 후 표시 크기를 일정하게 유지합니다.
            float rotatedWidth = frame.bounds.size.x * cos + frame.bounds.size.y * sin;
            float rotatedHeight = frame.bounds.size.x * sin + frame.bounds.size.y * cos;
            float scale = Mathf.Min(width / Mathf.Max(0.001f, rotatedWidth), height / Mathf.Max(0.001f, rotatedHeight));
            transform.position = new Vector3(bounds.center.x + offset.x, bounds.center.y + offset.y, body.transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale / SafeScale(parentScale.x), scale / SafeScale(parentScale.y), 1f);
            effectRenderer.sprite = frame;
            effectRenderer.sortingLayerID = body.sortingLayerID;
            // 일반 타격 효과(100) 뒤에 가려지지 않도록 전격을 앞에 표시합니다.
            effectRenderer.sortingOrder = Mathf.Max(WeaponVFXHelper.HitEffectSortingOrder + 1, body.sortingOrder + 2);
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
