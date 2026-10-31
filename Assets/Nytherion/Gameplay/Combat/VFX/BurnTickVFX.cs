using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>몸체 크기에 맞춘 짧은 화상 불꽃을 재사용합니다.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class BurnTickVFX : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private SpriteRenderer[] flames;
        [SerializeField, Min(1f)] private float framesPerSecond = 16f;
        [SerializeField, Range(0.1f, 1f)] private float bodyHeightRatio = 0.6f;
        [SerializeField, Min(0.01f)] private float minimumHeight = 0.5f;
        [SerializeField, Min(0.01f)] private float maximumHeight = 0.85f;
        [Tooltip("몸체 너비가 이 값의 2배 이상이면 불꽃 2개, 3배 이상이면 3개를 표시합니다.")]
        [SerializeField, Min(0.01f)] private float bodyWidthPerFlame = 0.9f;
        [SerializeField, Min(0f)] private float stagger = 0.025f;
        [Tooltip("몸체의 짧은 변에 대한 중심 이동 거리의 최솟값입니다.")]
        [SerializeField, Range(0f, 0.48f)] private float minimumCenterOffsetRatio = 0.12f;
        [Tooltip("몸체의 짧은 변에 대한 중심 이동 거리의 최댓값입니다.")]
        [SerializeField, Range(0f, 0.48f)] private float maximumCenterOffsetRatio = 0.48f;

        private SpriteRenderer body;
        private float elapsed;
        private int flameCount;
        private int spawnFrame;
        private bool playing;
        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private Vector2[] positionJitters;
        private Vector2[] placementSizes;
        private float[] flameHeights;

        public void Play(SpriteRenderer source)
        {
            // 강화로 틱이 빨라져도 마지막 불티까지 재생한 뒤 다음 틱을 받습니다.
            if (playing || source == null || frames == null || frames.Length == 0 ||
                flames == null || flames.Length == 0) return;
            body = source;
            flameCount = Mathf.Clamp(Mathf.FloorToInt(body.bounds.size.x / bodyWidthPerFlame), 1, flames.Length);
            if (positionJitters == null || positionJitters.Length != flames.Length)
            {
                positionJitters = new Vector2[flames.Length];
                placementSizes = new Vector2[flames.Length];
                flameHeights = new float[flames.Length];
            }
            Bounds bounds = bodyBounds.GetBounds(body);
            float shortSide = Mathf.Min(bounds.size.x, bounds.size.y);
            float height = Mathf.Clamp(bounds.size.y * bodyHeightRatio, minimumHeight, maximumHeight);
            float aspect = 0f;
            foreach (Sprite frame in frames)
                aspect = Mathf.Max(aspect, frame.bounds.size.x / Mathf.Max(0.001f, frame.bounds.size.y));
            for (int i = 0; i < flameCount; i++)
            {
                flameHeights[i] = Mathf.Max(minimumHeight, height * (i == 1 ? 0.88f : 1f));
                placementSizes[i] = new Vector2(flameHeights[i] * aspect, flameHeights[i]);
            }
            // 작은 대상은 중앙 고정. 여러 불꽃은 전체 배치를 다시 뽑고 멀리 밀어내지 않습니다.
            bool placed = flameCount == 1;
            for (int layout = 0; !placed && layout < 128; layout++)
                placed = TryPlaceFlames(shortSide);
            if (!placed) flameCount = 1;
            if (flameCount == 1) positionJitters[0] = Vector2.zero;
            elapsed = 0f;
            spawnFrame = Time.frameCount;
            playing = true;
            gameObject.SetActive(true);
            enabled = true;
            UpdateFlames();
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            if (Time.frameCount != spawnFrame) elapsed += Time.deltaTime;
            if (elapsed >= frames.Length / framesPerSecond + (flameCount - 1) * stagger)
            {
                Stop();
                return;
            }
            UpdateFlames();
        }

        private void UpdateFlames()
        {
            Bounds bounds = bodyBounds.GetBounds(body);
            for (int i = 0; i < flames.Length; i++)
            {
                SpriteRenderer flame = flames[i];
                float age = elapsed - i * stagger;
                int frame = Mathf.FloorToInt(age * framesPerSecond);
                flame.enabled = i < flameCount && frame >= 0 && frame < frames.Length;
                if (!flame.enabled) continue;
                Sprite sprite = frames[frame];
                flame.sprite = sprite;
                float flameHeight = flameHeights[i];
                // 좌·중앙·우 기준점 없이 각 불꽃을 중심 주변에서 독립적으로 배치합니다.
                Vector2 offset = positionJitters[i];
                flame.transform.position = new Vector3(bounds.center.x + offset.x, bounds.center.y + offset.y, body.transform.position.z);
                flame.transform.rotation = Quaternion.identity;
                // 월드 크기로 맞춰 부모 적의 확대·좌우 반전 배율을 중복 적용하지 않습니다.
                Vector3 parentScale = flame.transform.parent.lossyScale;
                float scale = flameHeight / Mathf.Max(0.001f, sprite.bounds.size.y);
                flame.transform.localScale = new Vector3(scale / SafeScale(parentScale.x),
                    scale / SafeScale(parentScale.y), 1f);
                flame.flipX = i % 2 != 0;
                flame.sortingLayerID = body.sortingLayerID;
                flame.sortingOrder = body.sortingOrder + 1;
            }
        }

        private bool TryPlaceFlames(float shortSide)
        {
            float maximum = Mathf.Clamp(maximumCenterOffsetRatio, 0f, 0.48f);
            float minimum = Mathf.Clamp(minimumCenterOffsetRatio, 0f, maximum);
            for (int i = 0; i < flameCount; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    float direction = Random.Range(0f, Mathf.PI * 2f);
                    float distance = Random.Range(minimum, maximum) * shortSide;
                    Vector2 candidate = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * distance;
                    if (OverlapsEarlier(candidate, i)) continue;
                    positionJitters[i] = candidate;
                    placed = true;
                    break;
                }
                if (!placed) return false;
            }
            return true;
        }

        private bool OverlapsEarlier(Vector2 candidate, int index)
        {
            for (int i = 0; i < index; i++)
            {
                Vector2 separation = candidate - positionJitters[i];
                Vector2 required = (placementSizes[index] + placementSizes[i]) * 0.5f + Vector2.one * 0.04f;
                if (Mathf.Abs(separation.x) < required.x && Mathf.Abs(separation.y) < required.y) return true;
            }
            return false;
        }

        private static float SafeScale(float value)
        {
            return Mathf.Abs(value) < 0.001f ? 0.001f : value;
        }

        public void Stop()
        {
            playing = false;
            body = null;
            if (flames != null)
                foreach (SpriteRenderer flame in flames)
                    if (flame != null) flame.enabled = false;
            enabled = false;
        }

        private void OnDisable() => Stop();
    }
}
