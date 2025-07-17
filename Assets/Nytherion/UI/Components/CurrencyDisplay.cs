using TMPro;
using UnityEngine;
using Nytherion.Core.Managers;
using Zenject;

public class CurrencyDisplay : MonoBehaviour
{
    [SerializeField] private CurrencyType type;
    [SerializeField] private TMP_Text amountText;
    
    private CurrencyManager _currencyManager;
    
    [Inject]
    public void Construct(CurrencyManager currencyManager)
    {
        _currencyManager = currencyManager;
    }

    private void Start()
    {
        if (_currencyManager != null)
        {
            UpdateUI(_currencyManager.GetCurrency(type));
            _currencyManager.onCurrencyChanged += OnCurrencyChanged;
        }
    }

    private void OnDestroy()
    {
        if (_currencyManager != null)
        {
            _currencyManager.onCurrencyChanged -= OnCurrencyChanged;
        }
    }

    private void OnCurrencyChanged(CurrencyType changedType, int newAmount)
    {
        if (changedType == type)
        {
            UpdateUI(newAmount);
        }
    }

    private void UpdateUI(int amount)
    {
        amountText.text = amount.ToString();
    }
}
