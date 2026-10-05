import java.io.*;
import java.lang.reflect.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/**
 * High-speed bulk extractor for Shamela 4 local Lucene 10.4.0 stores (store/page and store/title).
 * Compiles with standard JDK (e.g. JDK 17+) using reflection and runs on Shamela 4's bundled OpenJDK 21:
 *
 * Compile:
 *   javac -encoding UTF-8 scripts/shamela4-extractor/ShamelaLuceneDumper.java
 * Run:
 *   "D:\Islamic\shamela4\app\win\64\jre\2\bin\java.exe" "--add-modules=jdk.incubator.vector" ^
 *     -cp "D:\Islamic\shamela4\app\lucene\2\*;scripts\shamela4-extractor" ShamelaLuceneDumper ^
 *     "D:\Islamic\shamela4\database\store" "data\shamela_dump" [bookId,bookId,...]
 * The optional third argument is a comma-separated list of Shamela book IDs (default: the 19 hadith books).
 * Footnotes (the "foot" field) are written to <id>_foot.tsv when the book has any.
 */
public class ShamelaLuceneDumper {
    public static void main(String[] args) throws Exception {
        String shamelaStore = args.length > 0 ? args[0] : "D:\\Islamic\\shamela4\\database\\store";
        Path outDir = Paths.get(args.length > 1 ? args[1] : "data\\shamela_dump");
        Files.createDirectories(outDir);

        Class<?> fsDirClass = Class.forName("org.apache.lucene.store.FSDirectory");
        Class<?> dirClass = Class.forName("org.apache.lucene.store.Directory");
        Class<?> dirReaderClass = Class.forName("org.apache.lucene.index.DirectoryReader");
        Class<?> indexReaderClass = Class.forName("org.apache.lucene.index.IndexReader");
        Class<?> searcherClass = Class.forName("org.apache.lucene.search.IndexSearcher");
        Class<?> termClass = Class.forName("org.apache.lucene.index.Term");
        Class<?> termQueryClass = Class.forName("org.apache.lucene.search.TermQuery");
        Class<?> queryClass = Class.forName("org.apache.lucene.search.Query");
        Class<?> sortClass = Class.forName("org.apache.lucene.search.Sort");
        Class<?> luceneBulkClass = Class.forName("ws.shamela.LuceneBulk");

        Object pageDir = fsDirClass.getMethod("open", Path.class).invoke(null, Paths.get(shamelaStore, "page"));
        Object pageReader = dirReaderClass.getMethod("open", dirClass).invoke(null, pageDir);
        Object pageSearcher = searcherClass.getConstructor(indexReaderClass).newInstance(pageReader);

        Object titleDir = fsDirClass.getMethod("open", Path.class).invoke(null, Paths.get(shamelaStore, "title"));
        Object titleReader = dirReaderClass.getMethod("open", dirClass).invoke(null, titleDir);
        Object titleSearcher = searcherClass.getConstructor(indexReaderClass).newInstance(titleReader);

        Object indexOrderSort = sortClass.getField("INDEXORDER").get(null);
        Method queryRows = luceneBulkClass.getMethod("queryRows", searcherClass, queryClass, int.class, sortClass, String[].class);

        String[] bookIds = args.length > 2 ? args[2].split(",") : new String[]{
            "13174", "1456", "9344", "8493", "13122", "13159", "12981",
            "8361", "12520", "1446", "18144", "537", "1733", "28171",
            "13068", "9771", "1424", "148486", "10660"
        };

        long totalStart = System.currentTimeMillis();
        for (String bid : bookIds) {
            long t0 = System.currentTimeMillis();
            Object term = termClass.getConstructor(String.class, String.class).newInstance("book_key", bid);
            Object q = termQueryClass.getConstructor(termClass).newInstance(term);

            @SuppressWarnings("unchecked")
            List<String[]> pages = (List<String[]>) queryRows.invoke(null, pageSearcher, q, 200000, indexOrderSort, new String[]{"body", "foot"});
            @SuppressWarnings("unchecked")
            List<String[]> titles = (List<String[]>) queryRows.invoke(null, titleSearcher, q, 200000, indexOrderSort, new String[]{"body"});

            try (BufferedWriter pw = Files.newBufferedWriter(outDir.resolve(bid + "_pages.tsv"), StandardCharsets.UTF_8)) {
                for (String[] row : pages) {
                    String id = row[0] != null ? row[0] : "";
                    String body = row[1] != null ? row[1].replace("\t", " ").replace("\r\n", "\\n").replace("\r", "\\n").replace("\n", "\\n") : "";
                    pw.write(id + "\t" + body + "\n");
                }
            }

            // Footnotes go to their own file (only when the book has any), so <id>_pages.tsv keeps its format.
            if (pages.stream().anyMatch(r -> r.length > 2 && r[2] != null && !r[2].isBlank())) {
                try (BufferedWriter fw = Files.newBufferedWriter(outDir.resolve(bid + "_foot.tsv"), StandardCharsets.UTF_8)) {
                    for (String[] row : pages) {
                        if (row.length < 3 || row[2] == null || row[2].isBlank()) continue;
                        String id = row[0] != null ? row[0] : "";
                        String foot = row[2].replace("\t", " ").replace("\r\n", "\\n").replace("\r", "\\n").replace("\n", "\\n");
                        fw.write(id + "\t" + foot + "\n");
                    }
                }
            }

            try (BufferedWriter tw =Files.newBufferedWriter(outDir.resolve(bid + "_titles.tsv"), StandardCharsets.UTF_8)) {
                for (String[] row : titles) {
                    String id = row[0] != null ? row[0] : "";
                    String body = row[1] != null ? row[1].replace("\t", " ").replace("\r\n", " ").replace("\r", " ").replace("\n", " ") : "";
                    tw.write(id + "\t" + body + "\n");
                }
            }

            long t1 = System.currentTimeMillis();
            System.out.println("Dumped book " + bid + ": " + pages.size() + " pages, " + titles.size() + " titles in " + (t1 - t0) + "ms");
        }
        System.out.println("ALL " + bookIds.length + " BOOKS DUMPED in " + (System.currentTimeMillis() - totalStart) + "ms!");
    }
}
