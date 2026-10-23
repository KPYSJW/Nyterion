using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "PortalGate_Skill", menuName = "Data/Skill/Portal Gate")]
    public class PortalGateSkillData : SkillData
    {
        [Header("포탈 배치")]
        [Tooltip("포탈과 플레이어의 점유 공간에 더해 화면 가장자리 안쪽으로 확보할 여유입니다.")]
        [Min(0f)] public float screenEdgePadding = 0.1f;
        [Tooltip("포탈의 그림과 플레이어 충돌체를 모두 포함한 공간으로 벽을 검사합니다.")]
        [Min(0.1f)] public float placementRadius = 0.45f;
        [Min(0.01f)] public float wallPadding = 0.08f;
        [Tooltip("두 포탈이 겹칠 만큼 가까우면 시전을 취소하고 쿨타임을 소비하지 않습니다.")]
        [Min(0.1f)] public float minimumSeparation = 1.3f;
        public LayerMask blockingLayers = (1 << 9) | (1 << 11);

        [Header("포탈 이용")]
        [Min(0.1f)] public float lifetime = 15f;
        [Tooltip("두 포탈이 공유하는 총 순간이동 횟수입니다. 성공한 이동만 차감합니다.")]
        [Min(1)] public int maxTraversals = 6;
        [Min(0.1f)] public float interactionRadius = 0.6f;
        [Min(0.1f)] public float traversalDelay = 0.35f;

        [Header("연출")]
        public PortalGateEndpoint portalPrefab;
        public Sprite[] animationFrames;
        [Min(1f)] public float framesPerSecond = 12f;
    }
}
