namespace Nytherion.Core.Enums
{
    public enum StatType
    {
        MaxHealth,
        Defense,
        MoveSpeed,
        [UnityEngine.InspectorName("사용 중단: 근접 공격력 (호환용)")]
        MeleeDamage, // 기존 에셋의 직렬화 값 3 유지
        RangedDamage,
        [UnityEngine.InspectorName("사용 중단: 근접 공격 속도 (호환용)")]
        MeleeSpeed, // 기존 에셋의 직렬화 값 5 유지
        RangedSpeed,
        DashSpeed,
        DashDuration,
        DashCooldown,
        ExtraProjectiles,
        Lifesteal,
        ChargeTimeReduction,
        CritChance,
        CritDamage,
        All, // 모든 능력치 공통 적용용 (기존 직렬화 값 15 유지)
        [UnityEngine.InspectorName("효과 범위 증가")]
        ProjectileSize,
        [UnityEngine.InspectorName("최대 마나")]
        MaxMana,
        [UnityEngine.InspectorName("초당 마나 회복")]
        ManaRegenPerSecond,
        [UnityEngine.InspectorName("스킬 피해")]
        SkillDamage,
        [UnityEngine.InspectorName("스킬 쿨타임")]
        SkillCooldown
    }

    public static class StatTypeExtensions
    {
        public static bool IsRetired(this StatType stat)
        {
            return stat == StatType.MeleeDamage || stat == StatType.MeleeSpeed;
        }
    }
}
