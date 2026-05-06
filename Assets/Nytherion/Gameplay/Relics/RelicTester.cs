using UnityEngine;
using System.Collections.Generic;
using Nytherion.GamePlay.Combat;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Synergy;
using Nytherion.Core.Managers;
using VContainer;

public class RelicTester : MonoBehaviour
{
    public WeaponData testWeapon;
    public List<RelicData> testRelics;
    public List<WeaponRelicSynergyData> synergyTable;

    private ISynergyEvaluator synergyEvaluator;
    private EventManager eventManager;
    [Inject]
    public void Construct(EventManager eventManager)
    {
        this.eventManager = eventManager;
    }
    void Start()
    {
        synergyEvaluator = new SynergyEvaluator(synergyTable,eventManager);
        WeaponRelicSynergyData synergy = synergyEvaluator.EvaluateSynergy(testWeapon, testRelics);

        if (synergy != null)
        {
            Debug.Log($"시너지 발동: {synergy.weaponName} + {synergy.relicName}");
        }
        else
        {
            Debug.Log("시너지 없음.");
        }
    }
}

