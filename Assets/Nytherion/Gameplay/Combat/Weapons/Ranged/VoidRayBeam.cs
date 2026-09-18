using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>지속 광선의 좌표 계산, 충돌 필터링, 피해 주기와 풀 수명만 관리합니다.</summary>
    public class VoidRayBeam : MonoBehaviour
    {
        private VoidRayBeamVisual visual;

        private readonly List<RaycastHit2D> raycastHits = new List<RaycastHit2D>(64);
        private readonly Dictionary<Collider2D, IDamageable> damageableCache =
            new Dictionary<Collider2D, IDamageable>(32);
        private readonly List<Collider2D> staleCachedColliders = new List<Collider2D>(8);

        private VoidRayWeapon owner;
        private VoidRayWeaponData data;
        private Transform origin;
        private ObjectPoolManager pool;
        private string poolTag;
        private ContactFilter2D collisionFilter;
        private IDamageable currentTarget;
        private MonoBehaviour currentTargetBehaviour;
        private Collider2D currentTargetCollider;
        private Vector2 direction = Vector2.right;
        private Vector2 startPoint;
        private Vector2 endPoint;
        private float elapsed;
        private float fadeRemaining;
        private float visualAlpha = 1f;
        private bool connectedVisual;

        public bool IsFiring { get; private set; }
        public bool IsFading { get; private set; }
        public bool HasHit { get; private set; }
        public bool HasTargetHit => IsFiring && IsCurrentTargetValid();
        public float CurrentLength { get; private set; }
        public Vector2 StartPoint => startPoint;
        public Vector2 EndPoint => endPoint;

        private void Awake()
        {
            EnsureVisual();
            visual.Hide();
        }

        public void Initialize(VoidRayWeapon owner, Transform origin, VoidRayWeaponData data,
            ObjectPoolManager pool)
        {
            this.owner = owner;
            this.origin = origin;
            this.data = data;
            this.pool = pool;
            poolTag = data.projectilePrefab != null ? data.projectilePrefab.name : gameObject.name;
            elapsed = 0f;
            fadeRemaining = 0f;
            visualAlpha = 1f;
            IsFiring = true;
            IsFading = false;
            HasHit = false;
            connectedVisual = false;
            raycastHits.Clear();
            damageableCache.Clear();
            ClearCurrentTarget();

            collisionFilter = new ContactFilter2D { useTriggers = true };
            collisionFilter.SetLayerMask(data.targetLayers | data.obstructionLayers);

            EnsureVisual();
            visual.Initialize(owner.GetComponent<SpriteRenderer>(), data);
            UpdateGeometry();
            ProcessDamageTicks(Time.timeAsDouble);
            UpdateVisual();
        }

        private void LateUpdate()
        {
            if (IsFiring)
            {
                if (owner == null || !owner.isActiveAndEnabled || origin == null ||
                    !origin.gameObject.activeInHierarchy)
                {
                    StopImmediately();
                    return;
                }

                elapsed += Mathf.Max(0f, Time.deltaTime);
                UpdateGeometry();
                ProcessDamageTicks(Time.timeAsDouble);
                UpdateVisual();
                return;
            }

            if (!IsFading) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            fadeRemaining -= Mathf.Max(0f, Time.deltaTime);
            float duration = data != null ? Mathf.Max(0f, data.visualFadeDuration) : 0f;
            visualAlpha = duration > 0f ? Mathf.Clamp01(fadeRemaining / duration) : 0f;
            if (visualAlpha <= 0f)
            {
                FinishAndRelease();
                return;
            }
            UpdateVisual();
        }

        private void UpdateGeometry()
        {
            ClearCurrentTarget();
            if (owner == null || origin == null || data == null) return;

            Vector2 currentDirection = owner.CurrentFireDirection;
            if (currentDirection.sqrMagnitude > 0.0001f)
                direction = currentDirection.normalized;

            startPoint = origin.position;
            float nearestDistance = data.MaxRange;
            Collider2D nearestCollider = null;
            IDamageable nearestTarget = null;
            MonoBehaviour nearestTargetBehaviour = null;

            raycastHits.Clear();
            Physics2D.Raycast(startPoint, direction, collisionFilter, raycastHits, data.MaxRange);
            for (int i = 0; i < raycastHits.Count; i++)
            {
                RaycastHit2D hit = raycastHits[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null || IsCasterCollider(hitCollider)) continue;

                bool targetLayer = IsInLayerMask(hitCollider.gameObject.layer, data.targetLayers);
                bool obstructionLayer = IsInLayerMask(hitCollider.gameObject.layer, data.obstructionLayers);
                IDamageable hitTarget = null;
                MonoBehaviour hitTargetBehaviour = null;

                if (targetLayer)
                {
                    hitTarget = ResolveDamageable(hitCollider);
                    hitTargetBehaviour = hitTarget as MonoBehaviour;
                    if (!IsTargetAlive(hitTarget, hitTargetBehaviour))
                        continue;
                }
                else if (!obstructionLayer || hitCollider.isTrigger)
                {
                    // 적 피격 Trigger만 허용하고 그 밖의 Trigger/레이어는 광선을 막지 않습니다.
                    continue;
                }

                if (hit.distance > nearestDistance) continue;
                nearestDistance = Mathf.Max(0f, hit.distance);
                nearestCollider = hitCollider;
                nearestTarget = hitTarget;
                nearestTargetBehaviour = hitTargetBehaviour;
            }

            CurrentLength = nearestDistance;
            endPoint = startPoint + direction * nearestDistance;
            HasHit = nearestCollider != null;
            currentTargetCollider = nearestTarget != null ? nearestCollider : null;
            currentTarget = nearestTarget;
            currentTargetBehaviour = nearestTargetBehaviour;
        }

        private void ProcessDamageTicks(double now)
        {
            if (!IsFiring || owner == null || data == null) return;
            while (owner != null && owner.TryConsumeDamageTick(now))
            {
                // 밀린 틱 사이에 대상이 이동/사망해도 이전 판정을 재사용하지 않습니다.
                UpdateGeometry();
                if (!IsCurrentTargetValid()) continue;

                IDamageable target = currentTarget;
                MonoBehaviour targetBehaviour = currentTargetBehaviour;
                target.TakeDamage(owner.GetDamagePerTick());
                if (!IsFiring || owner == null) return;
                visual.NotifyDamageTick(elapsed);
                if (IsTargetAlive(target, targetBehaviour))
                    owner.ApplyStatusEffects(target);
                else
                {
                    RemoveCachedTarget(target);
                    ClearCurrentTarget();
                }
                // 죽거나 이동한 적에 굵은 연결선이 한 프레임 더 남지 않게 합니다.
                UpdateGeometry();
            }
        }

        private bool IsCurrentTargetValid()
        {
            return IsTargetAlive(currentTarget, currentTargetBehaviour) && currentTargetCollider != null &&
                currentTargetCollider.enabled && currentTargetCollider.gameObject.activeInHierarchy;
        }

        private IDamageable ResolveDamageable(Collider2D hitCollider)
        {
            if (damageableCache.TryGetValue(hitCollider, out IDamageable cached))
            {
                MonoBehaviour cachedBehaviour = cached as MonoBehaviour;
                if (IsTargetAlive(cached, cachedBehaviour)) return cached;
                damageableCache.Remove(hitCollider);
                return null;
            }
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();
            damageableCache.Add(hitCollider, target);
            return target;
        }

        private static bool IsTargetAlive(IDamageable target, MonoBehaviour behaviour)
        {
            return target != null && behaviour != null && behaviour.isActiveAndEnabled &&
                (!(target is EnemyBase enemy) || !enemy.isDead);
        }

        private void RemoveCachedTarget(IDamageable target)
        {
            staleCachedColliders.Clear();
            foreach (KeyValuePair<Collider2D, IDamageable> entry in damageableCache)
                if (ReferenceEquals(entry.Value, target)) staleCachedColliders.Add(entry.Key);
            for (int i = 0; i < staleCachedColliders.Count; i++)
                damageableCache.Remove(staleCachedColliders[i]);
            staleCachedColliders.Clear();
        }

        private bool IsCasterCollider(Collider2D hitCollider)
        {
            Transform caster = owner != null ? owner.CasterTransform : null;
            return caster != null && hitCollider.transform.IsChildOf(caster);
        }

        private static bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private void UpdateVisual()
        {
            if (IsFiring) connectedVisual = HasTargetHit;
            visual.Render(startPoint, endPoint, direction, HasHit, connectedVisual,
                elapsed, visualAlpha, IsFiring);
        }

        private void EnsureVisual()
        {
            if (visual == null) visual = new VoidRayBeamVisual(transform);
        }

        public void StopFiring()
        {
            if (!IsFiring) return;

            IsFiring = false;
            ClearCurrentTarget();
            VoidRayWeapon releasingOwner = owner;
            owner = null;
            releasingOwner?.OnBeamReleased(this);

            float fadeDuration = data != null ? Mathf.Max(0f, data.visualFadeDuration) : 0f;
            if (fadeDuration <= 0f)
            {
                FinishAndRelease();
                return;
            }

            IsFading = true;
            fadeRemaining = fadeDuration;
        }

        public void StopImmediately()
        {
            if (!IsFiring && !IsFading && owner == null) return;
            VoidRayWeapon releasingOwner = owner;
            owner = null;
            IsFiring = false;
            IsFading = false;
            releasingOwner?.OnBeamReleased(this);
            FinishAndRelease();
        }

        private void FinishAndRelease()
        {
            ObjectPoolManager returnPool = pool;
            string returnTag = poolTag;
            ResetState();
            if (returnPool != null && !string.IsNullOrEmpty(returnTag))
                returnPool.ReturnToPool(returnTag, gameObject);
            else if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        private void ResetState()
        {
            IsFiring = false;
            IsFading = false;
            HasHit = false;
            connectedVisual = false;
            visualAlpha = 0f;
            owner = null;
            origin = null;
            data = null;
            pool = null;
            poolTag = null;
            ClearCurrentTarget();
            raycastHits.Clear();
            damageableCache.Clear();
            staleCachedColliders.Clear();
            if (visual != null) visual.Hide();
        }

        private void ClearCurrentTarget()
        {
            currentTarget = null;
            currentTargetBehaviour = null;
            currentTargetCollider = null;
        }

        private void OnDisable()
        {
            VoidRayWeapon releasingOwner = owner;
            owner = null;
            releasingOwner?.OnBeamReleased(this);
            ResetState();
        }
    }

    /// <summary>지속 광선의 몸통, 발사점과 명중점 표현만 담당합니다.</summary>
    public sealed class VoidRayBeamVisual
    {
        private const int MaxConnectedStrands = 6;
        private const int MaxImpactSparks = 8;

        private readonly Transform root;
        private readonly LineRenderer[] connectedStrands = new LineRenderer[MaxConnectedStrands];
        private readonly LineRenderer[] impactSparkLines = new LineRenderer[MaxImpactSparks];
        private LineRenderer outerLine;
        private LineRenderer secondaryLine;
        private LineRenderer coreLine;
        private SpriteRenderer legacyBeamRenderer;
        private SpriteRenderer startEffectRenderer;
        private SpriteRenderer endEffectRenderer;

        private VoidRayWeaponData data;
        private SpriteRenderer weaponRenderer;
        private bool hasEndpointFrames;
        private bool missingReferenceLogged;
        private float lastDamageElapsed = float.NegativeInfinity;
        private int damageTickSequence;

        public VoidRayBeamVisual(Transform root)
        {
            this.root = root;
            EnsureReferences();
        }

        public void Initialize(SpriteRenderer weaponRenderer, VoidRayWeaponData data)
        {
            this.weaponRenderer = weaponRenderer;
            this.data = data;
            lastDamageElapsed = float.NegativeInfinity;
            damageTickSequence = 0;
            hasEndpointFrames = data != null && data.HasEndpointFrames;
            EnsureReferences();
            ConfigurePointCounts();
            ConfigureSorting(weaponRenderer);
        }

        public void NotifyDamageTick(float elapsed)
        {
            lastDamageElapsed = elapsed;
            damageTickSequence++;
        }

        public void Render(Vector2 startPoint, Vector2 endPoint, Vector2 direction, bool hasHit, bool hasTargetHit,
            float elapsed, float alpha, bool allowJitter)
        {
            if (data == null || outerLine == null || secondaryLine == null || coreLine == null) return;
            ConfigureSorting(weaponRenderer);
            ConfigurePointCounts();

            float phase = elapsed * data.bodyAnimationSpeed;
            // 벽 충돌과 적 연결은 다른 상태입니다. 굵은 몸통은 적에게 닿을 때만 유지합니다.
            float damagePulse = hasTargetHit
                ? 1f - Mathf.Clamp01((elapsed - lastDamageElapsed) / Mathf.Max(0.01f, data.damagePulseDuration))
                : 0f;
            float pulse = 1f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.035f +
                damagePulse * Mathf.Clamp01(data.damagePulseStrength);
            float outerWidth = hasTargetHit ? data.OuterWidth : data.AirOuterWidth;
            float coreWidth = hasTargetHit ? data.CoreWidth : data.AirCoreWidth;
            float jitter = (hasTargetHit ? data.jitterStrength : data.airJitterStrength) *
                (allowJitter ? 1f : 0.7f);
            float beamLength = Vector2.Distance(startPoint, endPoint);
            jitter *= Mathf.Clamp01(beamLength / Mathf.Max(outerWidth * 2f, 0.01f));
            Vector2 normal = new Vector2(-direction.y, direction.x);
            RenderArcGeometry(startPoint, endPoint, normal, elapsed, phase, jitter, alpha, hasTargetHit);
            Color outerColor = Color.Lerp(data.outerColor, data.coreColor, damagePulse * 0.35f);
            ConfigureLineAppearance(outerLine, outerWidth * pulse, outerColor,
                alpha * (hasTargetHit ? data.connectedGlowAlpha : 0.35f));

            int visibleStrands = hasTargetHit ? data.ConnectedStrandCount : 0;
            for (int i = 0; i < connectedStrands.Length; i++)
            {
                if (i >= visibleStrands) continue;
                float widthVariation = 0.82f + Hash01(i, 29) * 0.36f;
                Color strandColor = Color.Lerp(data.outerColor, data.coreColor,
                    0.55f + Hash01(i, 47) * 0.35f);
                ConfigureLineAppearance(connectedStrands[i],
                    data.CoreWidth * data.secondaryArcWidthMultiplier * widthVariation,
                    strandColor, alpha * data.secondaryArcAlpha);
            }
            ConfigureLineAppearance(coreLine, coreWidth * pulse, data.coreColor,
                alpha * (hasTargetHit ? 1f : 0.8f));
            RenderImpactSparks(endPoint, alpha, elapsed, hasTargetHit, damagePulse);

            int frameIndex = GetEndpointFrameIndex(elapsed);
            Sprite endpointSprite = frameIndex >= 0 ? data.startEndFrames[frameIndex] : null;
            float endpointWidth = hasTargetHit ? outerWidth * (1.4f + damagePulse * 0.8f) : outerWidth * 2f;
            UpdateEndpoint(startEffectRenderer, endpointSprite, startPoint, direction, true, alpha,
                endpointWidth, damagePulse);
            UpdateEndpoint(endEffectRenderer, endpointSprite, endPoint, -direction, hasHit, alpha,
                endpointWidth, damagePulse);
        }

        public void Hide()
        {
            if (legacyBeamRenderer != null) legacyBeamRenderer.enabled = false;
            if (outerLine != null) outerLine.enabled = false;
            if (coreLine != null) coreLine.enabled = false;
            for (int i = 0; i < connectedStrands.Length; i++)
                if (connectedStrands[i] != null) connectedStrands[i].enabled = false;
            for (int i = 0; i < impactSparkLines.Length; i++)
                if (impactSparkLines[i] != null) impactSparkLines[i].enabled = false;
            if (startEffectRenderer != null) startEffectRenderer.enabled = false;
            if (endEffectRenderer != null) endEffectRenderer.enabled = false;
        }

        private void RenderArcGeometry(Vector2 startPoint, Vector2 endPoint, Vector2 normal,
            float elapsed, float phase, float jitter, float alpha, bool connected)
        {
            bool visible = Vector2.SqrMagnitude(endPoint - startPoint) > 0.000001f && alpha > 0f;
            outerLine.enabled = visible;
            coreLine.enabled = visible;
            int activeStrands = connected && data.secondaryArcAlpha > 0f ? data.ConnectedStrandCount : 0;
            for (int strandIndex = 0; strandIndex < connectedStrands.Length; strandIndex++)
                connectedStrands[strandIndex].enabled = visible && strandIndex < activeStrands;
            if (!visible) return;

            float samplePosition = elapsed / data.PathRefreshInterval;
            int sampleIndex = Mathf.FloorToInt(samplePosition);
            // 허공의 가는 선은 짧은 간격으로 꺾인 경로를 갱신하고, 연결선은 연속해서 요동칩니다.
            float sampleBlend = connected ? Mathf.SmoothStep(0f, 1f, samplePosition - sampleIndex) : 0f;
            int pointCount = data.VisualSegments;

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)(pointCount - 1);
                Vector2 basePoint = Vector2.Lerp(startPoint, endPoint, t);
                if (i == 0 || i == pointCount - 1)
                {
                    outerLine.SetPosition(i, basePoint);
                    coreLine.SetPosition(i, basePoint);
                    for (int strandIndex = 0; strandIndex < activeStrands; strandIndex++)
                        connectedStrands[strandIndex].SetPosition(i, basePoint);
                    continue;
                }

                // 양 끝은 실제 판정 좌표에 고정합니다. 시각적인 번개로 대상을 탐색하지 않습니다.
                float envelope = Mathf.Sin(t * Mathf.PI);
                float mainNoise = SampleArcOffset(i, sampleIndex, sampleBlend, phase, 3.17f);
                float mainOffset = SnapToPixel(mainNoise * jitter * envelope);

                Vector2 mainPoint = basePoint + normal * mainOffset;
                outerLine.SetPosition(i, mainPoint);
                coreLine.SetPosition(i, mainPoint);
                for (int strandIndex = 0; strandIndex < activeStrands; strandIndex++)
                {
                    float strandNoise = SampleArcOffset(i, sampleIndex, sampleBlend, phase,
                        17.43f + strandIndex * 13.71f);
                    float strandBias = activeStrands > 1
                        ? (strandIndex / (float)(activeStrands - 1) - 0.5f) * 0.35f
                        : 0f;
                    float strandOffset = SnapToPixel(
                        (mainNoise * 0.18f + strandNoise * 0.82f + strandBias) * jitter *
                        data.connectedStrandSpreadMultiplier * envelope);
                    connectedStrands[strandIndex].SetPosition(i,
                        basePoint + normal * strandOffset);
                }
            }
        }

        private void RenderImpactSparks(Vector2 hitPoint, float alpha, float elapsed,
            bool connected, float damagePulse)
        {
            float duration = Mathf.Max(0.01f, data.impactSparkDuration);
            float age = elapsed - lastDamageElapsed;
            float life = connected && age >= 0f ? 1f - Mathf.Clamp01(age / duration) : 0f;
            int sparkCount = life > 0f ? data.ImpactSparkCount : 0;

            for (int i = 0; i < impactSparkLines.Length; i++)
            {
                LineRenderer spark = impactSparkLines[i];
                bool visible = i < sparkCount;
                spark.enabled = visible;
                if (!visible) continue;

                float angle = Hash01(damageTickSequence, i * 19 + 7) * 360f;
                Vector2 sparkDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                float lengthVariation = 0.55f + Hash01(damageTickSequence, i * 31 + 11) * 0.65f;
                float length = data.impactSparkLength * lengthVariation * life;
                Vector2 sparkStart = hitPoint + sparkDirection * SnapToPixel(data.CoreWidth * 0.35f);
                Vector2 sparkEnd = hitPoint + sparkDirection * SnapToPixel(length);
                spark.SetPosition(0, sparkStart);
                spark.SetPosition(1, sparkEnd);

                Color sparkColor = Color.Lerp(data.outerColor, Color.white,
                    0.55f + damagePulse * 0.45f);
                ConfigureLineAppearance(spark, data.impactSparkWidth * (0.45f + life * 0.55f),
                    sparkColor, alpha * life);
            }
        }

        private static float SampleArcOffset(int pointIndex, int sampleIndex, float sampleBlend,
            float phase, float seed)
        {
            float current = ArcNoise(pointIndex, sampleIndex, (uint)(seed * 1000f));
            float next = ArcNoise(pointIndex, sampleIndex + 1, (uint)(seed * 1000f));
            float steppedNoise = Mathf.Lerp(current, next, sampleBlend);
            float flowingNoise = Mathf.Sin(pointIndex * 0.85f - phase * 0.45f + seed);
            return steppedNoise * 0.6f + flowingNoise * 0.4f;
        }

        private static float ArcNoise(int pointIndex, int sampleIndex, uint seed)
        {
            // 전역 Random 상태를 변경하지 않고 각 마디에 독립적인 꺾임을 만듭니다.
            unchecked
            {
                uint value = (uint)pointIndex * 0x1f123bb5u ^ (uint)sampleIndex * 0x05491333u ^ seed;
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0xffffu) / 32767.5f - 1f;
            }
        }

        private static float Hash01(int first, int second)
        {
            unchecked
            {
                uint value = (uint)first * 0x9e3779b9u ^ (uint)second * 0x85ebca6bu ^ 0xc2b2ae35u;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                return (value & 0xffffu) / 65535f;
            }
        }

        private static void ConfigureLineAppearance(LineRenderer line, float width, Color color, float alpha)
        {
            Color visibleColor = color;
            visibleColor.a *= alpha;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = visibleColor;
            line.endColor = visibleColor;
        }

        private static float SnapToPixel(float value)
        {
            // GameScene PixelPerfectCamera의 Assets PPU(64)에 맞춰 중간 흔들림만 픽셀 단위로 정렬합니다.
            return Mathf.Round(value * 64f) / 64f;
        }

        private int GetEndpointFrameIndex(float elapsed)
        {
            if (!hasEndpointFrames) return -1;
            float framesPerSecond = Mathf.Max(1f, data.bodyAnimationSpeed);
            return Mathf.FloorToInt(elapsed * framesPerSecond) % data.startEndFrames.Length;
        }

        private void UpdateEndpoint(SpriteRenderer renderer, Sprite sprite, Vector2 position,
            Vector2 facingDirection, bool visible, float alpha, float width, float damagePulse)
        {
            if (renderer == null) return;
            renderer.enabled = visible && sprite != null && alpha > 0f;
            if (!renderer.enabled) return;

            renderer.sprite = sprite;
            Color color = Color.Lerp(data.coreColor, Color.white, damagePulse);
            color.a = alpha;
            renderer.color = color;
            renderer.transform.position = position;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg);
            float spriteHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
            float scale = width / spriteHeight;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void EnsureReferences()
        {
            if (legacyBeamRenderer == null)
            {
                Transform legacyVisual = root.Find("Visual");
                if (legacyVisual != null) legacyBeamRenderer = legacyVisual.GetComponent<SpriteRenderer>();
            }
            if (startEffectRenderer == null)
            {
                Transform start = root.Find("StartEffect");
                if (start != null) startEffectRenderer = start.GetComponent<SpriteRenderer>();
            }
            if (endEffectRenderer == null)
            {
                Transform end = root.Find("EndEffect");
                if (end != null) endEffectRenderer = end.GetComponent<SpriteRenderer>();
            }
            if (outerLine == null) outerLine = FindOrCreateLine("OuterLine");
            if (secondaryLine == null) secondaryLine = FindOrCreateLine("SecondaryLine");
            if (coreLine == null) coreLine = FindOrCreateLine("CoreLine");
            connectedStrands[0] = secondaryLine;
            for (int i = 1; i < connectedStrands.Length; i++)
                if (connectedStrands[i] == null)
                    connectedStrands[i] = FindOrCreateLine("ConnectedStrand" + (i + 1));
            for (int i = 0; i < impactSparkLines.Length; i++)
                if (impactSparkLines[i] == null)
                    impactSparkLines[i] = FindOrCreateLine("ImpactSpark" + (i + 1), 2);
            if (legacyBeamRenderer != null) legacyBeamRenderer.enabled = false;

            if ((startEffectRenderer == null || endEffectRenderer == null) && !missingReferenceLogged)
            {
                missingReferenceLogged = true;
                Debug.LogError("[VoidRayBeamVisual] StartEffect/EndEffect SpriteRenderer 참조를 확인해 주세요.", root);
            }
        }

        private LineRenderer FindOrCreateLine(string childName, int positionCount = 4)
        {
            Transform child = root.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(root, false);
            }

            LineRenderer line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = positionCount;
            line.loop = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            if (legacyBeamRenderer != null && legacyBeamRenderer.sharedMaterial != null)
                line.sharedMaterial = legacyBeamRenderer.sharedMaterial;
            return line;
        }

        private void ConfigurePointCounts()
        {
            if (data == null) return;
            int pointCount = data.VisualSegments;
            if (outerLine.positionCount != pointCount) outerLine.positionCount = pointCount;
            if (coreLine.positionCount != pointCount) coreLine.positionCount = pointCount;
            for (int i = 0; i < connectedStrands.Length; i++)
                if (connectedStrands[i].positionCount != pointCount)
                    connectedStrands[i].positionCount = pointCount;
            for (int i = 0; i < impactSparkLines.Length; i++)
                if (impactSparkLines[i].positionCount != 2)
                    impactSparkLines[i].positionCount = 2;
        }

        private void ConfigureSorting(SpriteRenderer weaponRenderer)
        {
            int sortingLayerId = weaponRenderer != null ? weaponRenderer.sortingLayerID : 0;
            int baseOrder = weaponRenderer != null ? weaponRenderer.sortingOrder : 0;
            outerLine.sortingLayerID = sortingLayerId;
            outerLine.sortingOrder = baseOrder + 1;
            for (int i = 0; i < connectedStrands.Length; i++)
            {
                connectedStrands[i].sortingLayerID = sortingLayerId;
                connectedStrands[i].sortingOrder = baseOrder + 2;
            }
            coreLine.sortingLayerID = sortingLayerId;
            coreLine.sortingOrder = baseOrder + 3;
            PrepareEndpoint(startEffectRenderer, sortingLayerId, baseOrder + 4);
            PrepareEndpoint(endEffectRenderer, sortingLayerId, baseOrder + 4);
            for (int i = 0; i < impactSparkLines.Length; i++)
            {
                impactSparkLines[i].sortingLayerID = sortingLayerId;
                impactSparkLines[i].sortingOrder = baseOrder + 5;
            }
        }

        private static void PrepareEndpoint(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
        {
            if (renderer == null) return;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
