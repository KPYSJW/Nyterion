using Nytherion.Data.ScriptableObjects.Engravings;

namespace Nytherion.Core.Interfaces
{
    public interface IEngravingSaveService
    {
        void SaveEngravings(EngravingGridState state);
        EngravingGridState LoadEngravings();
    }
}