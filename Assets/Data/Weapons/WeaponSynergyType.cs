using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data
{
    public enum WeaponSynergyType
    {
        None,               // 일반적인 경우, 시너지 없음
        Melee,              // 근거리 무기와 시너지
        Ranged,             // 원거리 무기와 시너지
        Universal,          // 어떤 무기든 관계없이 항상 시너지 발동
        CursedEngravingOnly, // 저주 각인 단독 보유 시 디버프
        CursedWeaponOnly,    // 저주 무기 단독 보유 시 디버프
        CursedSynergy        // 저주 무기 + 저주 각인 조합 시 강화 효과
    }
}
