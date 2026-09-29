namespace Nytherion.Core.Enums
{
    public enum StatType
    {
        MaxHealth,
        Defense,
        MoveSpeed,
        MeleeDamage,
        RangedDamage,
        MeleeSpeed,
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
        ProjectileSize,
        AttackRange
    }
}
