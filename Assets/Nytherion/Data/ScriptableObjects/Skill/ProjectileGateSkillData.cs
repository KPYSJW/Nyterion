using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "ProjectileGate_Skill", menuName = "Data/Skill/Projectile Gate")]
    public class ProjectileGateSkillData : SkillData
    {
        [Header("레벨별 쿨타임 감소")]
        [Tooltip("레벨이 1 오를 때 감소하는 쿨타임(초)입니다. 게이트의 나머지 수치는 레벨에 따라 증가하지 않습니다.")]
        [Min(0f)] public float cooldownReductionPerLevel = 1f;
        [Min(0.01f)] public float minimumCooldown = 2f;

        public float GetCooldown(int level)
        {
            float baseCooldown = Mathf.Max(0.01f, coolDown);
            float lowerBound = Mathf.Clamp(minimumCooldown, 0.01f, baseCooldown);
            return Mathf.Max(lowerBound,
                baseCooldown - Mathf.Max(0, level - 1) * Mathf.Max(0f, cooldownReductionPerLevel));
        }

        [Header("게이트 배치")]
        [Min(0.1f)] public float forwardDistance = 1f;
        [Min(0.1f)] public float halfWidth = 0.75f;
        [Min(0.01f)] public float lifetime = 6f;
        [Min(0.01f)] public float detectionThickness = 0.12f;
        [Tooltip("양 끝 구조물의 안쪽 경계에서 추가로 확보할 발사 여백입니다. 투사체와 레이저의 출구에만 적용합니다.")]
        [Min(0f)] public float outputEndpointPadding = 0.25f;

        [Header("통과한 투사체")]
        [Range(2, 32)] public int outputCount = 3;
        [Min(1.01f)] public float speedMultiplier = 1.8f;
        [Range(0.05f, 0.99f)] public float sizeMultiplier = 0.65f;
        [Tooltip("분열탄의 최대 비행 시간입니다. 기존 투사체의 충돌·피해·관통 효과는 유지합니다.")]
        [Min(0.1f)] public float outputLifetime = 5f;
        [Tooltip("분열탄에만 적용할 머티리얼입니다. 붉은색과 혼합 강도는 머티리얼에서 조절합니다.")]
        public Material outputProjectileMaterial;

        [Header("분열 발사 각도 (투사체·레이저)")]
        [Tooltip("게이트 정면을 기준으로 분열된 투사체와 레이저가 좌우로 꺾일 수 있는 최대 각도입니다. 0이면 모두 정면으로 발사합니다.")]
        [Range(0f, 89f)] public float laserSpreadHalfAngle = 45f;

        [Header("연출")]
        public ProjectileGateController gatePrefab;
        public AnimationClip beamAnimation;
        [Min(0.01f)] public float endpointScale = 1f;
        [Tooltip("일반 투사체가 진입할 때 무작위로 한 개를 재생합니다.")]
        public ProjectileGateEntryEffect[] entryEffectPrefabs;
        [Min(0.01f)] public float entryEffectScale = 1f;
        [Tooltip("분열탄이 생성되는 각 출구에서 발사 방향으로 한 번 재생합니다.")]
        public ProjectileGateEntryEffect outputEffectPrefab;
        [Min(0.01f)] public float outputEffectScale = 1f;

        // 새 에셋을 생성할 때만 호출합니다. 기존 Inspector 설정에는 적용하지 않습니다.
        public void ApplyCreationDefaults()
        {
            skillID = "skill_projectile_gate";
            skillType = Nytherion.Core.Enums.SkillType.ProjectileGate;
            skillName = "분광 게이트";
            description = "투사체 분열 게이트 생성";
            skillLevel = 1;
            exp = 0;
            coolDown = 20f;
            manaCost = 0;
            damage = 0f;
            range = 3f;
            cooldownReductionPerLevel = 1f;
            minimumCooldown = 2f;
            forwardDistance = 1f;
            halfWidth = 0.75f;
            lifetime = 6f;
            detectionThickness = 0.12f;
            outputEndpointPadding = 0.25f;
            outputCount = 3;
            speedMultiplier = 1.8f;
            sizeMultiplier = 0.65f;
            outputLifetime = 5f;
            laserSpreadHalfAngle = 45f;
            endpointScale = 1f;
        }
    }
}
