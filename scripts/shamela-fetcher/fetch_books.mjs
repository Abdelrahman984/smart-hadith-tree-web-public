import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { SSEClientTransport } from "@modelcontextprotocol/sdk/client/sse.js";
import fs from "fs";
import path from "path";

const books = [
    { slug: "musannaf_abdurrazzaq", id: 13174 },
    { slug: "musnad_tayalisi", id: 1456 },
    { slug: "musnad_shafii", id: 9344 },
    { slug: "musnad_humaydi", id: 8493 },
    { slug: "sunan_said_ibn_mansur", id: 13122 },
    { slug: "musnad_ishaq", id: 13159 },
    { slug: "musnad_bazzar", id: 12981 },
    { slug: "sunan_kubra_nasai", id: 8361 },
    { slug: "musnad_abi_yala", id: 12520 },
    { slug: "sahih_ibn_khuzaymah", id: 1446 },
    { slug: "mustakhraj_abi_awanah", id: 18144 },
    { slug: "sahih_ibn_hibban", id: 537 },
    { slug: "mujam_kabir_tabarani", id: 1733 },
    { slug: "mujam_awsat_tabarani", id: 28171 },
    { slug: "mujam_saghir_tabarani", id: 1734 },
    { slug: "sunan_daraqutni", id: 9771 },
    { slug: "mustadrak_hakim", id: 1424 },
    { slug: "sunan_kubra_bayhaqi", id: 148486 },
    { slug: "shuab_iman_bayhaqi", id: 10660 }
];

const DATA_DIR = path.resolve("../../data/itqan/sunni");

async function main() {
    console.log("Connecting to Shamela MCP over SSE...");
    const transport = new SSEClientTransport(new URL("https://shamela.link/mcp/sse"));
    const client = new Client({
        name: "shamela-fetcher",
        version: "1.0.0"
    }, {
        capabilities: {}
    });

    await client.connect(transport);
    console.log("Connected.");

    for (const book of books) {
        console.log(`Processing ${book.slug} (ID: ${book.id})...`);
        const bookDir = path.join(DATA_DIR, book.slug);
        
        // Ensure directory exists
        if (!fs.existsSync(bookDir)) {
            fs.mkdirSync(bookDir, { recursive: true });
        }

        // Fetch TOC first to build index.json
        console.log(`  Fetching TOC...`);
        let tocResult;
        try {
            const result = await client.callTool({
                name: "shamela_get_toc",
                arguments: { book_id: book.id }
            });
            const content = result.content.find(c => c.type === "text").text;
            tocResult = JSON.parse(content);
        } catch (e) {
            console.error(`  Failed to get TOC: ${e.message}`);
            continue;
        }

        const indexData = {
            metadata: {
                id: book.id,
                title: book.slug
            },
            chapters: []
        };

        if (!tocResult.structuredContent) {
            console.warn("  No structuredContent in TOC!");
            continue;
        }

        let chapterCount = 1;
        for (const item of tocResult.structuredContent) {
            const chapterTitle = item.title;
            const titleId = item.title_id;
            
            console.log(`  Fetching chapter ${chapterCount}: ${chapterTitle}...`);
            
            let chapterPages = [];
            let currentTitleId = titleId;
            // Note: shamela_get_book_section returns max 30 pages by default. We can fetch using start_page_id via shamela_get_pages_range if section is too long.
            // For simplicity, let's just fetch up to 100 pages using get_book_section max_pages
            try {
                let truncated = false;
                let nextStartPageId = null;
                
                let res = await client.callTool({
                    name: "shamela_get_book_section",
                    arguments: { book_id: book.id, title_id: titleId, max_pages: 100, response_format: "json" }
                });
                
                let textRes = res.content.find(c => c.type === "text").text;
                let parsed = JSON.parse(textRes);
                
                if (parsed.structuredContent && parsed.structuredContent.pages) {
                    chapterPages = parsed.structuredContent.pages.map(p => ({
                        page_id: p.page_id,
                        body: p.body,
                        foot: p.foot,
                        number: p.number
                    }));
                }
            } catch(e) {
                console.error(`  Error fetching chapter ${chapterCount}: ${e.message}`);
            }

            if (chapterPages.length > 0) {
                const chapterFileName = `chapter_${chapterCount}.json`;
                fs.writeFileSync(path.join(bookDir, chapterFileName), JSON.stringify({
                    title: chapterTitle,
                    pages: chapterPages
                }, null, 2));

                indexData.chapters.push({
                    title: chapterTitle,
                    file: chapterFileName
                });
            }

            chapterCount++;
            
            // To prevent hitting rate limits or taking hours, let's only fetch first 5 chapters for testing the ETL pipeline.
            if (chapterCount > 5) {
                console.log("  Stopping after 5 chapters for test run.");
                break;
            }
        }

        fs.writeFileSync(path.join(bookDir, "index.json"), JSON.stringify(indexData, null, 2));
        console.log(`  Finished ${book.slug}.`);
        
        // Delay to be nice to the server
        await new Promise(r => setTimeout(r, 1000));
    }
    
    console.log("All books processed.");
    process.exit(0);
}

main().catch(console.error);
