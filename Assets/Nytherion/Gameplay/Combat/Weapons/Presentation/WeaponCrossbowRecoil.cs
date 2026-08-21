using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 원거리 무기 프리팹에 부착해 사용하는 석궁식 반동 연출 컴포넌트입니다.
    /// 발사 직후 무기를 발사 반대 방향으로 밀고, 원래 위치와 각도로 되돌립니다.
    /// </summary>
    public class WeaponCrossbowRecoil : MonoBehaviour
    {
        [Header("Crossbow Recoil Settings")]
        [SerializeField, Min(0f)] private float recoilDistance = 0.08f;
        [SerializeField, Min(0f)] private float kickRotationDegrees = 4f;
        [SerializeField, Min(0.01f)] private float returnDuration = 0.12f;

        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;
        private Vector3 recoilLocalOffset;
        private float recoilRotationOffset;
        private bool hasRestPose;

        private void Awake()
        {
            CacheRestPose();
        }

        /// <summary>
        /// 장착 과정에서 적용된 시각 위치/회전 오프셋을 반동의 기준 자세로 저장합니다.
        /// </summary>
        public void CacheRestPose()
        {
            restLocalPosition = transform.localPosition;
            restLocalRotation = transform.localRotation;
            recoilLocalOffset = Vector3.zero;
            recoilRotationOffset = 0f;
            hasRestPose = true;
        }

        public void Play(Vector2 fireDirection, float strength = 1f)
        {
            if (fireDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            if (!hasRestPose)
            {
                CacheRestPose();
            }

            Transform parentTransform = transform.parent;
            Vector3 worldBackDirection = -(Vector3)fireDirection.normalized;
            Vector3 localBackDirection = parentTransform != null
                ? parentTransform.InverseTransformVector(worldBackDirection).normalized
                : worldBackDirection;

            float clampedStrength = Mathf.Max(0f, strength);
            recoilLocalOffset = localBackDirection * recoilDistance * clampedStrength;
            recoilRotationOffset = kickRotationDegrees * clampedStrength;
        }

        private void LateUpdate()
        {
            if (!hasRestPose)
            {
                return;
            }

            float positionReturnSpeed = recoilDistance / returnDuration;
            float rotationReturnSpeed = kickRotationDegrees / returnDuration;

            recoilLocalOffset = Vector3.MoveTowards(
                recoilLocalOffset,
                Vector3.zero,
                positionReturnSpeed * Time.deltaTime);
            recoilRotationOffset = Mathf.MoveTowards(
                recoilRotationOffset,
                0f,
                rotationReturnSpeed * Time.deltaTime);

            transform.localPosition = restLocalPosition + recoilLocalOffset;
            transform.localRotation = restLocalRotation * Quaternion.Euler(0f, 0f, recoilRotationOffset);
        }

        private void OnDisable()
        {
            if (!hasRestPose)
            {
                return;
            }

            transform.localPosition = restLocalPosition;
            transform.localRotation = restLocalRotation;
            recoilLocalOffset = Vector3.zero;
            recoilRotationOffset = 0f;
        }
    }
}
