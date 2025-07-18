using Nytherion.Core.Data;

namespace Nytherion.Core.Interfaces
{
    public interface IEngravingSaveService
    {
        void SaveEngravings(EngravingGridState state);
        EngravingGridState LoadEngravings();
    }
}