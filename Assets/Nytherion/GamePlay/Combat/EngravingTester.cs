using UnityEngine;
using System.Collections.Generic;
using Nytherion.Gameplay.Combat;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Data.ScriptableObjects.Synergy;

public class EngravingTester : MonoBehaviour
{
    public WeaponData testWeapon;
    public List<EngravingData> testEngravings;
    public List<WeaponEngravingSynergyData> synergyTable;

    private ISynergyEvaluator synergyEvaluator;
    void Start()
    {
        synergyEvaluator = new SynergyEvaluator(synergyTable);
        WeaponEngravingSynergyData synergy = synergyEvaluator.EvaluateSynergy(testWeapon, testEngravings);

        if (synergy != null)
        {
            Debug.Log($"✅ 시너지 발동: {synergy.weaponName} + {synergy.engravingName}");
        }
        else
        {
            Debug.Log("❌ 시너지 없음.");
        }
    }
}

