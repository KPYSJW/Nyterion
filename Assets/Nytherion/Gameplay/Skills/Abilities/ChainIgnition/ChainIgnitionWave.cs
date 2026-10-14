using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>시전 위치에 고정된 세 파동의 연출과 피해를 관리하고, 종료 후 로컬 풀에 반환합니다.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class ChainIgnitionWave : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] explosionRenderers;

        private readonly bool[] damagedWaves = new bool[ChainIgnitionSkillData.WaveCount];
        private readonly bool[] playedSoundWaves = new bool[ChainIgnitionSkillData.WaveCount];
        private readonly bool[] activeDirections = new bool[ChainIgnitionSkillData.DirectionCount];
        private readonly float[] directionAngles = new float[ChainIgnitionSkillData.DirectionCount];
        private readonly List<int> tiedDirections = new List<int>(ChainIgnitionSkillData.DirectionCount);
        private const int HitRangeSegments = 64;
        private readonly LineRenderer[] hitRangeRenderers = new LineRenderer[ChainIgnitionSkillData.WaveCount * ChainIgnitionSkillData.DirectionCount];
        private readonly Vector3[] hitRangePoints = new Vector3[HitRangeSegments];
        private readonly Vector3[] explosionGroundPositions = new Vector3[ChainIgnitionSkillData.WaveCount * ChainIgnitionSkillData.DirectionCount];
        private readonly HashSet<IDamageable> waveTargets = new HashSet<IDamageable>();
        private readonly List<Collider2D> overlapResults = new List<Collider2D>(32);
        private ChainIgnitionSkillData data;
        private ContactFilter2D targetFilter;
        private float startTime;
        private AudioSource explosionAudio;
        private float castDamage;
        private float castExplosionRadius;
        private Vector2 castExplosionRadii;
        private int castWaveCount;

        private void Awake()
        {
            explosionAudio = GetComponent<AudioSource>();
            explosionAudio.playOnAwake = false;
            explosionAudio.loop = false;
            explosionAudio.spatialBlend = 0f;
        }

        public void Begin(ChainIgnitionSkillData skill, Vector3 center, Vector2 aimDirection,
            int projectileCount, float damage, float sizeMultiplier = 1f, float rangeMultiplier = 1f,
            bool singleExplosion = false)
        {
            if (skill == null || !skill.HasValidAnimation || explosionRenderers == null ||
                explosionRenderers.Length != ChainIgnitionSkillData.WaveCount * ChainIgnitionSkillData.DirectionCount)
            {
                Debug.LogError("[ChainIgnitionWave] 폭발 애니메이션 또는 24개 렌더러 연결을 확인해 주세요.", this);
                gameObject.SetActive(false);
                return;
            }

            data = skill;
            castWaveCount = singleExplosion ? 1 : ChainIgnitionSkillData.WaveCount;
            castDamage = Mathf.Max(0f, damage);
            sizeMultiplier = Mathf.Max(0.01f, sizeMultiplier);
            rangeMultiplier = Mathf.Max(0.01f, rangeMultiplier);
            castExplosionRadii = data.GetExplosionRadii(sizeMultiplier);
            castExplosionRadius = Mathf.Max(castExplosionRadii.x, castExplosionRadii.y);
            for (int point = 0; point < HitRangeSegments; point++)
            {
                float angle = point * Mathf.PI * 2f / HitRangeSegments;
                hitRangePoints[point] = new Vector3(Mathf.Cos(angle) * castExplosionRadii.x, Mathf.Sin(angle) * castExplosionRadii.y, 0f);
            }
            SelectDirections(singleExplosion ? Vector2.right : aimDirection,
                singleExplosion ? 1 : Mathf.Clamp(projectileCount, 1, ChainIgnitionSkillData.DirectionCount));
            transform.SetPositionAndRotation(center, Quaternion.identity);
            transform.localScale = Vector3.one;
            targetFilter = new ContactFilter2D { useTriggers = true };
            targetFilter.SetLayerMask(data.targetLayers);
            startTime = Time.time;
            for (int wave = 0; wave < ChainIgnitionSkillData.WaveCount; wave++)
            {
                damagedWaves[wave] = false;
                playedSoundWaves[wave] = false;
                for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
                {
                    float angle = direction * Mathf.PI * 2f / ChainIgnitionSkillData.DirectionCount;
                    int index = wave * ChainIgnitionSkillData.DirectionCount + direction;
                    SpriteRenderer renderer = explosionRenderers[index];
                    explosionGroundPositions[index] = singleExplosion ? Vector3.zero :
                        new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * data.GetRingRadius(wave) * rangeMultiplier;
                    float visualScale = Mathf.Max(0.01f, data.visualScale * sizeMultiplier);
                    renderer.transform.localPosition = explosionGroundPositions[index] + (Vector3)data.explosionVisualOffset * visualScale;
                    renderer.transform.localScale = Vector3.one * visualScale;
                    renderer.sprite = data.explosionFrames[0];
                    renderer.enabled = false;
                    if (hitRangeRenderers[index] != null)
                    {
                        hitRangeRenderers[index].SetPositions(hitRangePoints);
                        hitRangeRenderers[index].enabled = false;
                    }
                }
            }
            gameObject.SetActive(true);
            Update();
        }

        private void SelectDirections(Vector2 aimDirection, int count)
        {
            for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
            {
                activeDirections[direction] = false;
                float angle = direction * Mathf.PI * 2f / ChainIgnitionSkillData.DirectionCount;
                directionAngles[direction] = aimDirection.sqrMagnitude > 0.0001f
                    ? Vector2.Angle(aimDirection, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))) : 0f;
            }
            // 시전 때 한 번 선택해 세 파동 모두 같은 방향을 유지합니다. 동률 비교 중에는 무작위 정렬을 하지 않습니다.
            for (int selected = 0; selected < count; selected++)
            {
                float nearestAngle = float.PositiveInfinity;
                for (int direction = 0; direction < activeDirections.Length; direction++)
                    if (!activeDirections[direction]) nearestAngle = Mathf.Min(nearestAngle, directionAngles[direction]);
                tiedDirections.Clear();
                for (int direction = 0; direction < activeDirections.Length; direction++)
                    if (!activeDirections[direction] && directionAngles[direction] <= nearestAngle + Mathf.Clamp(data.directionTieAngle, 0f, 22.5f) + 0.001f)
                        tiedDirections.Add(direction);
                activeDirections[tiedDirections.Count == 1 ? tiedDirections[0] : tiedDirections[Random.Range(0, tiedDirections.Count)]] = true;
            }
        }

        private void Update()
        {
            if (data == null) return;
            float elapsed = Time.time - startTime;
            for (int wave = 0; wave < castWaveCount; wave++)
            {
                float waveElapsed = elapsed - wave * data.WaveInterval;
                if (!playedSoundWaves[wave] && waveElapsed >= 0f)
                {
                    playedSoundWaves[wave] = true;
                    if (data.explosionSound != null)
                    {
                        explosionAudio.volume = UserSettings.GetSfxVolume();
                        explosionAudio.PlayOneShot(data.explosionSound, Mathf.Clamp01(data.explosionSoundVolume));
                    }
                }
                bool visible = waveElapsed >= 0f && waveElapsed < data.AnimationDuration;
                int frame = Mathf.Clamp(Mathf.FloorToInt(waveElapsed * Mathf.Max(1f, data.framesPerSecond)),
                    0, data.explosionFrames.Length - 1);
                for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
                {
                    SpriteRenderer renderer = explosionRenderers[wave * ChainIgnitionSkillData.DirectionCount + direction];
                    renderer.enabled = visible && activeDirections[direction];
                    if (renderer.enabled) renderer.sprite = data.explosionFrames[frame];
                    UpdateHitRange(wave * ChainIgnitionSkillData.DirectionCount + direction, renderer);
                }

                // 프레임이 건너뛰어져도 각 파동의 피해를 정확히 한 번 처리합니다.
                if (!damagedWaves[wave] && waveElapsed >= data.DamageDelay)
                {
                    damagedWaves[wave] = true;
                    DealWaveDamage(wave);
                }
            }
            // 마지막 효과음의 잔향이 끝난 뒤 반환하여 애니메이션 종료 시 소리가 잘리지 않게 합니다.
            if (elapsed >= (castWaveCount - 1) * data.WaveInterval + data.AnimationDuration &&
                !explosionAudio.isPlaying)
                gameObject.SetActive(false);
        }

        private void UpdateHitRange(int index, SpriteRenderer explosion)
        {
            LineRenderer outline = hitRangeRenderers[index];
            if (!data.showHitRanges || !explosion.enabled)
            {
                if (outline != null) outline.enabled = false;
                return;
            }
            if (outline == null)
            {
                GameObject obj = new GameObject($"HitRange_{index / ChainIgnitionSkillData.DirectionCount + 1}_{index % ChainIgnitionSkillData.DirectionCount + 1}");
                obj.transform.SetParent(transform, false);
                outline = obj.AddComponent<LineRenderer>();
                outline.useWorldSpace = false;
                outline.loop = true;
                outline.positionCount = HitRangeSegments;
                outline.SetPositions(hitRangePoints);
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                // 기존 스프라이트의 공용 머티리얼을 사용하므로 별도 머티리얼 인스턴스를 만들지 않습니다.
                outline.sharedMaterial = explosion.sharedMaterial;
                hitRangeRenderers[index] = outline;
            }
            // 스프라이트의 표시 배율을 다시 곱하지 않고 실제 판정의 가로·세로 반경을 사용합니다.
            outline.transform.localPosition = explosionGroundPositions[index];
            outline.widthMultiplier = Mathf.Max(0.001f, data.hitRangeLineWidth);
            outline.startColor = outline.endColor = data.hitRangeColor;
            outline.sortingLayerID = explosion.sortingLayerID;
            outline.sortingOrder = explosion.sortingOrder + 1;
            outline.enabled = true;
        }

        private void DealWaveDamage(int wave)
        {
            // 적 이동·디버그 소환 직후에도 화면에 표시된 최신 위치로 검색합니다.
            Physics2D.SyncTransforms();
            waveTargets.Clear();
            for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
            {
                if (!activeDirections[direction]) continue;
                Vector3 position = transform.TransformPoint(explosionGroundPositions[wave * ChainIgnitionSkillData.DirectionCount + direction]);
                overlapResults.Clear();
                Physics2D.OverlapCircle(position, castExplosionRadius, targetFilter, overlapResults);
                foreach (Collider2D hit in overlapResults)
                {
                    if (hit == null) continue;
                    IDamageable target = hit.GetComponentInParent<IDamageable>();
                    // 공격용 콜라이더를 제외하고 일반 공격과 같은 몸체의 전체 크기로 판정합니다.
                    if (target is IGroundDamageable groundTarget)
                    {
                        if (!groundTarget.TryGetGroundHitBounds(out Bounds groundBounds) ||
                            !ChainIgnitionHitRange.OverlapsBounds(position, castExplosionRadii, groundBounds))
                            continue;
                    }
                    else if (!Mathf.Approximately(castExplosionRadii.x, castExplosionRadii.y))
                    {
                        // 지면 영역을 제공하지 않는 대상은 기존 콜라이더의 접촉 위치를 사용합니다.
                        Vector2 contact = hit.ClosestPoint(position);
                        if (hit is CircleCollider2D circle)
                        {
                            Vector2 circleCenter = circle.transform.TransformPoint(circle.offset);
                            float radius = Mathf.Max(circle.bounds.extents.x, circle.bounds.extents.y);
                            if (!ChainIgnitionHitRange.OverlapsCircle(circleCenter - (Vector2)position, castExplosionRadii, radius)) continue;
                        }
                        else if (!ChainIgnitionHitRange.OverlapsCircle(contact - (Vector2)position, castExplosionRadii, 0f)) continue;
                    }
                    if (target is Component component && component.gameObject.activeInHierarchy && waveTargets.Add(target))
                        target.TakeDamage(castDamage);
                }
            }
        }

        private void OnDisable()
        {
            if (explosionAudio != null) explosionAudio.Stop();
            data = null;
            waveTargets.Clear();
            overlapResults.Clear();
            foreach (LineRenderer outline in hitRangeRenderers)
                if (outline != null) outline.enabled = false;
            if (explosionRenderers == null) return;
            foreach (SpriteRenderer renderer in explosionRenderers)
                if (renderer != null) renderer.enabled = false;
        }
    }
}
