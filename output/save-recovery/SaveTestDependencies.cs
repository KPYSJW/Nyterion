using System;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using UnityEngine;
using VContainer;

// 협력 객체만 대체합니다. 저장 서비스, 저장 매니저, 데이터 형식은 실제 소스입니다.
namespace Nytherion.Core.Managers
{
    public class CurrencyDataManager : BaseManager
    {
        public int Gold = 50;
        public bool ThrowOnLoad;
        public bool ThrowOnSave;
        public override void PopulateSaveData(SaveData data)
        {
            data.currencyTypes.Clear(); data.currencyAmounts.Clear();
            data.currencyTypes.Add(CurrencyType.Gold); data.currencyAmounts.Add(Gold);
            if (ThrowOnSave) throw new InvalidOperationException("의도한 저장 수집 실패");
        }
        public override void LoadFromSaveData(SaveData data)
        {
            if (ThrowOnLoad) throw new InvalidOperationException("의도한 로드 적용 실패");
            Gold = data.currencyAmounts.Count == 0 ? 50 : data.currencyAmounts[0];
        }
    }
    public class InventoryDataManager : BaseManager { }
    public class RelicManager : BaseManager { }
    public class EquipmentDataManager : BaseManager { }
    public class ShopManager : BaseManager { }
    public class SkillDataManager : BaseManager { }
    public interface IProgressionManager : ISaveable { }
    public static class GameManager { public static bool IsVerboseLogging() => false; }
}
namespace Nytherion.Core.Systems
{
    public class DataLifetimeScope : MonoBehaviour
    {
        public static DataLifetimeScope Instance;
        public IObjectResolver Container;
    }
}
public class GameSceneLifetimeScope : MonoBehaviour { public IObjectResolver Container; }
namespace Nytherion.UI.Inventory
{
    public class QuickSlotManager : MonoBehaviour, ISaveable
    {
        public void PopulateSaveData(SaveData data) { }
        public void LoadFromSaveData(SaveData data) { }
    }
    public class EquipmentSlotUI : MonoBehaviour { }
}
namespace Nytherion.UI.Skill
{
    public class SkillUIController : MonoBehaviour { public void SyncUIFromData() { } }
}

