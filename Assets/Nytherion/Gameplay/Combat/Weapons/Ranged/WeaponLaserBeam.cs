using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>레이저의 수명, 화면/장애물 끝점, 번개 외형, 틱 판정과 풀 반환을 담당합니다.</summary>
    public class WeaponLaserBeam : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int TilingProperty = Shader.PropertyToID("_Tiling");
        private static readonly int AnimationTimeProperty = Shader.PropertyToID("_AnimationTime");
        private static readonly int AnimationSpeedProperty = Shader.PropertyToID("_AnimationSpeed");
        private static readonly int FrameCountProperty = Shader.PropertyToID("_FrameCount");

        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private SpriteRenderer startEffectRenderer;
        [SerializeField] private SpriteRenderer endEffectRenderer;

        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> obstructions = new List<RaycastHit2D>(8);
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private MaterialPropertyBlock materialProperties;

        private LaserWeapon owner;
        private Transform origin;
        private ObjectPoolManager pool;
        private string poolTag;
        private ContactFilter2D targetFilter;
        private ContactFilter2D obstructionFilter;
        private Vector2 initialDirection;
        private Vector3 localAimDirection;
        private Vector2 direction;
        private Vector3[] visualPoints;
        private Camera cachedCamera;
        private Rect cameraLocalRect;
        private float damage;
        private float duration;
        private float interval;
        private float width;
        private float visualWidth;
        private float fallbackLength;
        private float fadeDuration;
        private float jitterMagnitude;
        private float jitterInterval;
        private float textureTileLength;
        private float bakedAnimationDuration;
        private float elapsed;
        private float nextJitterTime;
        private float jitterSeed;
        private int visualSegments;
        private int maxDamageTicks;
        private int damageTicksDealt;
        private int nextTick;
        private float firingEndTime;
        private bool followAim;
        private bool usesBakedFadeFrames;
        private bool initialized;
        private Color beamColor;

        public bool IsFiring { get; private set; }
        public float CurrentLength { get; private set; }

        public void Initialize(LaserWeapon owner, Transform origin, Vector2 aimDirection,
            LaserWeaponData data, float tickDamage, ObjectPoolManager pool)
        {
            this.owner = owner;
            this.origin = origin;
            this.pool = pool;
            poolTag = data.projectilePrefab != null ? data.projectilePrefab.name : string.Empty;
            damage = Mathf.Max(0f, tickDamage);
            duration = Mathf.Max(0.01f, data.fireDuration);
            interval = Mathf.Max(0.01f, data.tickInterval);
            width = Mathf.Max(0.01f, data.beamWidth);
            visualWidth = Mathf.Max(0.01f, data.visualBeamWidth);
            fallbackLength = Mathf.Max(0f, data.range);
            fadeDuration = Mathf.Max(0f, data.fadeDuration);
            followAim = data.followAim;
            visualSegments = Mathf.Max(2, data.visualSegments);
            jitterMagnitude = Mathf.Max(0f, data.jitterMagnitude);
            jitterInterval = Mathf.Max(0.01f, data.jitterInterval);
            textureTileLength = Mathf.Max(0.01f, data.textureTileLength);
            maxDamageTicks = Mathf.Max(1, data.damageTickCount);
            damageTicksDealt = 0;
            elapsed = 0f;
            firingEndTime = duration;
            nextTick = 1;
            nextJitterTime = 0f;
            jitterSeed = Random.value * 1000f;
            bakedAnimationDuration = 0f;
            usesBakedFadeFrames = false;
            overlaps.Clear();
            obstructions.Clear();
            hitTargets.Clear();

            initialDirection = aimDirection.sqrMagnitude > 0.0001f
                ? aimDirection.normalized : (Vector2)origin.right;
            localAimDirection = origin.InverseTransformDirection(initialDirection);
            targetFilter = new ContactFilter2D { useTriggers = true };
            targetFilter.SetLayerMask(data.targetLayers);
            obstructionFilter = new ContactFilter2D { useTriggers = false };
            obstructionFilter.SetLayerMask(data.obstructionLayers);

            EnsureVisualBuffer();
            if (lineRenderer != null)
            {
                beamColor = Color.white;
                lineRenderer.enabled = true;
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = visualPoints.Length;
                lineRenderer.numCapVertices = 0;
                lineRenderer.numCornerVertices = 2;

                Material beamMaterial = lineRenderer.sharedMaterial;
                if (beamMaterial != null && beamMaterial.HasProperty(AnimationSpeedProperty) &&
                    beamMaterial.HasProperty(FrameCountProperty))
                {
                    float animationSpeed = beamMaterial.GetFloat(AnimationSpeedProperty);
                    float frameCount = beamMaterial.GetFloat(FrameCountProperty);
                    if (animationSpeed > 0f && frameCount > 0f)
                    {
                        bakedAnimationDuration = frameCount / animationSpeed;
                        usesBakedFadeFrames = true;
                    }
                }

                SpriteRenderer sourceRenderer = owner.GetComponentInChildren<SpriteRenderer>();
                if (sourceRenderer != null)
                {
                    lineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                    lineRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;
                }

                PrepareEndpointEffect(startEffectRenderer);
                PrepareEndpointEffect(endEffectRenderer);
            }

            initialized = true;
            IsFiring = true;
            UpdateGeometry(1f, true);
            PerformDamageTick();
            if (damageTicksDealt >= maxDamageTicks) FinishFiring();
        }

        private void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        private void Advance(float deltaTime)
        {
            if (!initialized) return;
            if (owner == null || !owner.isActiveAndEnabled || origin == null || !origin.gameObject.activeInHierarchy)
            {
                StopImmediately();
                return;
            }
            if (deltaTime <= 0f) return;

            elapsed += deltaTime;
            bool refreshJitter = jitterMagnitude > 0f && elapsed >= nextJitterTime;
            if (refreshJitter) nextJitterTime = elapsed + jitterInterval;
            UpdateGeometry(IsFiring ? 1f : GetVisualVisibility(), refreshJitter);

            if (IsFiring)
            {
                // 정수 틱 번호로 누적 오차를 줄이고 낮은 프레임률에서도 3회의 공격을 놓치지 않습니다.
                while (damageTicksDealt < maxDamageTicks &&
                       nextTick * (double)interval < duration - 0.000001d &&
                       nextTick * (double)interval <= elapsed + 0.000001d)
                {
                    nextTick++;
                    PerformDamageTick();
                    if (!initialized) return;
                    if (damageTicksDealt >= maxDamageTicks)
                    {
                        FinishFiring();
                        break;
                    }
                }

                if (IsFiring && elapsed >= duration) FinishFiring();
            }

            if (!IsFiring && elapsed >= GetVisualEndTime())
            {
                StopImmediately();
            }
        }

        private float GetVisualVisibility()
        {
            // 마지막 두 프레임에 소멸 표현이 포함된 재질은 코드에서 별도로 투명하게 만들지 않습니다.
            if (usesBakedFadeFrames) return 1f;
            return fadeDuration > 0f ? 1f - Mathf.Clamp01((elapsed - firingEndTime) / fadeDuration) : 0f;
        }

        private float GetVisualEndTime()
        {
            return usesBakedFadeFrames
                ? Mathf.Max(firingEndTime, bakedAnimationDuration)
                : firingEndTime + fadeDuration;
        }

        private void FinishFiring()
        {
            if (!IsFiring) return;
            IsFiring = false;
            firingEndTime = elapsed;
            owner?.OnBeamFiringEnded(this);
        }

        private void UpdateGeometry(float visibility, bool refreshJitter)
        {
            direction = followAim
                ? ((Vector2)origin.TransformDirection(localAimDirection)).normalized
                : initialDirection;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(origin.position, Quaternion.Euler(0f, 0f, angle));

            // 카메라가 있으면 사거리 대신 화면 끝을 최대 끝점으로 사용합니다.
            // 카메라가 없는 검증/수동 씬에서는 WeaponData.range가 안전한 예비 길이입니다.
            CurrentLength = TryGetCameraEdgeDistance(origin.position, direction, out float cameraDistance)
                ? cameraDistance : fallbackLength;

            // 빔의 폭까지 검사하므로 가장자리가 벽을 뚫고 피해를 주지 않습니다.
            obstructions.Clear();
            Physics2D.BoxCast(origin.position, new Vector2(0.001f, width), angle,
                direction, obstructionFilter, obstructions, CurrentLength);
            for (int i = 0; i < obstructions.Count; i++)
            {
                Collider2D obstacle = obstructions[i].collider;
                if (obstacle == null || obstacle.transform.IsChildOf(owner.CasterTransform)) continue;
                CurrentLength = Mathf.Min(CurrentLength, obstructions[i].distance);
            }

            UpdateVisual(visibility, refreshJitter);
        }

        private bool TryGetCameraEdgeDistance(Vector2 start, Vector2 rayDirection, out float distance)
        {
            distance = 0f;
            cachedCamera = null;
            Camera targetCamera = Camera.main;
            if (targetCamera == null) return false;

            float planeDistance = Vector3.Dot(origin.position - targetCamera.transform.position,
                targetCamera.transform.forward);
            if (planeDistance <= 0f) return false;

            Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, planeDistance));
            Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, planeDistance));
            Vector3 localBottomLeft = targetCamera.transform.InverseTransformPoint(bottomLeft);
            Vector3 localTopRight = targetCamera.transform.InverseTransformPoint(topRight);
            Vector3 localStart = targetCamera.transform.InverseTransformPoint(start);
            Vector3 localDirection = targetCamera.transform.InverseTransformDirection(rayDirection);
            Vector2 localDirection2D = new Vector2(localDirection.x, localDirection.y);
            if (localDirection2D.sqrMagnitude <= 0.0001f) return false;
            localDirection2D.Normalize();

            // 선의 절반 폭을 네 변에서 모두 빼면 발사 방향 쪽 경계에서도 과도하게 안쪽에 멈춥니다.
            // 선에 수직인 폭이 각 화면 축에 차지하는 양만 남겨 실제 충돌 경계에는 거의 붙입니다.
            Vector2 localNormal = new Vector2(-localDirection2D.y, localDirection2D.x);
            float halfVisualWidth = Mathf.Max(width, visualWidth) * 0.5f;
            const float edgeSafetyMargin = 0.001f;
            float horizontalMargin = halfVisualWidth * Mathf.Abs(localNormal.x) + edgeSafetyMargin;
            float verticalMargin = halfVisualWidth * Mathf.Abs(localNormal.y) + edgeSafetyMargin;
            cameraLocalRect = Rect.MinMaxRect(
                Mathf.Min(localBottomLeft.x, localTopRight.x) + horizontalMargin,
                Mathf.Min(localBottomLeft.y, localTopRight.y) + verticalMargin,
                Mathf.Max(localBottomLeft.x, localTopRight.x) - horizontalMargin,
                Mathf.Max(localBottomLeft.y, localTopRight.y) - verticalMargin);
            if (!cameraLocalRect.Contains(localStart)) return false;
            cachedCamera = targetCamera;

            float horizontalDistance = float.PositiveInfinity;
            if (localDirection.x > 0.0001f)
                horizontalDistance = (cameraLocalRect.xMax - localStart.x) / localDirection.x;
            else if (localDirection.x < -0.0001f)
                horizontalDistance = (cameraLocalRect.xMin - localStart.x) / localDirection.x;

            float verticalDistance = float.PositiveInfinity;
            if (localDirection.y > 0.0001f)
                verticalDistance = (cameraLocalRect.yMax - localStart.y) / localDirection.y;
            else if (localDirection.y < -0.0001f)
                verticalDistance = (cameraLocalRect.yMin - localStart.y) / localDirection.y;

            distance = Mathf.Max(0f, Mathf.Min(horizontalDistance, verticalDistance));
            return !float.IsInfinity(distance) && !float.IsNaN(distance);
        }

        private void UpdateVisual(float visibility, bool refreshJitter)
        {
            if (lineRenderer == null) return;

            lineRenderer.enabled = visibility > 0f;
            // 소멸 형태는 LaserEffect2의 마지막 프레임을 그대로 사용하므로 선 폭은 줄이지 않습니다.
            lineRenderer.startWidth = visualWidth;
            lineRenderer.endWidth = visualWidth;
            UpdateEndpointEffects(visibility);
            if (!lineRenderer.enabled) return;

            if (refreshJitter || visualPoints[0] != ClampToCamera(origin.position) ||
                visualPoints[visualPoints.Length - 1] != ClampToCamera(GetEndPoint()))
            {
                GenerateLightningPath();
            }

            lineRenderer.SetPositions(visualPoints);
            // Unity가 Play Mode 중 스크립트를 다시 로드하면 네이티브 래퍼 필드는 null이 될 수 있습니다.
            // 풀에 이미 만들어진 빔도 안전하게 재사용하도록 매 사용 시 보장합니다.
            if (materialProperties == null) materialProperties = new MaterialPropertyBlock();
            materialProperties.Clear();
            materialProperties.SetColor(ColorProperty,
                new Color(beamColor.r, beamColor.g, beamColor.b, visibility));
            materialProperties.SetFloat(TilingProperty,
                Mathf.Max(0.0001f, CurrentLength / textureTileLength));
            materialProperties.SetFloat(AnimationTimeProperty, elapsed);
            lineRenderer.SetPropertyBlock(materialProperties);
        }

        private void PrepareEndpointEffect(SpriteRenderer endpoint)
        {
            if (endpoint == null) return;

            endpoint.enabled = true;
            endpoint.color = Color.white;
            endpoint.sortingLayerID = lineRenderer.sortingLayerID;
            endpoint.sortingOrder = lineRenderer.sortingOrder + 1;
            Animator animator = endpoint.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private void UpdateEndpointEffects(float visibility)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 시작 효과의 왼쪽(-X)은 플레이어 쪽, 종료 효과는 그 반대 방향을 향합니다.
            UpdateEndpointEffect(startEffectRenderer, origin.position, angle, visibility);
            UpdateEndpointEffect(endEffectRenderer, GetEndPoint(), angle + 180f, visibility);
        }

        private static void UpdateEndpointEffect(SpriteRenderer endpoint, Vector3 position,
            float angle, float visibility)
        {
            if (endpoint == null) return;

            endpoint.enabled = visibility > 0f;
            if (!endpoint.enabled) return;

            endpoint.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            Color color = endpoint.color;
            color.a = visibility;
            endpoint.color = color;
        }

        private void GenerateLightningPath()
        {
            Vector3 start = origin.position;
            Vector3 end = GetEndPoint();
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float noiseTime = Mathf.Floor(elapsed / jitterInterval) * 0.73f;

            visualPoints[0] = ClampToCamera(start);
            for (int i = 1; i < visualPoints.Length - 1; i++)
            {
                float t = (float)i / (visualPoints.Length - 1);
                Vector3 basePoint = Vector3.Lerp(start, end, t);
                float noise = Mathf.PerlinNoise(jitterSeed + i * 0.618f, noiseTime) * 2f - 1f;
                float envelope = Mathf.Sin(t * Mathf.PI);
                visualPoints[i] = ClampToCamera(basePoint + (Vector3)(normal * (noise * jitterMagnitude * envelope)));
            }
            visualPoints[visualPoints.Length - 1] = ClampToCamera(end);
        }

        private Vector3 GetEndPoint()
        {
            return origin != null ? origin.position + (Vector3)(direction * CurrentLength) : transform.position;
        }

        private Vector3 ClampToCamera(Vector3 point)
        {
            if (cachedCamera == null) return point;
            Vector3 localPoint = cachedCamera.transform.InverseTransformPoint(point);
            localPoint.x = Mathf.Clamp(localPoint.x, cameraLocalRect.xMin, cameraLocalRect.xMax);
            localPoint.y = Mathf.Clamp(localPoint.y, cameraLocalRect.yMin, cameraLocalRect.yMax);
            return cachedCamera.transform.TransformPoint(localPoint);
        }

        private void EnsureVisualBuffer()
        {
            int pointCount = visualSegments + 1;
            if (visualPoints == null || visualPoints.Length != pointCount)
                visualPoints = new Vector3[pointCount];
        }

        private void PerformDamageTick()
        {
            damageTicksDealt++;
            owner?.OnBeamDamageTick(this, direction);
            if (!initialized || !IsFiring || owner == null || origin == null) return;

            // 반동으로 총구가 움직인 같은 프레임에 빔 시작점도 다시 맞춥니다.
            UpdateGeometry(1f, false);
            if (CurrentLength <= 0f) return;

            overlaps.Clear();
            hitTargets.Clear();
            Vector2 center = (Vector2)transform.position + direction * (CurrentLength * 0.5f);
            Physics2D.OverlapBox(center, new Vector2(CurrentLength, width),
                transform.eulerAngles.z, targetFilter, overlaps);

            for (int i = 0; i < overlaps.Count; i++)
            {
                Collider2D hit = overlaps[i];
                if (hit == null || !hit.gameObject.activeInHierarchy ||
                    hit.transform.IsChildOf(owner.CasterTransform)) continue;

                // 자식 히트박스를 지원하며 같은 적의 여러 콜라이더는 한 틱에 한 번만 처리합니다.
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (!(target is MonoBehaviour targetBehaviour) || !targetBehaviour.isActiveAndEnabled ||
                    !hitTargets.Add(target)) continue;

                target.TakeDamage(damage);
                if (!initialized || owner == null) return;
                if (targetBehaviour.isActiveAndEnabled) owner.ApplyStatusEffects(target);
            }
        }

        public void StopImmediately()
        {
            if (!initialized) return;

            bool wasFiring = IsFiring;
            initialized = false;
            IsFiring = false;
            if (owner != null)
            {
                if (wasFiring) owner.OnBeamFiringEnded(this);
                owner.OnBeamReleased(this);
            }
            owner = null;
            origin = null;
            cachedCamera = null;
            overlaps.Clear();
            hitTargets.Clear();
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (startEffectRenderer != null) startEffectRenderer.enabled = false;
            if (endEffectRenderer != null) endEffectRenderer.enabled = false;

            if (pool != null && !string.IsNullOrEmpty(poolTag))
                pool.ReturnToPool(poolTag, gameObject);
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            if (initialized && owner != null)
            {
                if (IsFiring) owner.OnBeamFiringEnded(this);
                owner.OnBeamReleased(this);
            }
            initialized = false;
            IsFiring = false;
            owner = null;
            origin = null;
            cachedCamera = null;
            overlaps.Clear();
            obstructions.Clear();
            hitTargets.Clear();
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (startEffectRenderer != null) startEffectRenderer.enabled = false;
            if (endEffectRenderer != null) endEffectRenderer.enabled = false;
        }
    }
}
