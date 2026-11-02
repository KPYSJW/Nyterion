using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class SpiritsGuidanceTrail : MonoBehaviour
    {
        private const int FrameCount = 5;
        private const int VisibleCount = 3;

        [Header("꼬리 설정")]
        [SerializeField] private Sprite[] trailSprites;
        [SerializeField] private Vector2 localOffset = new Vector2(-0.8f, 0f);
        [SerializeField, Min(0.01f)] private float frameInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float trailScale = 3f;
        [SerializeField, Min(0.01f)] private float trailSpacing = 1.35f;

        private readonly SpriteRenderer[] trailRenderers = new SpriteRenderer[VisibleCount];
        private SpriteRenderer projectileRenderer;
        private int variant = -1;
        private int animationPhase;
        private float frameTimer;

        private void OnEnable()
        {
            ClearTrail();
        }

        // 발사할 때 투사체 애니메이션과 동일한 인덱스를 전달합니다.
        public void SetVariant(int index)
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

            CreateRenderers();
            variant = index;
            animationPhase = 0;
            frameTimer = 0f;
            ApplyFrames();
            for (int i = 0; i < VisibleCount; i++) trailRenderers[i].enabled = isActiveAndEnabled;
        }

        private void Update()
        {
            if (variant < 0) return;

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
            for (int i = 0; i < VisibleCount; i++)
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
            for (int i = 0; i < VisibleCount; i++)
            {
                if (trailRenderers[i] != null) trailRenderers[i].enabled = false;
            }
        }

        private void CreateRenderers()
        {
            for (int i = 0; i < VisibleCount; i++)
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
            // 다섯 위치 중 세 곳을 순환해 어느 조각도 계속 고정되지 않게 합니다.
            for (int i = 0; i < VisibleCount; i++)
                ApplyFrame(trailRenderers[i], firstFrame, (animationPhase + i * 2) % FrameCount);
        }

        private void ApplyFrame(SpriteRenderer renderer, int firstFrame, int frame)
        {
            renderer.sprite = trailSprites[firstFrame + frame];
            renderer.transform.localScale = Vector3.one * trailScale;

            // 시트 안에서 프레임마다 그림이 6픽셀씩 왼쪽으로 배치되어 있습니다.
            // 그림 중심을 기준으로 확대하고 투사체에 가까운 쪽을 기준으로 간격을 조정합니다.
            Vector2 frameCenter = new Vector2((12.5f - 6f * frame) / 32f, -0.5f / 32f);
            float spacingOffset = -6f * frame / 32f * (trailSpacing - 1f);
            renderer.transform.localPosition = localOffset + frameCenter * (1f - trailScale) + Vector2.right * spacingOffset;
        }
    }
}
