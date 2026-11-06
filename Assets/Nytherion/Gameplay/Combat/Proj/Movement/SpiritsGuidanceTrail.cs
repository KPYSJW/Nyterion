using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class SpiritsGuidanceTrail : MonoBehaviour
    {
        private const int FrameCount = 5;
        private const int DefaultVisibleCount = 3;
        private const int MaxVisibleCount = 5;
        private const float WorldTrailInterval = 0.08f;
        private const float WorldTrailLifetime = 0.32f;

        [Header("꼬리 설정")]
        [SerializeField] private Sprite[] trailSprites;
        [SerializeField] private Vector2 localOffset = new Vector2(-0.8f, 0f);
        [SerializeField, Min(0.01f)] private float frameInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float trailScale = 3f;
        [SerializeField, Min(0.01f)] private float trailSpacing = 1.35f;

        private readonly SpriteRenderer[] trailRenderers = new SpriteRenderer[MaxVisibleCount];
        private readonly Vector3[] trailPositions = new Vector3[MaxVisibleCount];
        private readonly float[] trailAges = new float[MaxVisibleCount];
        private readonly bool[] activeTrails = new bool[MaxVisibleCount];
        private SpriteRenderer projectileRenderer;
        private int variant = -1;
        private int visibleCount = DefaultVisibleCount;
        private int animationPhase;
        private int nextTrail;
        private float frameTimer;
        private bool worldSpaceTrail;
        private Vector3 previousPosition;

        private void OnEnable()
        {
            ClearTrail();
        }

        // 발사할 때 투사체 애니메이션과 동일한 인덱스를 전달합니다.
        public void SetVariant(int index, int count = DefaultVisibleCount, bool leaveWorldTrail = false)
        {
            ClearTrail();
            int firstFrame = index * FrameCount;
            if (index < 0 || trailSprites == null || firstFrame + FrameCount > trailSprites.Length)
                return;

            for (int i = 0; i < FrameCount; i++)
            {
                if (trailSprites[firstFrame + i] == null) return;
            }

            if (projectileRenderer == null) projectileRenderer = GetComponent<SpriteRenderer>();
            if (projectileRenderer == null) return;

            worldSpaceTrail = leaveWorldTrail;
            visibleCount = worldSpaceTrail ? MaxVisibleCount : Mathf.Clamp(count, 1, DefaultVisibleCount);
            CreateRenderers();
            variant = index;
            animationPhase = 0;
            frameTimer = 0f;
            nextTrail = 0;
            previousPosition = transform.position;
            if (!worldSpaceTrail)
            {
                ApplyFrames();
                for (int i = 0; i < visibleCount; i++) trailRenderers[i].enabled = isActiveAndEnabled;
            }
        }

        private void Update()
        {
            if (variant < 0 || worldSpaceTrail) return;

            frameTimer += Time.deltaTime;
            float interval = Mathf.Max(0.01f, frameInterval);
            if (frameTimer < interval) return;

            int steps = Mathf.FloorToInt(frameTimer / interval);
            frameTimer -= steps * interval;
            animationPhase = (animationPhase + steps) % FrameCount;
            ApplyFrames();
        }

        private void LateUpdate()
        {
            if (variant < 0 || projectileRenderer == null) return;
            if (worldSpaceTrail) UpdateWorldTrail();
            for (int i = 0; i < visibleCount; i++)
            {
                trailRenderers[i].sortingLayerID = projectileRenderer.sortingLayerID;
                trailRenderers[i].sortingOrder = projectileRenderer.sortingOrder - 1;
            }
        }

        private void OnDisable()
        {
            ClearTrail();
        }

        private void ClearTrail()
        {
            variant = -1;
            frameTimer = 0f;
            for (int i = 0; i < MaxVisibleCount; i++)
            {
                activeTrails[i] = false;
                if (trailRenderers[i] != null) trailRenderers[i].enabled = false;
            }
        }

        private void CreateRenderers()
        {
            for (int i = 0; i < visibleCount; i++)
            {
                if (trailRenderers[i] != null) continue;
                GameObject trailObject = new GameObject("SpiritsGuidanceTrail" + (i + 1));
                trailObject.layer = gameObject.layer;
                trailObject.transform.SetParent(transform, false);
                trailObject.transform.localPosition = localOffset;
                SpriteRenderer trailRenderer = trailObject.AddComponent<SpriteRenderer>();
                trailRenderer.sharedMaterial = projectileRenderer.sharedMaterial;
                trailRenderer.sortingLayerID = projectileRenderer.sortingLayerID;
                trailRenderer.sortingOrder = projectileRenderer.sortingOrder - 1;
                trailRenderer.enabled = false;
                trailRenderers[i] = trailRenderer;
            }
        }

        private void ApplyFrames()
        {
            int firstFrame = variant * FrameCount;
            // 다섯 위치 중 설정된 개수만큼 순환해 어느 조각도 계속 고정되지 않게 합니다.
            for (int i = 0; i < visibleCount; i++)
                ApplyFrame(trailRenderers[i], firstFrame, (animationPhase + i * 2) % FrameCount);
        }

        private void UpdateWorldTrail()
        {
            float deltaTime = Time.deltaTime;
            float interval = WorldTrailInterval;
            float lifetime = WorldTrailLifetime;

            for (int i = 0; i < visibleCount; i++)
            {
                if (!activeTrails[i]) continue;
                trailAges[i] += deltaTime;
                if (trailAges[i] >= lifetime)
                {
                    activeTrails[i] = false;
                    trailRenderers[i].enabled = false;
                }
            }

            frameTimer += deltaTime;
            int steps = Mathf.FloorToInt(frameTimer / interval);
            frameTimer -= steps * interval;
            int spawnCount = Mathf.Min(steps, visibleCount);
            for (int i = spawnCount - 1; i >= 0; i--)
            {
                float age = frameTimer + i * interval;
                if (age >= lifetime) continue;
                float fraction = deltaTime > 0f ? Mathf.Clamp01(1f - age / deltaTime) : 1f;
                trailPositions[nextTrail] = Vector3.Lerp(previousPosition, transform.position, fraction)
                    + transform.TransformVector(localOffset);
                trailAges[nextTrail] = age;
                activeTrails[nextTrail] = true;
                trailRenderers[nextTrail].enabled = true;
                nextTrail = (nextTrail + 1) % visibleCount;
            }
            previousPosition = transform.position;

            int firstFrame = variant * FrameCount;
            for (int i = 0; i < visibleCount; i++)
            {
                if (!activeTrails[i]) continue;
                float progress = Mathf.Clamp01(trailAges[i] / lifetime);
                int frame = Mathf.Min(Mathf.FloorToInt(progress * FrameCount), FrameCount - 1);
                float scale = trailScale * Mathf.Lerp(0.65f, 0.4f, progress);
                SpriteRenderer renderer = trailRenderers[i];
                renderer.sprite = trailSprites[firstFrame + frame];
                renderer.transform.localScale = Vector3.one * scale;
                // 시트의 각 조각은 프레임마다 중심이 달라 월드 위치에서 보정합니다.
                Vector2 frameCenter = new Vector2((12.5f - 6f * frame) / 32f, -0.5f / 32f);
                renderer.transform.position = trailPositions[i] - transform.TransformVector(frameCenter * scale);
                Color color = projectileRenderer.color;
                color.a *= 0.7f * (1f - progress);
                renderer.color = color;
            }
        }

        private void ApplyFrame(SpriteRenderer renderer, int firstFrame, int frame)
        {
            renderer.sprite = trailSprites[firstFrame + frame];
            renderer.color = Color.white;
            renderer.transform.localScale = Vector3.one * trailScale;

            // 시트 안에서 프레임마다 그림이 6픽셀씩 왼쪽으로 배치되어 있습니다.
            // 그림 중심을 기준으로 확대하고 투사체에 가까운 쪽을 기준으로 간격을 조정합니다.
            Vector2 frameCenter = new Vector2((12.5f - 6f * frame) / 32f, -0.5f / 32f);
            float spacingOffset = -6f * frame / 32f * (trailSpacing - 1f);
            renderer.transform.localPosition = localOffset + frameCenter * (1f - trailScale) + Vector2.right * spacingOffset;
        }
    }
}
