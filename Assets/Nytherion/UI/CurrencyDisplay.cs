using TMPro;
using UnityEngine;
using Nytherion.Core;

public class CurrencyDisplay : MonoBehaviour
{
    [SerializeField] private CurrencyType type;
    [SerializeField] private TMP_Text amountText;

    private void Start()
    {
        UpdateUI(CurrencyManager.Instance.GetCurrency(type));
        CurrencyManager.Instance.onCurrencyChanged += OnCurrencyChanged;
    }

    private void OnDestroy()
    {
        CurrencyManager.Instance.onCurrencyChanged -= OnCurrencyChanged;
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
