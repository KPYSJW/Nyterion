using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>폭발 이미지의 생성 위치를 중심 주변에서 선택하고 재생 중에는 고정합니다.</summary>
    public sealed class EvilEyeHitDrift : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [Tooltip("생성할 때 중심 주변에서 위치를 선택하는 반경입니다.")]
        [SerializeField, Min(0f)] private float hitEffectOffsetRadius = 0.18f;

        private void OnEnable()
        {
            if (visual == null) return;
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, hitEffectOffsetRadius);
            visual.localPosition = new Vector3(offset.x, offset.y, 0f);
        }

        public void SetSpawnCenter(Vector3 center)
        {
            if (visual == null) return;
            // 폭발 판정 위치는 유지하고 생성 시 선택한 오프셋의 기준점만 변경합니다.
            Vector3 offset = transform.TransformVector(visual.localPosition);
            visual.position = center + offset;
        }

        private void OnDisable()
        {
            if (visual != null) visual.localPosition = Vector3.zero;
        }
    }
}
