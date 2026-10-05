# Project Specification: Smart Hadith Tree (شجرة الأسانيد الذكية)

## 1. Project Overview
The "Smart Hadith Tree" is a MENA-targeted SaaS platform and research tool designed to digitize and visualize Hadith narrator chains (Isnad) dynamically. It combines robust relational databases for classical narrator data with AI-powered Retrieval-Augmented Generation (RAG) to summarize scholar evaluations (Jarh and Ta'deel). The primary language is Arabic, requiring native RTL support.

## 2. Technology Stack
*   **Backend:** ASP.NET Core Web API (C#).
*   **Database:** SQL Server managed via Entity Framework Core (EF Core).
*   **Frontend:** React.js / Next.js.
*   **Graph/Tree Visualization:** React Flow (or Cytoscape.js).
*   **AI Integration:** Microsoft Semantic Kernel (for RAG architecture and LLM orchestration).

## 3. Database & Architecture Guidelines
**AI Agent Instruction:** When generating backend code, strictly adhere to the following architectural rules:
*   **Primary Keys:** Use `UNIQUEIDENTIFIER` (GUID) for all primary keys to prevent conflicts during external data ingestion from various sources.
*   **Arabic Collation:** Configure SQL Server text columns using `Arabic_100_CI_AI` to ensure searches are accent-insensitive (ignoring Arabic diacritics/Tashkeel).
*   **Core Entities:** 
    1.  `Narrator` (الراوي): Stores biographical data.
    2.  `HadithText` (المتن): Stores the core text.
    3.  `Transmission` (السند): The many-to-many relationship linking a Sheikh to a Student for a specific Hadith.
    4.  `ScholarEvaluation` (أقوال الجرح والتعديل): The raw texts from scholars evaluating the narrator.
*   **Data Retrieval:** Use Recursive CTEs (Common Table Expressions) for querying the tree structure efficiently.

## 4. UI/UX & Design System (Arabic First)
*   **Typography:** Use `Noto Sans Arabic` for all Arabic text to ensure excellent readability of diacritics. Use `Inter` for numbers and English UI elements.
*   **Color Palette:**
    *   **Primary:** Deep Blue (`#1A3A5C`) for general UI, trusted narrators, and main structural elements.
    *   **Secondary/Highlight:** Teal (`#00C2CB`) for active selections or acceptable (صدوق) narrators.
    *   **Warning/Alert:** Appropriate shades of red/orange for weak/rejected narrators.
*   **Graph Layout:** The tree must be rendered Vertically (Top-to-Bottom) to accommodate long Arabic names and RTL text direction without horizontal overlapping.

## 5. Core Features & Workflows
1.  **Smart Search:** Users can search by Hadith text, narrator name, or book index.
2.  **Disambiguation:** If multiple results exist, present a list for the user to select the exact Hadith.
3.  **Dynamic Tree Generation:** Render the Isnad chain from the compiler (bottom) to the Prophet ﷺ (top).
4.  **Progressive Disclosure:**
    *   *Hover:* Show a fast, lightweight tooltip with the narrator's name, tier (طبقة), and a 2-word status.
    *   *Click:* Open a detailed Sidebar.
5.  **AI RAG Evaluation:** Inside the sidebar, use the LLM to read the `ScholarEvaluation` records associated with the selected narrator, summarize them, and provide a definitive ruling (e.g., "Thiqah" - Trustworthy) with citations. *The AI must not hallucinate; it must only use the retrieved DB records.*

## 6. Data Ingestion & ETL Pipeline
**AI Agent Instruction:** The data will not be manually entered. We will rely on external open-source datasets (e.g., Sunnah.com GitHub repos, Shamela DB dumps).
*   **Tooling:** We will build a separate C# Console Application or Background Worker Service dedicated to data ingestion.
*   **Process:** 
    1. Read and parse raw JSON, XML, or SQL dump files.
    2. Map the external data structures to our EF Core Entities.
    3. Use Bulk Insert extensions (e.g., `EFCore.BulkExtensions`) for performance, as we will be processing millions of records.
    4. Handle duplicates gracefully using the configured GUIDs.

## 7. Implementation Phases
**AI Agent Instruction:** We will build this iteratively. Do not generate the entire project at once. Await my command to start a specific phase.
*   **Phase 1:** Define EF Core Entities, DbContext, and Configurations (Fluent API).
*   **Phase 2:** Develop the Data Ingestion (ETL) C# script to parse external JSON/XML data and populate the database.
*   **Phase 3:** Develop the Search and Recursive Tree Data API endpoints.
*   **Phase 4:** Setup the React application, routing, and basic React Flow RTL implementation.
*   **Phase 5:** Integrate Microsoft Semantic Kernel for the RAG-based Sidebar summaries.

## 8. Coding Standards
*   Apply SOLID principles and Clean Architecture concepts.
*   Use standard C# naming conventions (PascalCase for classes/methods, camelCase for variables).
*   Provide robust error handling and logging.
*   Ensure all JSON serialization handles Arabic characters natively without Unicode escaping issues.

---
**Agent:** Acknowledge this prompt by summarizing the core objective in one sentence, then ask me if we should begin Phase 1 (EF Core Entities).