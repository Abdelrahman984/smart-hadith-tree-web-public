namespace SmartHadithTree.Domain.Utilities;

/// <summary>
/// Compares the places where two narrators lived. Scholars travelled widely, so a difference in
/// cities alone says little: places are grouped into regions, and only narrators with no city
/// and no region in common are reported, as a note for the researcher (not as a break).
/// </summary>
public static class PlaceRegions
{
    private static readonly Dictionary<string, string[]> RegionCities = new()
    {
        ["الحجاز"] = ["الحجاز", "مكة", "المدينة", "الطائف", "جدة", "ينبع", "الجحفة"],
        ["العراق"] = ["العراق", "بغداد", "البصرة", "الكوفة", "واسط", "الموصل", "سامراء", "المدائن", "الانبار", "هيت", "عسكر مكرم"],
        ["الشام"] = ["الشام", "دمشق", "حمص", "حلب", "الرملة", "بيت المقدس", "القدس", "فلسطين", "عسقلان", "طرسوس", "انطاكية", "طبرية", "بعلبك", "صور", "صيدا", "حماة", "الاردن", "المصيصة"],
        ["مصر"] = ["مصر", "الفسطاط", "الاسكندرية", "القاهرة", "دمياط", "زقاق القناديل"],
        ["خراسان"] = ["خراسان", "مرو", "نيسابور", "بلخ", "هراة", "بخارى", "سمرقند", "طوس", "نسا", "سرخس", "ابيورد", "ترمذ", "زرزم", "فربر"],
        ["اليمن"] = ["اليمن", "صنعاء", "عدن", "الجند", "زبيد"],
        ["المغرب"] = ["المغرب", "القيروان", "تونس", "الاندلس", "قرطبة", "فاس"],
        ["الجبال"] = ["الجبال", "الري", "همذان", "همدان", "قزوين", "اصبهان", "اصفهان", "قم", "زنجان"],
        ["الجزيرة"] = ["الجزيرة", "الرقة", "حران", "الرها", "رأس عين", "ديار بكر"],
        ["فارس"] = ["فارس", "شيراز", "الاهواز", "كرمان"],
    };

    // Words that describe the stay, not the place: «سكن المدينة»، «ونزل البصرة»، «قرية زرزم».
    private static readonly HashSet<string> NoiseWords = new[]
    {
        "سكن", "وسكن", "نزل", "ونزل", "ثم", "انتقل", "وانتقل", "رحل", "ورحل", "قرية", "بلدة", "اقام", "واقام", "ب", "في"
    }.Select(Normalize).ToHashSet(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> CityToRegion = BuildCityIndex();

    private static Dictionary<string, string> BuildCityIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (region, cities) in RegionCities)
            foreach (var city in cities)
                index[Normalize(city)] = Normalize(region);
        return index;
    }

    private static string Normalize(string text) =>
        ArabicNormalizer.Normalize(text).Replace('ى', 'ي').Trim();

    /// <summary>Splits «سكن المدينة، ونزل البصرة» into the places it names: «المدينة», «البصرة» (normalized).</summary>
    public static HashSet<string> ExtractPlaces(string? residence, string? death) =>
        ExtractWithDisplay(residence, death).Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>Normalized place → the place as written in the source, for showing to the user.</summary>
    private static Dictionary<string, string> ExtractWithDisplay(string? residence, string? death)
    {
        var places = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var part in new[] { residence, death })
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            foreach (var piece in part.Split(['،', ',', '-', '؛', ';', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                var original = piece.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var skip = 0;
                while (skip < original.Length && NoiseWords.Contains(Normalize(original[skip]))) skip++;
                if (skip == original.Length) continue;

                var display = string.Join(' ', original.Skip(skip));
                var place = Normalize(display);
                // «ونزل البصرة» may arrive with a leading و on the place itself.
                if (!CityToRegion.ContainsKey(place) && place.StartsWith('و') && CityToRegion.ContainsKey(place[1..]))
                {
                    place = place[1..];
                    display = display[1..];
                }
                places.TryAdd(place, display);
            }
        }
        return places;
    }

    private static HashSet<string> RegionsOf(IEnumerable<string> places) =>
        places.Select(p => CityToRegion.GetValueOrDefault(p, p)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// True when both narrators have known places and share neither a city nor a region.
    /// <paramref name="note"/> then names the two sets for the researcher to review.
    /// </summary>
    public static bool HaveNothingInCommon(
        string? sheikhResidence, string? sheikhDeath,
        string? studentResidence, string? studentDeath,
        out string? note)
    {
        note = null;
        var sheikhPlaces = ExtractWithDisplay(sheikhResidence, sheikhDeath);
        var studentPlaces = ExtractWithDisplay(studentResidence, studentDeath);
        if (sheikhPlaces.Count == 0 || studentPlaces.Count == 0) return false;

        if (sheikhPlaces.Keys.Intersect(studentPlaces.Keys).Any()) return false;
        if (RegionsOf(sheikhPlaces.Keys).Overlaps(RegionsOf(studentPlaces.Keys))) return false;

        note = $"بلدان الشيخ ({string.Join("، ", sheikhPlaces.Values)}) لا تلتقي مع بلدان التلميذ ({string.Join("، ", studentPlaces.Values)})، فيُراجع إمكان اللقاء في رحلة.";
        return true;
    }
}
