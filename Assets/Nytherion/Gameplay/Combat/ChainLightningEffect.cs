using UnityEngine;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(LineRenderer))]
    public class ChainLightningEffect : MonoBehaviour
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

        private float elapsed = 0f;
        
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
            lineRenderer.enabled = true;
            
            UpdateTargetsPositions();
        }

        private void Update()
        {
            if (!lineRenderer.enabled) return;

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                lineRenderer.enabled = false;
                return;
            }

            // 텍스처 오프셋 롤링 애니메이션
            if (lineRenderer.material != null)
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
                lineRenderer.enabled = false;
                return;
            }

            GenerateLightningPath(currentPositions);
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
    }
}
