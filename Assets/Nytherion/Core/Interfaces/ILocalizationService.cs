using System;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Interfaces
{
    public interface ILocalizationService
    {
        event Action<SupportedLanguage> LanguageChanged;

        SupportedLanguage CurrentLanguage { get; }
        bool IsReady { get; }

        void SetLanguage(SupportedLanguage language);
        string GetString(
            string tableName,
            string entryKey,
            string koreanFallback = "",
            string englishFallback = "",
            params object[] arguments);
    }
}
