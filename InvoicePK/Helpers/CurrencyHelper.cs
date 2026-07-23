namespace InvoicePK.Helpers;

// Central place for supported currencies — used by PDF generation and
// can be exposed via an endpoint if the frontend ever wants to fetch it
// dynamically instead of hardcoding the list.
public static class CurrencyHelper
{
    public static readonly Dictionary<string, string> Symbols = new()
    {
        { "PKR", "Rs." },
        { "USD", "$"   },
        { "SAR", "SR"  },   // Saudi Riyal
        { "AED", "AED" },   // UAE Dirham
        { "GBP", "£"   },
        { "EUR", "€"   },
    };

    public static string GetSymbol(string currencyCode) =>
        Symbols.TryGetValue(currencyCode, out var symbol) ? symbol : currencyCode;

    // Export-of-service invoices (foreign currency) conventionally carry no
    // domestic GST/sales tax — this is used as a sensible default, not a
    // hard rule, since the user can always override it.
    public static bool IsDomestic(string currencyCode) => currencyCode == "PKR";
}
