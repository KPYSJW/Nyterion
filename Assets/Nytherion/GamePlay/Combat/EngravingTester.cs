using UnityEngine;
using System.Collections.Generic;
using Nytherion.Gameplay.Combat;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Synergy;

public class EngravingTester : MonoBehaviour
{
    public WeaponData weapon;
    public List<EngravingData> engravings;
    public List<WeaponEngravingSynergyData> synergyTable;

    void Start()
    {
        var synergy = SynergyChecker.CheckSynergy(weapon, engravings, synergyTable);

        if (synergy != null)
        {
            Debug.Log("✅ 시너지 발동!");
            Debug.Log("공격력 배수: " + synergy.bonusAttackMultiplier);
        }
        else
        {
            Debug.Log("❌ 시너지 없음.");
        }
    }
}

