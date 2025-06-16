using System.Collections.Generic;

namespace Nytherion.Core.Interfaces
{
    public interface ICurrencySaveService
    {
        void SaveCurrencies(Dictionary<CurrencyType, int> currencies);
        Dictionary<CurrencyType, int> LoadCurrencies();
    }
}
