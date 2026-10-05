using System.Text.Json.Serialization;

namespace SmartHadithTree.Etl.Parsers.Shamela;

/// <summary>A critic's quote about a narrator, as <c>registry.json</c> stores it (Tahdhib al-Kamal).</summary>
public sealed record RegistryQuote(
    [property: JsonPropertyName("critic")] string? Critic,
    [property: JsonPropertyName("via")] string? Via,
    [property: JsonPropertyName("text")] string? Text);

/// <summary>
/// One narrator of the Shamela-built registry (<c>data/shamela_rijal/registry.json</c>, written by
/// <c>rijal_pilot/pipeline.py</c>). Ids are stable: <c>tk&lt;num&gt;</c> for Tahdhib al-Kamal, <c>&lt;source&gt;:&lt;num&gt;</c> for the rest.
/// </summary>
public sealed record RegistryEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("header")] string Header,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("verdict")] string? Verdict,
    [property: JsonPropertyName("rank")] int? Rank,
    [property: JsonPropertyName("tabaqa")] string? Tabaqa,
    [property: JsonPropertyName("shuyukh")] List<string>? Shuyukh,
    [property: JsonPropertyName("talamidh")] List<string>? Talamidh,
    [property: JsonPropertyName("quotes")] List<RegistryQuote>? Quotes);

/// <summary>A narrator of Shamela's own encyclopedia (S1.db, decoded to <c>data/shamela/narrators.json</c>).</summary>
public sealed record S1Quote(
    [property: JsonPropertyName("section")] string? Section,
    [property: JsonPropertyName("critic")] string? Critic,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("book")] string? Book,
    [property: JsonPropertyName("vol")] int? Vol,
    [property: JsonPropertyName("page")] int? Page);

public sealed record S1Narrator(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("death")] int? Death,
    [property: JsonPropertyName("fields")] Dictionary<string, string>? Fields,
    [property: JsonPropertyName("quotes")] List<S1Quote>? Quotes);

/// <summary>Result of <c>map_s1.py</c> for one S1 id: the registry narrator it is, and how sure the script is.</summary>
public sealed record S1Mapping(
    [property: JsonPropertyName("registry")] string? Registry,
    [property: JsonPropertyName("confidence")] string? Confidence);

/// <summary>One name of a resolved isnad (<c>export_chains_shamela.py</c>).</summary>
public sealed record ChainName(
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("how")] string? How,
    [property: JsonPropertyName("verbs")] List<string>? Verbs);

/// <summary>
/// One record's resolved isnad. <c>Names</c> is its first chain; <c>Branches</c> is present when the isnad has
/// several chains (tahwil: «ح», «قالا», <c>tahwil.py</c>) and holds every chain, the first one included, each from
/// the compiler upward.
/// </summary>
public sealed record RecordChain(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("names")] List<ChainName> Names,
    [property: JsonPropertyName("branches")] List<List<ChainName>>? Branches = null);

public sealed record BookChains(
    [property: JsonPropertyName("book")] string Book,
    [property: JsonPropertyName("compiler")] string? Compiler,
    [property: JsonPropertyName("chains")] List<RecordChain> Chains);

/// <summary>A span of a record's <c>arabic</c> text (isnad, matn, remark, note, text).</summary>
public sealed record RecordPart(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("end")] int End);

/// <summary>One record of <c>data/shamela/&lt;slug&gt;/&lt;n&gt;.json</c> (Phase 4 format).</summary>
public sealed record ShamelaRecord(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("bab")] string? Bab,
    [property: JsonPropertyName("vol")] string? Vol,
    [property: JsonPropertyName("arabic")] string? Arabic,
    [property: JsonPropertyName("parts")] List<RecordPart>? Parts);

public sealed record ShamelaBookInfo(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("title")] string Title);

public sealed record ShamelaIndexEntry(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("name_ar")] string? NameAr);
