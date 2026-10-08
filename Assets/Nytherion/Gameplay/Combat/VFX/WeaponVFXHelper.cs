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
