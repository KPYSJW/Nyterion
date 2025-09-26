using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Interfaces
{
    public interface ICurrencySaveService
    {
        void SaveCurrencies(Dictionary<CurrencyType, int> currencies);
        Dictionary<CurrencyType, int> LoadCurrencies();
    }
}
