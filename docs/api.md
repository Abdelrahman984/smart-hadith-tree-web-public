# REST API Reference

The backend API is hosted at `http://localhost:5147/api`.

## 1. Books Exploration

### `GET /api/books`
Returns a list of all distinct available Hadith books.

**Response (200 OK):**
```json
[
  "صحيح البخاري"
]
```

### `GET /api/books/{bookName}/chapters`
Returns a list of distinct chapters in the specified book, ordered sequentially.

**Response (200 OK):**
```json
[
  "المقدمة",
  "كتاب بدء الوحي",
  "كتاب الإيمان"
]
```

### `GET /api/books/{bookName}/chapters/{chapter}/hadiths`
Returns all hadiths in the specified book and chapter.

**Response (200 OK):**
```json
[
  {
    "id": "guid",
    "bookName": "صحيح البخاري",
    "hadithNumber": 1,
    "chapter": "كتاب بدء الوحي",
    "matnSnippet": "إنما الأعمال بالنيات..."
  }
]
```

## 2. Search

### `GET /api/hadith/search?q={query}`
Searches for hadiths by text, book name, or narrator name.

**Response (200 OK):**
```json
[
  {
    "id": "guid",
    "bookName": "صحيح البخاري",
    "hadithNumber": 1,
    "chapter": "بدء الوحي",
    "matnSnippet": "إنما الأعمال بالنيات..."
  }
]
```

### `GET /api/narrators/search?q={query}`
Searches for narrators by their full name or aliases.

**Response (200 OK):**
```json
[
  {
    "id": "guid",
    "fullName": "مالك بن أنس",
    "knownAs": "الإمام مالك",
    "generationTier": "كبار أتباع التابعين",
    "deathYearHijri": 179
  }
]
```

## 3. Isnad Tree (Graph Data)

### `GET /api/tree/{hadithId}`
Returns the full Isnad tree for a specific Hadith using a Recursive CTE query. 

**Response (200 OK):**
```json
{
  "hadithId": "guid",
  "bookName": "صحيح البخاري",
  "hadithNumber": 1,
  "matnArabic": "حدثنا الحميدي...",
  "nodes": [
    {
      "id": "guid (transmission_id)",
      "narratorId": "guid",
      "parentNodeId": "guid (parent_transmission_id)",
      "narratorName": "سفيان بن عيينة",
      "knownAs": "سفيان",
      "generationTier": "أتباع التابعين",
      "transmissionTerm": "حدثنا",
      "stepOrder": 2
    }
  ]
}
```

## 4. Narrator Details & AI

### `GET /api/narrators/{id}`
Returns detailed biographical info and all classical scholar evaluations.

### `GET /api/narrators/{id}/tooltip`
Returns a lightweight summary suitable for UI hover cards.

### `GET /api/narrators/{id}/ai-summary`
Invokes the Semantic Kernel to run a RAG prompt against the narrator's evaluations and returns a dynamic AI ruling.

**Response (200 OK):**
```json
{
  "summary": "بناءً على أقوال العلماء (ابن معين، أبو حاتم)، الراوي ثقة حافظ ومتقن لحديث الزهري."
}
```
