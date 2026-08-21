using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public static class WeaponVFXHelper
    {
        public static void PlayHitEffect(GameObject effectPrefab, Vector3 position, float chargePercent = 0f, Vector3? direction = null)
        {
            if (effectPrefab == null) return;

            // 일반 공격(chargePercent=0) 시 0.6 ~ 1.0
            // 최대 차징(chargePercent=1) 시 1.0 ~ 1.4
            float minScale = Mathf.Lerp(0.6f, 1.0f, chargePercent);
            float maxScale = Mathf.Lerp(1.0f, 1.4f, chargePercent);
            float randomScale = Random.Range(minScale, maxScale);
            
            // atlas의 hiteffect는 이미지가 아래에서 위로 뻗어나가므로 회전을 고정하거나 공격 방향에 정렬합니다.
            Quaternion targetRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            if (effectPrefab.name.Contains("AtlasHitEffect"))
            {
                if (direction.HasValue && direction.Value != Vector3.zero)
                {
                    Vector3 dir2D = new Vector3(direction.Value.x, direction.Value.y, 0f).normalized;
                    if (dir2D != Vector3.zero)
                    {
                        targetRotation = Quaternion.FromToRotation(Vector3.up, dir2D);
                    }
                    else
                    {
                        targetRotation = Quaternion.identity;
                    }
                }
                else
                {
                    targetRotation = Quaternion.identity;
                }
            }

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
