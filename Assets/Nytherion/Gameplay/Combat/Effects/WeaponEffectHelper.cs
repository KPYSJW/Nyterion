using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public static class WeaponEffectHelper
    {
        public static void PlayHitEffect(GameObject effectPrefab, Vector3 position)
        {
            if (effectPrefab == null) return;

            // 방법 A 적용: 크기 0.8~1.2 랜덤화, Z축 회전 0~360도 랜덤화
            float randomScale = Random.Range(0.8f, 1.2f);
            Quaternion randomRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            GameObject effectObj = null;
            if (ObjectPoolManager.Instance != null)
            {
                effectObj = ObjectPoolManager.Instance.SpawnFromPool(effectPrefab, position, randomRotation);
            }
            else
            {
                effectObj = Object.Instantiate(effectPrefab, position, randomRotation);
            }

            if (effectObj != null)
            {
                effectObj.transform.localScale = new Vector3(randomScale, randomScale, 1f);

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
