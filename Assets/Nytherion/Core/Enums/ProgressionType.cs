namespace Nytherion.Core.Enums
{
    public enum ProgressionType
    {
        None,
        KillEnemy,          // 적 처치
        CollectGold,        // 골드 획득
        UseSkill,           // 스킬 사용
        ClearFloor,         // 층 클리어
        EarnToken,          // 토큰 획득
        TotalPlayTime,      // 누적 플레이 시간 (초)
        DealDamage,         // 가한 데미지 누적
        TakeDamage          // 받은 데미지 누적
    }
}
