namespace SmartHadithTree.Domain.Utilities;

public static class ArabicNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        return text
            // Strip Tashkeel
            .Replace("\u064E", "") // Fatha
            .Replace("\u064F", "") // Damma
            .Replace("\u0650", "") // Kasra
            .Replace("\u0652", "") // Sukun
            .Replace("\u0651", "") // Shadda
            .Replace("\u064B", "") // Tanwin Fatha
            .Replace("\u064C", "") // Tanwin Damma
            .Replace("\u064D", "") // Tanwin Kasra
            // Normalize Alefs
            .Replace("\u0623", "\u0627") // أ to ا
            .Replace("\u0625", "\u0627") // إ to ا
            .Replace("\u0622", "\u0627") // آ to ا
            // Normalize Ta Marbuta
            .Replace("\u0629", "\u0647"); // ة to ه
    }
}
