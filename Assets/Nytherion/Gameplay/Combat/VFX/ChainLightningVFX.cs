using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(LineRenderer))]
    public class ChainLightningVFX : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        
        [Header("Lightning Visual Settings")]
        [Tooltip("번개가 꺾이는 횟수 (세그먼트 수)")]
        public int segments = 10;
        
        [Tooltip("지그재그 흔들림의 최대 강도")]
        public float jitterMagnitude = 0.3f;
        
        [Tooltip("번개가 유지되는 시간 (초)")]
        public float duration = 0.2f;
        
        [Tooltip("전류가 흘러가는 속도")]
        public float textureScrollSpeed = 15f;

        [Tooltip("번개를 지그재그가 아닌 완벽한 직선 형태로 그릴지 여부")]
        public bool useStraightLine = false;

        [Header("스프라이트 애니메이션 (선택)")]
        [Tooltip("지정하면 각 대상 사이에 이 프리팹의 스프라이트 애니메이션을 표시합니다.")]
        [SerializeField] private SpriteRenderer spriteSegmentPrefab;
        [SerializeField, Min(0.01f)] private float spriteHeight = 1f;
        [SerializeField] private Sprite[] spriteFrames;
        [SerializeField, Min(1f)] private float spriteFramesPerSecond = 30f;
        [Tooltip("이어지는 원본 크기 조각 사이의 애니메이션 프레임 차이")]
        [SerializeField, Min(0)] private int spriteFrameOffset = 1;
        private readonly List<SpriteRenderer> spriteSegments = new List<SpriteRenderer>();
        private bool playing;

        [Header("대상 중심 피격 이펙트")]
        [SerializeField] private LightningImpactVFX impactEffectPrefab;
        public bool HasImpactEffect => impactEffectPrefab != null;

        public bool IsVisible => gameObject.activeInHierarchy && playing && (spriteSegmentPrefab != null
            ? spriteSegments.Exists(segment => segment != null && segment.gameObject.activeInHierarchy && segment.enabled)
            : lineRenderer != null && lineRenderer.enabled);

        private float elapsed = 0f;
        private int setupFrame = -1;
        
        // 실시간 위치 추적을 위한 Transform 레퍼런스
        private Transform startAnchor;
        private List<Transform> targetAnchors = new List<Transform>();
        
        // 고정 좌표 렌더링용 리스트 (허공 샷 등 끝점 고정용)
        private List<Vector3> staticTargets = new List<Vector3>();

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
        }

        /// <summary>
        /// 실시간 앵커 트랙킹 및 고정 좌표 빔을 단일화하여 구동합니다.
        /// </summary>
        public void Setup(Transform startTransform, List<Transform> targets, List<Vector3> staticPoints = null)
        {
            if (lineRenderer == null) Awake();
            startAnchor = startTransform;
            
            targetAnchors.Clear();
            if (targets != null)
            {
                targetAnchors.AddRange(targets);
            }

            staticTargets.Clear();
            if (staticPoints != null)
            {
                staticTargets.AddRange(staticPoints);
            }

            elapsed = 0f;
            setupFrame = Time.frameCount;
            playing = true;
            lineRenderer.enabled = spriteSegmentPrefab == null;
            foreach (SpriteRenderer segment in spriteSegments)
                if (segment != null) segment.gameObject.SetActive(false);
            if (Application.isPlaying && impactEffectPrefab != null)
            {
                HashSet<EnemyBase> impacted = new HashSet<EnemyBase>();
                PlayImpact(startAnchor, impacted);
                foreach (Transform anchor in targetAnchors) PlayImpact(anchor, impacted);
            }
            
            UpdateTargetsPositions();
        }

        private void PlayImpact(Transform anchor, HashSet<EnemyBase> impacted)
        {
            EnemyBase enemy = anchor != null ? anchor.GetComponentInParent<EnemyBase>() : null;
            if (enemy == null || !enemy.gameObject.activeInHierarchy || !impacted.Add(enemy)) return;
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            GameObject instance = pool != null
                ? pool.SpawnFromPool(impactEffectPrefab.gameObject, enemy.transform.position, Quaternion.identity)
                : Instantiate(impactEffectPrefab.gameObject, enemy.transform.position, Quaternion.identity);
            if (instance != null) instance.GetComponent<LightningImpactVFX>().Play(pool, enemy);
        }

        private void Update()
        {
            if (!playing) return;
            // 적중 프레임의 긴 deltaTime으로 첫 렌더 전에 사라지는 것을 방지합니다.
            if (Time.frameCount == setupFrame) return;

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                StopRendering();
                return;
            }


            // 텍스처 오프셋 롤링 애니메이션
            if (spriteSegmentPrefab == null && lineRenderer.material != null)
            {
                float offset = Time.time * textureScrollSpeed;
                lineRenderer.material.mainTextureOffset = new Vector2(-offset, 0f);
            }

            // 실시간 트래킹 업데이트
            UpdateTargetsPositions();
        }

        private void UpdateTargetsPositions()
        {
            // 에디터 옵션 풀림 방지 강제화
            if (lineRenderer != null && !lineRenderer.useWorldSpace)
            {
                lineRenderer.useWorldSpace = true;
            }

            List<Vector3> currentPositions = new List<Vector3>();
            
            // 1. 플레이어 발사체 시작 위치 실시간 갱신 (Z축 2D 일치)
            if (startAnchor != null)
            {
                currentPositions.Add(new Vector3(startAnchor.position.x, startAnchor.position.y, 0f));
            }
            else
            {
                currentPositions.Add(Vector3.zero);
            }

            // 2. 연쇄된 몬스터들의 현재 위치 실시간 갱신 (죽어서 파괴된 몹은 제외)
            foreach (Transform t in targetAnchors)
            {
                if (t != null)
                {
                    currentPositions.Add(new Vector3(t.position.x, t.position.y, 0f));
                }
            }

            // 3. 고정 좌표 데이터 (허공 샷 등 끝부분 좌표 고정)
            foreach (Vector3 p in staticTargets)
            {
                currentPositions.Add(new Vector3(p.x, p.y, 0f));
            }

            // 타겟이 전부 파괴되어 그릴 선이 없는 경우 즉시 끔
            if (currentPositions.Count < 2)
            {
                StopRendering();
                return;
            }

            if (spriteSegmentPrefab != null)
            {
                lineRenderer.positionCount = currentPositions.Count;
                lineRenderer.SetPositions(currentPositions.ToArray());
                UpdateSpriteSegments(currentPositions);
            }
            else GenerateLightningPath(currentPositions);
        }

        private void UpdateSpriteSegments(List<Vector3> points)
        {
            int used = 0;
            bool hasFrames = spriteFrames != null && spriteFrames.Length > 0;
            Sprite reference = hasFrames ? spriteFrames[0] : spriteSegmentPrefab.sprite;
            if (reference == null) return;
            Vector3 size = reference.bounds.size;
            float scale = spriteHeight / Mathf.Max(0.0001f, size.y);
            float tileWidth = Mathf.Max(0.0001f, size.x * scale);
            int timeFrame = Mathf.FloorToInt(elapsed * spriteFramesPerSecond);
            Vector3 parentScale = transform.lossyScale;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                float distance = delta.magnitude;
                if (distance < 0.0001f) continue;
                Vector3 direction = delta / distance;
                Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                int count = Mathf.CeilToInt(distance / tileWidth);
                for (int tile = 0; tile < count; tile++)
                {
                    float width = Mathf.Min(tileWidth, distance - tile * tileWidth);
                    if (width < 0.0001f) continue;
                    if (used >= spriteSegments.Count)
                    {
                        SpriteRenderer created = Instantiate(spriteSegmentPrefab, transform);
                        Animator animator = created.GetComponent<Animator>();
                        if (animator != null) animator.enabled = false;
                        spriteSegments.Add(created);
                    }
                    SpriteRenderer segment = spriteSegments[used];
                    segment.sprite = hasFrames ? spriteFrames[(timeFrame + used * spriteFrameOffset) % spriteFrames.Length] : reference;
                    segment.gameObject.SetActive(true);
                    segment.enabled = true;
                    segment.sortingLayerID = lineRenderer.sortingLayerID;
                    segment.sortingOrder = lineRenderer.sortingOrder;
                    segment.transform.SetPositionAndRotation(points[i] + direction * (tile * tileWidth + width * 0.5f), rotation);
                    // 조각별로 원본 비율을 유지하고, 마지막은 왼쪽부터 필요한 너비만 표시합니다.
                    segment.drawMode = SpriteDrawMode.Tiled;
                    segment.tileMode = SpriteTileMode.Continuous;
                    segment.size = new Vector2(width / scale, size.y);
                    segment.transform.localScale = new Vector3(
                        scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                        scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
                    used++;
                }
            }
            for (int i = used; i < spriteSegments.Count; i++)
                spriteSegments[i].gameObject.SetActive(false);
        }

        private void StopRendering()
        {
            playing = false;
            if (lineRenderer != null) lineRenderer.enabled = false;
            foreach (SpriteRenderer segment in spriteSegments)
                if (segment != null) segment.gameObject.SetActive(false);
        }

        private void GenerateLightningPath(List<Vector3> pathPoints)
        {
            if (pathPoints.Count < 2) return;

            List<Vector3> points = new List<Vector3>();

            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Vector3 start = pathPoints[i];
                Vector3 end = pathPoints[i + 1];
                points.AddRange(GenerateSegmentPath(start, end));
            }

            points.Add(pathPoints[pathPoints.Count - 1]);

            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
        }

        private List<Vector3> GenerateSegmentPath(Vector3 start, Vector3 end)
        {
            List<Vector3> segmentPoints = new List<Vector3>();
            segmentPoints.Add(start);

            if (useStraightLine)
            {
                // 직선 모드인 경우 꺾임 노이즈 연산 없이 시작 지점만 담고 즉시 리턴
                return segmentPoints;
            }

            Vector3 direction = end - start;
            Vector3 normal = new Vector3(-direction.y, direction.x, 0f).normalized;

            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector3 basePoint = Vector3.Lerp(start, end, t);

                // 중간 노이즈 랜덤값 계산
                float offset = Random.Range(-jitterMagnitude, jitterMagnitude);
                
                // 번개의 중심은 덜 꺾이고 중간 부분에서 가장 역동적으로 흔들리도록 사인 곡선 적용
                float envelope = Mathf.Sin(t * Mathf.PI);
                Vector3 jitterPoint = basePoint + normal * (offset * envelope);

                segmentPoints.Add(jitterPoint);
            }

            return segmentPoints;
        }

        private void OnDisable()
        {
            StopRendering();
            startAnchor = null;
            targetAnchors.Clear();
            staticTargets.Clear();
            elapsed = 0f;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
                lineRenderer.positionCount = 0;
            }
        }
    }
}
