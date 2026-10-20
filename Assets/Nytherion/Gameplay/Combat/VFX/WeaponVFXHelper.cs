using UnityEngine;
using Nytherion.Core.Managers;
using UnityEngine.Rendering;

namespace Nytherion.GamePlay.Combat
{
    public static class WeaponVFXHelper
    {
        // 적 본체와 비행 적(정렬 순서 10)보다 앞에 타격 효과를 표시합니다.
        public const int HitEffectSortingOrder = 100;
        // 바닥 타일(-10) 위, 적 렌더러(-5 이상) 아래에 광선을 표시합니다.
        public const int BackgroundBeamSortingOrder = -8;

        public static void ConfigureBeamSorting(GameObject root, Renderer beam, SpriteRenderer start, SpriteRenderer impact)
        {
            if (!root.TryGetComponent(out SortingGroup group)) group = root.AddComponent<SortingGroup>();
            group.sortAtRoot = true;
            group.sortingLayerID = beam.sortingLayerID;
            group.sortingOrder = BackgroundBeamSortingOrder;
            beam.sortingOrder = BackgroundBeamSortingOrder;
            if (start != null)
            {
                start.sortingLayerID = beam.sortingLayerID;
                start.sortingOrder = BackgroundBeamSortingOrder + 1;
            }
            if (impact == null) return;
            // 타격 효과는 공격 본체의 그룹에서 독립시켜 적보다 앞에 표시합니다.
            if (!impact.TryGetComponent(out SortingGroup impactGroup)) impactGroup = impact.gameObject.AddComponent<SortingGroup>();
            impactGroup.sortAtRoot = true;
            impactGroup.sortingLayerID = beam.sortingLayerID;
            impactGroup.sortingOrder = HitEffectSortingOrder;
            impact.sortingLayerID = beam.sortingLayerID;
            impact.sortingOrder = HitEffectSortingOrder;
        }

        public static void ApplyHitEffectSorting(GameObject effect)
        {
            foreach (Renderer effectRenderer in effect.GetComponentsInChildren<Renderer>(true))
                effectRenderer.sortingOrder = Mathf.Max(HitEffectSortingOrder, effectRenderer.sortingOrder);
        }

        public static void PlayHitEffect(GameObject effectPrefab, Vector3 position, float chargePercent = 0f, Vector3? direction = null)
        {
            if (effectPrefab == null) return;

            // 일반 공격(chargePercent=0) 시 0.6 ~ 1.0
            // 최대 차징(chargePercent=1) 시 1.0 ~ 1.4
            float minScale = Mathf.Lerp(0.6f, 1.0f, chargePercent);
            float maxScale = Mathf.Lerp(1.0f, 1.4f, chargePercent);
            float randomScale = Random.Range(minScale, maxScale);
            
            Quaternion targetRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            GameObject effectObj = null;
            if (ObjectPoolManager.Instance != null)
            {
                effectObj = ObjectPoolManager.Instance.SpawnFromPool(effectPrefab, position, targetRotation);
            }
            else
            {
                effectObj = Object.Instantiate(effectPrefab, position, targetRotation);
            }

            if (effectObj != null)
            {
                ApplyHitEffectSorting(effectObj);

                Vector3 originalScale = effectPrefab.transform.localScale;
                effectObj.transform.localScale = new Vector3(originalScale.x * randomScale, originalScale.y * randomScale, originalScale.z);

                // AutoReturnToPool 컴포넌트 검사 및 대기 지연 초기화
                AutoReturnToPool autoReturn;
                if (!effectObj.TryGetComponent<AutoReturnToPool>(out autoReturn))
                {
                    autoReturn = effectObj.AddComponent<AutoReturnToPool>();
                    autoReturn.InitializeDelay(0.5f); // 기본 0.5초 대기 후 풀 반환
                }
            }
        }

        public static void PlayFireEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (effectPrefab == null) return;

            GameObject effectObj = null;
            if (ObjectPoolManager.Instance != null)
            {
                effectObj = ObjectPoolManager.Instance.SpawnFromPool(effectPrefab, position, rotation);
            }
            else
            {
                effectObj = Object.Instantiate(effectPrefab, position, rotation);
            }

            if (effectObj != null)
            {
                if (effectObj.TryGetComponent<SpriteMuzzleFlashEffect>(out var spriteFlash))
                {
                    spriteFlash.Play(ObjectPoolManager.Instance, parent);
                    return;
                }

                if (parent != null)
                {
                    effectObj.transform.SetParent(parent);
                }

                // AutoReturnToPool 컴포넌트 검사 및 대기 지연 초기화
                AutoReturnToPool autoReturn;
                if (!effectObj.TryGetComponent<AutoReturnToPool>(out autoReturn))
                {
                    autoReturn = effectObj.AddComponent<AutoReturnToPool>();
                    autoReturn.InitializeDelay(0.5f); // 기본 0.5초 대기 후 풀 반환
                }
            }
        }
    }
}
