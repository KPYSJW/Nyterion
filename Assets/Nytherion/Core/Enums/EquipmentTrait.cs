namespace Nytherion.Core.Enums
{
    /// <summary>
    /// 장비가 가질 수 있는 고유한 특성(태그)들을 정의합니다.
    /// 각인 시스템과의 시너지 연동(예: 저주 해방 등)에 사용됩니다.
    /// </summary>
    public enum EquipmentTrait
    {
        None = 0,
        Sealed,         // 봉인 상태 (기존 Cursed)
        Curse,          // 저주 속성 (기존 HighCurse, 받는 피해 증가)
        Fire,           // 화염 속성
        Ice,            // 빙결 속성
        Lightning,      // 전격 속성
        Holy,           // 신성 속성
        Demonic,         // 마성 속성
        Poison           // 독 속성
    }
}