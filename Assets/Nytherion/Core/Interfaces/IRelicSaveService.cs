using Nytherion.Core.Data;

namespace Nytherion.Core.Interfaces
{
    public interface IRelicSaveService
    {
        void SaveRelics(RelicGridState state);
        RelicGridState LoadRelics();
    }
}