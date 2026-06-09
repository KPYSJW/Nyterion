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
        TakeDamage,         // 받은 데미지 누적
        TornPouchTrigger,   // 구멍 난 주머니 발동 횟수
        BuyShopItem,        // 상점 아이템 구매 횟수
        MaxGoldSnoutBuff,   // 황금 돼지코 버프 최대치 도달 횟수
        ComfyCornerClear,   // 구석 조약돌 가장자리 배치 상태로 스테이지 클리어
        CenterPebblePlace,  // 중앙의 자갈 정중앙 배치 횟수
        SocialDistancingTrigger, // 길쭉한 가지 주변 4슬롯 비우고 효과 발동
        TangledYarnRoomClear, // 꼬인 실타래 활성 링크 3개 이상 상태로 방 클리어
        SqueakyGearTrigger, // 삐걱이는 톱니 효과 발동
        GlassCapBossClear,  // 유리 병뚜껑 장착 상태로 보스 처치
        LuckyCloverResetInOneBattle // 네잎클로버 대쉬 초기화 3회 이상 획득 (한 전투 내)
    }
}
