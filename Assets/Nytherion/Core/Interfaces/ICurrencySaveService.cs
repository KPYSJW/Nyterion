using System.Collections.Generic;
using Nytherion.Core.Managers;

namespace Nytherion.Core.Interfaces
{
    public interface ICurrencySaveService
    {
        void SaveCurrencies(Dictionary<CurrencyType, int> currencies);
        Dictionary<CurrencyType, int> LoadCurrencies();
    }
}
