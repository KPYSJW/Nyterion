using Nytherion.Core.Data;

namespace Nytherion.Core.Interfaces
{
    public interface ISaveable
    {
        void PopulateSaveData(SaveData saveData);
        void LoadFromSaveData(SaveData saveData);
    }
}