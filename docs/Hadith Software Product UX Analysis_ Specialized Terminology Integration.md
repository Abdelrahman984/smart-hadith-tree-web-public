# **Architectural Validation and UX Reconceptualization of Hadith Research Software: Aligning Digital Workflows with Classical Scholarship**

The intersection of computational linguistics, graph database architecture, and classical Islamic epistemology presents a profound opportunity to revolutionize digital humanities. However, developing academic research software for advanced Hadith studies requires transcending superficial text retrieval and basic data visualization. To serve the rigorous demands of premier academic institutions, such as Al-Azhar University, and to meet the meticulous standards of classical *Mustalah al-Hadith* (Hadith terminology and sciences), digital tools must accurately map to the intensive cognitive and practical workflow of the *Muhaqqiq* (critical researcher).

This report provides an exhaustive architectural validation and product audit of Hadith research software, utilizing the 'Smart Hadith Tree' as a primary case study. By dissecting the real-world workflow of Hadith authentication, conducting a rigorous gap analysis, and auditing current software features through the dual lenses of product design and classical scholarship, this analysis establishes a comprehensive blueprint for developing truly indispensable tools for modern academic researchers.

## **The Epistemology of Authentication: The Real-World Researcher Workflow**

The authentication of a Prophetic tradition is not a linear lookup process; it is a highly complex, multidimensional investigation. It synthesizes textual criticism (*Matn* analysis), topological network analysis (*Isnad* mapping), spatial-temporal prosopography (*'Ilm al-Rijal*), and the detection of hidden structural defects (*'Ilm al-'Ilal*). To design effective software, the product architecture must mirror this exact cognitive and practical journey, systematically reducing the researcher's cognitive load without compromising academic rigor.

### **Initial Selection and Comprehensive Extraction (*Takhrij*)**

The foundational step in any Hadith research is *Takhrij*—the exhaustive process of locating a specific Hadith across the vast corpus of classical literature, extracting all its transmission routes (*Turuq*), and tracing it back to its primary sources1. A *Muhaqqiq* does not search for a static text string; they search for a conceptual transmission unit that may have morphed across different regions and centuries.

Classical authors structured their compilations using highly varied architectural frameworks. Some scholars arranged their works by the narrating Companion (*Masanid*, such as the *Musnad* of Imam Ahmad, which catalogs over a thousand Companions), others by jurisprudential topics (*Sunan* and *Jawami'*), and others by geography or specific teachers (*Ma'ajim*)1. Because of this structural diversity, the *Muhaqqiq* must cast a remarkably wide net, identifying every instance where the text, or a semantically similar variant, was narrated1.

This exhaustive collection is an absolute methodological requirement. A Hadith cannot be accurately judged in isolation; a transmission chain that appears structurally weak in one source may be elevated to an acceptable grade by a corroborating chain found in an obscure, geographically distant compilation3. The researcher must gather all these disparate threads before any analysis can begin, transitioning raw, scattered text into a unified analytical dataset.

### **Schematic Mapping and Network Organization (*I'tibar* and *Rasm al-Shajarah*)**

Once the raw data is collected, the *Muhaqqiq* engages in *I'tibar* (comprehensive consideration and mapping). Historically, this was a manual, highly taxing cognitive process of drawing the transmission tree (*Rasm al-Shajarah*) to visualize the flow of the narration from the Prophet Muhammad down to the various compilers5.

During *I'tibar*, the researcher actively hunts for the *Madar*, which is the common link or bottleneck narrator upon whom the various chains converge. Establishing the *Madar* is critical for determining the exact historical and geographical moment where variations in the text or chain originated. The researcher organizes the network into two distinct supportive structures to assess the density and reliability of the transmission.

The first structure involves *Mutaba'at* (parallel corroborations). These are instances where a narrator is supported by another narrator who transmits the exact same Hadith from the same teacher. This is known as a perfect or full corroboration (*Mutaba'ah Tammah*). If the corroboration occurs higher up in the chain—for example, if two different students narrate from two different teachers, but those teachers share the same master—it is termed a partial corroboration (*Mutaba'ah Qasirah*)5.

The second structure involves *Shawahid* (witnesses). These are corroborating narrations that convey the exact same semantic meaning or legal ruling but originate from a completely different Companion (*Sahabi*)5. By physically or mentally mapping these complex relationships, the scholar assesses the structural density of the transmission network. A single thread represents an isolated report (*Gharib* or *Fard*), which is highly susceptible to human error, whereas a dense, overlapping network suggests precision, collective memory retention, and historical authenticity3.

### **Narrator Evaluation and Prosopography (*'Ilm al-Rijal* and *Jarh wa Ta'dil*)**

With the topological network mapped, the researcher must evaluate the individual nodes within the graph: the narrators. This phase is governed by *'Ilm al-Rijal* (the science of narrators) and specifically *Jarh wa Ta'dil* (the systematic disparagement and accreditation of individuals)9. The researcher assesses two absolute prerequisites for authenticity for every single node in the network: *'Adalah* (moral rectitude and religious integrity) and *Dabt* (precision, memory retention, and accuracy in oral or written transmission)4.

This evaluation requires consulting highly specialized biographical dictionaries. A strict hierarchy of sources is observed, particularly adhering to the methodologies institutionalized by later critical synthesizers who possessed access to the entire preceding tradition.

&nbsp;

| Source Tier | Primary Text | Author | Function in the Researcher Workflow |
| :---- | :---- | :---- | :---- |
| **Primary Anchor** | *Taqrib al-Tahdhib* | Ibn Hajar al-'Asqalani (d. 852 AH) | Provides final, concise, and academically binding single-line verdicts on narrator reliability4. |
| **Detailed Assessment** | *Tahdhib al-Tahdhib* | Ibn Hajar al-'Asqalani (d. 852 AH) | Consulted for detailed debate and historical context when the primary anchor is insufficient or disputed4. |
| **Foundational Context** | *Tahdhib al-Kamal* | Al-Mizzi (d. 742 AH) | Used to trace the origins of the narrator, full lists of teachers/students, and geographical movements11. |
| **Defect Verification** | *Mizan al-I'tidal* | Al-Dhahabi (d. 748 AH) | Specifically targeted to investigate narrators explicitly accused of weakness, forgery, or hidden defects4. |

The researcher maps the narrator to a specific grading tier that directly dictates their impact on the chain's overall authenticity. For example, a narrator graded as *Thiqah* (reliable) supports a *Sahih* (authentic) chain, whereas a narrator graded as *Saduq* (truthful but not perfectly precise) yields a *Hasan* (good) chain4.

However, identifying a narrator's grade is wholly insufficient on its own. The researcher must prove that transmission between a teacher and a student was physically, geographically, and historically possible. This involves verifying *Tabaqat* (generational timelines) to ensure the student's birth and teacher's death dates overlap sufficiently to allow for the age of hearing (*Sinn al-Tahammul*). Furthermore, the scholar looks for proof of *Sama'* (direct hearing), verifying explicit statements from classical scholars confirming that the student actively heard the Hadith from the teacher, rather than just living in the same era3. Finally, the researcher maps *Rihlah* (geographical overlap), tracing the travel routes of narrators to ensure geographical proximity during the alleged time of transmission15.

### **Textual Cross-Referencing and Variation Analysis (*Ikhtilaf al-Alfaz*)**

An *Isnad* cannot be authenticated in a vacuum; its structural strength is inextricably tied to the *Matn* (text) it carries. Once the network is mapped and the nodes are vetted, the *Muhaqqiq* performs rigorous textual criticism, mapping *Ikhtilaf al-Alfaz* (variations in wording) across the divergent chains1.

If a narrator adds a word, a contextual phrase, or a jurisprudential ruling not found in the narrations of their peers who share the same teacher, the researcher must evaluate this addition. If the narrator is exceptionally reliable and their addition does not contradict the consensus, it may be accepted as *Ziyadat al-Thiqah* (the acceptable addition of a reliable narrator). However, if the narrator is of lesser standing than the group they contradict, or if the addition radically alters the theological or legal meaning of the text, the narration is flagged as *Shadh* (anomalous) or *Munkar* (denounced)4.

Furthermore, if multiple highly reliable narrators report severely conflicting texts from the same teacher, and reconciliation (*Jam'*) or determining chronological abrogation (*Naskh*) proves impossible, the Hadith is declared *Mudtarib* (confused or unstable). This leads to its rejection despite the individual reliability of the narrators, as the conflicting data indicates a catastrophic failure in collective memory at a specific node in the network6. This phase demands intense comparative analysis, matching specific textual fragments to specific branches of the transmission tree.

### **Identifying Hidden Defects (*'Ilm al-'Ilal*)**

The most elite, cognitively demanding tier of Hadith scholarship is the identification of *'Ilal* (hidden, subtle defects that undermine the authenticity of an outwardly flawless chain)4. While a superficial review might flag explicit breaks (*Inqita'*), the human *Muhaqqiq* looks for what is intentionally or accidentally concealed within the network topology.

Hidden defects often manifest as *Tadlis* (concealment). This occurs when a narrator, who is historically known to have met a specific teacher, drops the name of a weak intermediary student and uses ambiguous transmission terminology—such as saying "from" (*'an*) instead of the explicit "he narrated to me" (*haddathani*). This linguistic sleight of hand creates the illusion of a continuous, elevated chain3. Another common *'Illah* is an erroneous elevation (*Raf'*). For instance, ten students of a prominent successor might correctly narrate a statement as the personal opinion of a Companion (*Mawquf*), but one student erroneously attributes the statement directly to the Prophet Muhammad (*Marfu'*), fundamentally changing the legal authority of the text.

Detecting an *'Illah* requires the researcher to overlay all collected *Turuq*, align their respective texts, and spot the exact point in the geographical and chronological matrix where the anomaly was introduced6. It requires advanced pattern recognition across massive, unstructured datasets, relying historically on the encyclopedic memory of classical scholars to spot inconsistencies that a localized reading would miss.

### **Final Synthesis and Verdict (*Hukm*)**

The final stage of the workflow is algorithmic in its underlying logic but requires immense hermeneutic nuance in execution. The *Muhaqqiq* calculates the base grade of the Hadith from its weakest link and then applies the highly specific rules of *Taqwiyah* (mutual strengthening)4.

If a chain is determined to be *Da'if* (weak) due to a narrator's minor defect in memory (*Dabt*), but the researcher has found two other independent chains with similar minor weaknesses, the collective structural strength of these diverse routes elevates the Hadith to *Hasan li-ghayrihi* (good due to corroboration)3. Conversely, if the weakness stems from a narrator explicitly accused of lying (*Muttaham bi-l-kadhib*), the chain is categorized as structurally compromised and abandoned (*Matruk* or *Mawdu'*). A foundational epistemological principle of *Taqwiyah* dictates that a severe deficiency in moral integrity (*'Adalah*) cannot be repaired by any amount of corroboration; a liar corroborated by other liars does not generate truth4.

The researcher synthesizes the continuum of the chain (*Ittisal*), the integrity and precision of the narrators (*'Adalah* and *Dabt*), the absence of anomaly (*'Adam al-Shudhudh*), and the absence of hidden defects (*'Adam al-'Illah*) to issue a final, academically binding *Hukm* (verdict) that dictates Islamic jurisprudence and theology4.

## **Gap Analysis: Misalignments and Missing Capabilities**

When mapping this robust, highly rigorous classical workflow against the current 'Smart Hadith Tree' feature set—which presently consists of basic full-text search, automated topological visualization, and AI-generated narrative summaries—critical architectural deficiencies emerge. The software currently facilitates the initial data retrieval but entirely abandons the researcher during the high-cognitive-load phases of textual criticism, spatio-temporal validation, and defect detection.

### **The Absence of Contextual Text Alignment (Multiple Sequence Alignment)**

The most glaring gap in the current product is the architectural segregation of the *Isnad* and the *Matn*. While the software maps the chain of narrators, it fails to visualize how the text itself transforms as it moves through that specific chain6.

The most time-consuming pain point for a *Muhaqqiq* is manually tracking *Ikhtilaf al-Alfaz* (textual variations). If ten branches diverge from a common link (*Madar*), the researcher must currently read all ten texts side-by-side to find exactly where a specific word was added, altered, or omitted6. The software lacks the string-matching and text-alignment algorithms necessary to automate this. In the domain of classical Arabic natural language processing (NLP), solving this requires sequence alignment algorithms akin to the Smith-Waterman local sequence alignment—traditionally used in bioinformatics—or dynamic programming techniques designed for semantic textual similarity16. Without this computational capability, the software is entirely blind to *Shudhudh* (anomalies) and cannot assist the researcher in identifying the precise node where a textual corruption was introduced6.

### **Inadequate Spatio-Temporal Validation (The *Sama'* and *Rihlah* Blindspot)**

The software currently indicates explicit anomalies like broken chains (*Inqita'*), presumably by checking if a direct edge exists between two nodes in a database. However, it ignores the nuanced dimensions of time (*Tabaqat*) and geographic space (*Rihlah*) which are paramount in classical scholarship15.

Classical scholars did not merely ask if a database recorded a link between two individuals; they actively investigated if those individuals could have physically occupied the same geographic space at the same time. A researcher must manually cross-reference birth and death dates against geographical travel records to detect *Inqita' Khafi* (hidden disconnection). The current graph visualization is purely topological, lacking the spatio-temporal properties required by academic centers like Al-Azhar15. If a narrator residing in Basra claims to have heard a Hadith from a teacher in Kufa in the year 120 AH, the software should instantly flag the relationship with a high-severity warning if historical temporal records indicate the teacher died in 118 AH, or if spatial records indicate the student never traveled to Kufa. The absence of spatial and temporal graph properties forces the researcher back to manual encyclopedia lookups.

### **Epistemological Flattening via Abstractive AI**

Using an AI-powered Retrieval-Augmented Generation (RAG) system to provide natural language "summaries" of narrator profiles fundamentally violates the principles of *Tahqiq* (academic verification)11.

In *'Ilm al-Rijal*, precision is the absolute currency of the discipline. The exact phrasing used by authorities like Ibn Hajar or Al-Dhahabi carries specific, binding legal and methodological weight4. A professional researcher cannot cite an "AI-generated summary" in a doctoral thesis, a peer-reviewed journal, or a critical edition of a classical text11. They require the exact, verifiable quote, properly attributed to its source, complete with volume and page numbers21.

Large Language Models (LLMs) are inherently prone to hallucination and often flatten highly nuanced, technical terminology into generic, conversational prose18. If an LLM summarizes a narrator's profile by stating, "He was generally reliable but had some memory issues," it destroys the technical utility of classical terms. A researcher needs to know if the scholar explicitly labeled the narrator as *Saduq yahim* (truthful but errs) or *Maqbul* (acceptable only when corroborated)4. By summarizing these texts, the software replaces a highly engineered, millennium-old classification system with a black-box approximation, rendering the feature academically invalid.

### **Lack of Algorithmic *Taqwiyah* (Mutual Strengthening) Modeling**

The software visualizes chains but completely fails to assist the researcher in calculating the cumulative structural strength of the network. The researcher must manually execute the complex conditional logic of *Taqwiyah*—verifying that supporting chains are genuinely independent, identifying the specific tier of weakness for each chain, and applying the correct mathematical upgrade while avoiding the fatal pitfall of corroborating forged (*Mawdu'*) chains3. The current system lacks a rules-engine capable of executing this logic. It does not utilize the graph network's topology—such as degree centrality or path independence—to automate preliminary grading suggestions, leaving a massive analytical burden entirely on the human user8.

### **Deficient Preprocessing for Classical Arabic**

Modern NLP tools are primarily trained on Modern Standard Arabic (MSA) or dialectal data, which drastically underperforms when applied to Classical Arabic texts23. To perform accurate full-text search, topic modeling, or textual variation detection, the raw text must be accurately tokenized and morphologically analyzed.

&nbsp;

| NLP Tool | Specialization | Utility for Hadith Software |
| :---- | :---- | :---- |
| **CAMeL Tools** | Comprehensive Arabic NLP package. | Highly effective for text preprocessing, significantly improving evaluation metrics (e.g., BLEU scores) when processing complex Arabic morphologies23. |
| **Farasa** | Fast and accurate Arabic segmenter. | Utilizes SVM-rank with linear kernels, outperforming state-of-the-art tools in speed while maintaining high accuracy for morphological analysis23. |
| **Stanza / MADAMIRA** | General preprocessing and disambiguation. | Useful baselines, but often require fine-tuning to handle the specific syntactical structures of Classical Arabic (*Matn*) and the repetitive naming conventions in chains (*Isnad*)24. |

Without integrating specialized tools like CAMeL Tools or Farasa to properly segment the *Matn* and the *Isnad*, the software's underlying data remains noisy, leading to failed searches, missed corroborations, and inaccurate sequence alignments23.

## **Feature Audit: The "Useless" Test and UX Pivoting Strategies**

To ensure the 'Smart Hadith Tree' transitions from a superficial visualization novelty to a mission-critical platform for the academic *Muhaqqiq*, the current features must be brutally audited against real-world utility and aggressively pivoted.

### **Audit 1: Advanced Full-Text Search**

**Current State:** Advanced full-text lexical search across the 'Itqan' dataset.

**The "Useless" Test:** A purely lexical (keyword-based) search is highly inadequate for the demands of *Takhrij*. Classical texts exhibit massive morphological variation, pervasive synonym usage, and varied transmission idioms. If a researcher searches for the exact phrase "began to weep," but a variant narration uses the phrase "his eyes shed tears," a standard full-text search will fail to group these narrations. This failure causes the researcher to miss a vital *Shahid* (corroborating report), potentially resulting in an incorrect final verdict on the Hadith.

**The Pivot: Semantic Knowledge-Graph Querying** The search architecture must pivot from superficial lexical matching to Semantic Textual Similarity (STS) utilizing advanced NLP transformer models fine-tuned on classical Islamic texts1. The software must integrate models like AraBERT to process semantic embeddings, allowing the system to retrieve entire *Takhreej* groups based on conceptual similarity, achieving high recall for variations in the *Matn*1. Furthermore, the search interface must allow users to query the network's topological structure directly. Using a graph database like Neo4j, a researcher should be able to execute a Cypher query to find: "All narrations originating from Companion X, passing through any narrator located in Basra between 100 AH and 150 AH, containing semantic concept Y"1. This transforms search from a text lookup into a multi-dimensional knowledge extraction tool.

### **Audit 2: Automated Isnad Visualization**

**Current State:** Automated schematic representation of the transmission tree with visual indicators for explicit anomalies.

**The "Useless" Test:** An automated tree visualization is dangerously misleading if it does not integrate textual variations (*Ikhtilaf al-Alfaz*). Displaying a visually massive, dense tree creates a false sense of security, heavily implying *Tawatur* (extreme continuous strength) to the user. However, if the text mutates drastically across those branches, the graph actually represents severe confusion (*Idtirab*), not strength6. Graphing the transmission chain without showing the mutation of the text is analogous to tracking the geographical movement of delivery trucks without knowing if the cargo they are carrying has been tampered with or replaced.

**The Pivot: The "Matn-Aware" Sankey Isnad Graph** The visualizer must abandon the separation of chain and text, integrating the *Matn* directly into the topology of the *Isnad*. The software should utilize morphological segmenters to tokenize the Arabic texts and apply Multiple Sequence Alignment (MSA) algorithms to generate a precise comparison matrix of all text variants6. The UI should then implement a Sankey diagram or a multi-edge topological graph where the edges (representing the act of transmission) are color-coded based on the textual variant they carry6. If a specific narrator introduces an anomalous addition (*Ziyadah*), the edge emanating from that specific node, and all subsequent child nodes that inherit that text, should visually highlight the presence of that altered text. This allows the researcher to instantly visually trace an anomaly back to its precise origin point, drastically reducing the time required to detect *Shudhudh*.

### **Audit 3: AI-Powered Summaries of Narrator Profiles**

**Current State:** AI-powered (RAG) summaries of narrator profiles and biographical data.

**The "Useless" Test:** As established in the gap analysis, AI-generated summaries of *Jarh wa Ta'dil* are fundamentally useless for deep academic research due to their lack of citability, the flattening of technical terminology, and the high risk of hallucination11.

**The Pivot: Hierarchical Extractive Retrieval and Tier Classification** The system architecture must pivot entirely from *abstractive* AI (generating new conversational text) to *extractive* AI (retrieving and structuring exact historical texts). The software should act as a sophisticated critical apparatus, extracting the exact quotes from the primary biographical dictionaries and presenting them hierarchically to the user. It must display Ibn Hajar's foundational ruling from *Taqrib al-Tahdhib* as the unalterable anchor, immediately followed by detailed assessments from *Tahdhib al-Kamal* or *Mizan al-I'tidal*4.

Crucially, instead of generating a paragraph summary, the AI should map the classical quotes to a standardized, mathematically operational tier system.

| Standardized Tier | Arabic Terminology | English Translation | Grading Impact on Chain |
| :---- | :---- | :---- | :---- |
| **T1** | *Sahabi* | Companion | Automatic pass; exempt from *Jarh wa Ta'dil* scrutiny. |
| **T2** | *Thiqah mutqin* | Very reliable, precise | Strongly supports a *Sahih* classification. |
| **T3** | *Thiqah* | Reliable | Supports a *Sahih* classification. |
| **T4** | *Saduq* | Truthful | Supports a *Hasan* classification. |
| **T5** | *Saduq yahim* | Truthful but errs | Supports a *Hasan* classification (borderline). |
| **T6** | *Maqbul* | Acceptable (conditionally) | *Da'if* alone; becomes *Hasan* only with corroboration. |
| **T7** | *Da'if / Majhul* | Weak / Unknown | *Da'if* (Weak); heavily reliant on *Taqwiyah* (corroboration). |
| **T8** | *Da'if jiddan* | Very weak | *Da'if*; eligible for *Taqwiyah* under exceptionally strict limits. |
| **T9 \- T11** | *Matruk / Muttaham* | Abandoned / Accused | Very weak; entirely blocks all corroboration (*Taqwiyah*). |
| **T12** | *Kadhdhab / Wadda'* | Liar / Fabricator | *Mawdu'* (Fabricated); completely rejected from the corpus. |

By using NLP to extract the exact term (e.g., identifying the phrase *"Saduq yahim"* in the raw biographical text) and algorithmically mapping it to Tier 5, the software can feed this structured data directly into a logical rules engine4. This allows the graph database to automatically compute the baseline grade of the transmission path using the lowest-tier node in that specific branch, bridging the gap between raw data and actionable academic insight4.

### **Missing Indispensable Feature: The 'Ilal and Taqwiyah Engine**

To bridge the final gap between basic visualization and true academic utility, the software must introduce a computational modeling engine designed explicitly for *Taqwiyah* (Mutual Strengthening) and *Ilal* (defect) detection.

Utilizing Graph Neural Networks (GNNs), the software should automatically detect genuinely independent paths within the *Isnad* network8. The system must structurally verify that corroborating branches do not share a hidden common bottleneck that would nullify their statistical independence. Based on the tier levels of the nodes and the proven independence of the paths, the software should project a preliminary grading matrix. For example, if the system detects a T7 (*Da'if*) chain, but utilizes its graph architecture to find two fully independent T7 paths corroborating it, the system should flag the overall cluster as a strong candidate for *Hasan li-ghayrihi*4. This pivots the software from a passive visualization canvas into an active, analytical research assistant capable of executing high-level deductive reasoning alongside the *Muhaqqiq*.

## **Conclusion**

The digitization of classical Islamic sciences requires a profound, unwavering respect for the epistemological frameworks established by the early masters of *Mustalah al-Hadith*. A software platform designed for the modern academic *Muhaqqiq* cannot rely on generic technology-industry paradigms—such as basic full-text search and abstractive AI summarization—which inherently flatten the deep nuance of textual criticism, spatio-temporal validation, and *Jarh wa Ta'dil*.

To achieve true indispensability, the 'Smart Hadith Tree' must undergo a rigorous architectural evolution. It must embrace Semantic Textual Similarity for comprehensive *Takhrij*, integrate Multiple Sequence Alignment directly into its topological *Isnad* graphs to expose *Ikhtilaf al-Alfaz*, and utilize hierarchical extractive retrieval to present a verifiable, citable critical apparatus of narrator profiles. By transforming from a superficial data-viewer into a deeply integrated computational engine that models historical time, geographical space, textual mutation, and the complex mathematics of *Taqwiyah*, the platform will not merely assist researchers; it will fundamentally elevate and accelerate the standard of modern digital *Tahqiq*.

#### **Works cited**

> 1. robust hadith ir using knowledge- graphs and semantic-similarity, [https://aircconline.com/csit/papers/vol13/csit131215.pdf](https://aircconline.com/csit/papers/vol13/csit131215.pdf)  
> 2. دراسة الاسانيد ( قطع كبير ) \- مركز احسان لدراسات السنة \- دار أطلس الخضراء, [https://daratlas.sa/products/%D8%AF%D8%B1%D8%A7%D8%B3%D8%A9-%D8%A7%D9%84%D8%A7%D8%B3%D8%A7%D9%86%D9%8A%D8%AF-%D9%85%D8%B1%D9%83%D8%B2-%D8%A7%D8%AD%D8%B3%D8%A7%D9%86-%D9%84%D8%AF%D8%B1%D8%A7%D8%B3%D8%A7%D8%AA-%D8%A7%D9%84%D8%B3%D9%86%D8%A9](https://daratlas.sa/products/%D8%AF%D8%B1%D8%A7%D8%B3%D8%A9-%D8%A7%D9%84%D8%A7%D8%B3%D8%A7%D9%86%D9%8A%D8%AF-%D9%85%D8%B1%D9%83%D8%B2-%D8%A7%D8%AD%D8%B3%D8%A7%D9%86-%D9%84%D8%AF%D8%B1%D8%A7%D8%B3%D8%A7%D8%AA-%D8%A7%D9%84%D8%B3%D9%86%D8%A9)  
> 3. الدور الثالث في القرن الثالث الهجري والنصف الأول من القرن الرابع الهجري, [https://awkafonline.gov.eg/content-sections/112/7401/%D8%A7%D9%84%D8%AF%D9%88%D8%B1-%D8%A7%D9%84%D8%AB%D8%A7%D9%84%D8%AB-%D9%81%D9%8A-%D8%A7%D9%84%D9%82%D8%B1%D9%86-%D8%A7%D9%84%D8%AB%D8%A7%D9%84%D8%AB-%D8%A7%D9%84%D9%87%D8%AC%D8%B1%D9%8A-%D9%88%D8%A7%D9%84%D9%86%D8%B5%D9%81-%D8%A7%D9%84%D8%A3%D9%88%D9%84-%D9%85%D9%86-%D8%A7%D9%84%D9%82%D8%B1%D9%86-%D8%A7%D9%84%D8%B1%D8%A7%D8%A8%D8%B9-%D8%A7%D9%84%D9%87%D8%AC%D8%B1%D9%8A](https://awkafonline.gov.eg/content-sections/112/7401/%D8%A7%D9%84%D8%AF%D9%88%D8%B1-%D8%A7%D9%84%D8%AB%D8%A7%D9%84%D8%AB-%D9%81%D9%8A-%D8%A7%D9%84%D9%82%D8%B1%D9%86-%D8%A7%D9%84%D8%AB%D8%A7%D9%84%D8%AB-%D8%A7%D9%84%D9%87%D8%AC%D8%B1%D9%8A-%D9%88%D8%A7%D9%84%D9%86%D8%B5%D9%81-%D8%A7%D9%84%D8%A3%D9%88%D9%84-%D9%85%D9%86-%D8%A7%D9%84%D9%82%D8%B1%D9%86-%D8%A7%D9%84%D8%B1%D8%A7%D8%A8%D8%B9-%D8%A7%D9%84%D9%87%D8%AC%D8%B1%D9%8A)  
> 4. IPSC V4 Technical Methodology — Islamic Primary Source Corpus, [https://ipsc.theogrid.ai/methodology/](https://ipsc.theogrid.ai/methodology/)  
> 5. ﺠﺯﺀ ﻓﻴﻪ ﺃﺤﺎﺩﻴﺙ ﺃﺒﻲ ﺍﻝﻴﻤﺎﻥ ﺍﻝﺤﻜﻡ ﺒﻥ ﻨﺎﻓﻊ A Volume with Hadiths of \- جامعة الأزهر, [https://www.alazhar.edu.ps/journal/attachedFile.asp?seqq1=2044](https://www.alazhar.edu.ps/journal/attachedFile.asp?seqq1=2044)  
> 6. OmarShafie/hadith: a search engine which provides Visual ... \- GitHub, [https://github.com/OmarShafie/hadith](https://github.com/OmarShafie/hadith)  
> 7. ﺍﳊﺒﺎﺋﻚ ﰲ ﺃﺧﺒﺎﺭ ﺍﳌﻼﺋﻚ, [https://www.alhesn.net/upload/upload1649996042291.pdf](https://www.alhesn.net/upload/upload1649996042291.pdf)  
> 8. Multi-IsnadSet MIS for Sahih Muslim Hadith with chain of narrators, [https://pmc.ncbi.nlm.nih.gov/articles/PMC11096860/](https://pmc.ncbi.nlm.nih.gov/articles/PMC11096860/)  
> 9. Full text of "Aliran Kritik Hadith Semasa Analisis" \- Internet Archive, [https://archive.org/stream/AliranKritikHadithSemasaAnalisis/Aliran\_Kritik\_Hadith\_Semasa\_Analisis\_djvu.txt](https://archive.org/stream/AliranKritikHadithSemasaAnalisis/Aliran_Kritik_Hadith_Semasa_Analisis_djvu.txt)  
> 10. Types of Narrations – According to the Shia \- Mahajjah, [https://mahajjah.com/types-of-narrations-according-to-the-shia/](https://mahajjah.com/types-of-narrations-according-to-the-shia/)  
> 11. An Analytical Study of the Tahqiq Methodology of ʿAbd Al-Fattah, [https://rsisinternational.org/journals/ijriss/articles/an-analytical-study-of-the-tahqiq-methodology-of-%CA%BFabd-al-fattah-abu-ghuddah-in-his-work-al-raf%CA%BFu-wa-al-takmil-fi-al-jarh-wa-al-ta%CA%BFdil/](https://rsisinternational.org/journals/ijriss/articles/an-analytical-study-of-the-tahqiq-methodology-of-%CA%BFabd-al-fattah-abu-ghuddah-in-his-work-al-raf%CA%BFu-wa-al-takmil-fi-al-jarh-wa-al-ta%CA%BFdil/)  
> 12. Ilm ar-Rijal: The Science of Narrators | Islam365, [https://islam365.io/topic/hadith-ilm\_al\_rijal](https://islam365.io/topic/hadith-ilm_al_rijal)  
> 13. Rijal al-Hadith (the study of the reporters of Hadith) \- IslamBasics.com, [https://islambasics.com/chapter/rijal-al-hadith-the-study-of-the-reporters-of-hadith/](https://islambasics.com/chapter/rijal-al-hadith-the-study-of-the-reporters-of-hadith/)  
> 14. Ilm E Rijaal {The Science of Hadith Narrators} Level 01 By, [https://askmadani.com/wp-content/uploads/2024/09/Ilm-rijaal-level-1-2019-2020-batch-10-lessons-23-2-2021-final-copy-2-1.pdf](https://askmadani.com/wp-content/uploads/2024/09/Ilm-rijaal-level-1-2019-2020-batch-10-lessons-23-2-2021-final-copy-2-1.pdf)  
> 15. Closing the Gap in Non-Latin-Script Data – Projects \- GitHub Pages, [https://m-l-d-h.github.io/Closing-The-Gap-In-Non-Latin-Script-Data/](https://m-l-d-h.github.io/Closing-The-Gap-In-Non-Latin-Script-Data/)  
> 16. EMNLP 2023, [https://2023.emnlp.org/downloads/EMNLP-2023-Handbook-Dec-03.pdf](https://2023.emnlp.org/downloads/EMNLP-2023-Handbook-Dec-03.pdf)  
> 17. (PDF) Semantic Textual Similarity Methods, Tools, and Applications, [https://www.researchgate.net/publication/313362937\_Semantic\_Textual\_Similarity\_Methods\_Tools\_and\_Applications\_A\_Survey](https://www.researchgate.net/publication/313362937_Semantic_Textual_Similarity_Methods_Tools_and_Applications_A_Survey)  
> 18. Large Language Models (LLMs) in Protein Bioinformatics (Methods, [https://dokumen.pub/large-language-models-llms-in-protein-bioinformatics-methods-in-molecular-biology-2941.html](https://dokumen.pub/large-language-models-llms-in-protein-bioinformatics-methods-in-molecular-biology-2941.html)  
> 19. Multi-IsnadSet MIS for Sahih Muslim Hadith with chain of narrators, [https://www.researchgate.net/publication/380007041\_Multi-IsnadSet\_MIS\_for\_Sahih\_Muslim\_Hadith\_with\_chain\_of\_Narrators\_based\_on\_multiple\_ISNAD](https://www.researchgate.net/publication/380007041_Multi-IsnadSet_MIS_for_Sahih_Muslim_Hadith_with_chain_of_Narrators_based_on_multiple_ISNAD)  
> 20. Criticism of Orientalist Critical Views Toward Hadith Studies, [https://jurnal.asilha.com/index.php/johs/article/download/32/21/84](https://jurnal.asilha.com/index.php/johs/article/download/32/21/84)  
> 21. سنن ابن ماجه \- شبكة إحسان \- رابطة الشبكة العالمية لدراسة الحديث إحسان, [https://www.ihsanetwork.org/printededition/printededition3/ibnmajah.aspx](https://www.ihsanetwork.org/printededition/printededition3/ibnmajah.aspx)  
> 22. العمدة في محاسن الشعر وآدابه (ط. أخرى) | مجلد 1 | صفحة 13 | منهج التحقيق, [https://ketabonline.com/ar/books/54579/read?part=1\&page=13\&index=5892756](https://ketabonline.com/ar/books/54579/read?part=1&page=13&index=5892756)  
> 23. Exploring Semantic Hadith Overlap Across Topics | Request PDF, [https://www.researchgate.net/publication/388618126\_Exploring\_Semantic\_Hadith\_Overlap\_Across\_Topics](https://www.researchgate.net/publication/388618126_Exploring_Semantic_Hadith_Overlap_Across_Topics)  
> 24. Text Preprocessing Pipeline preparing Classical Arabic Literature, [https://aclanthology.org/2024.osact-1.8.pdf](https://aclanthology.org/2024.osact-1.8.pdf)  
> 25. (PDF) Enhancing the Takhrij Al-Hadith based on Contextual, [https://www.researchgate.net/publication/356830950\_Enhancing\_the\_Takhrij\_Al-Hadith\_based\_on\_Contextual\_Similarity\_using\_BERT\_Embeddings](https://www.researchgate.net/publication/356830950_Enhancing_the_Takhrij_Al-Hadith_based_on_Contextual_Similarity_using_BERT_Embeddings)  
> 26. Pivot Approach for Extracting Paraphrase Patterns from Bilingual, [https://www.researchgate.net/publication/220873236\_Pivot\_Approach\_for\_Extracting\_Paraphrase\_Patterns\_from\_Bilingual\_Corpora](https://www.researchgate.net/publication/220873236_Pivot_Approach_for_Extracting_Paraphrase_Patterns_from_Bilingual_Corpora)