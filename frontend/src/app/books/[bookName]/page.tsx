import type { Metadata } from "next";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";
import Breadcrumbs from "@/features/books/Breadcrumbs";
import ChapterList from "@/features/books/ChapterList";
import { decodeParam } from "@/features/books/bookParams";
import { getBookMeta } from "@/lib/bookTheme";
import { CORPUS_BOOKS } from "@/lib/corpus";

type Params = Promise<{ bookName: string }>;

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { bookName } = await params;
  return { title: `أبواب ${decodeParam(bookName)}` };
}

export default async function BookChaptersPage({ params }: { params: Params }) {
  const bookName = decodeParam((await params).bookName);
  const meta = getBookMeta(bookName);
  const corpusBook = CORPUS_BOOKS.find((b) => b.name === bookName);

  return (
    <>
      <SiteHeader />
      <main id="main-content" className="mx-auto w-full max-w-4xl p-4 pt-6 sm:p-6 sm:pt-8">
        <Breadcrumbs items={[{ label: "فهرس الدواوين", href: "/books" }, { label: bookName }]} />
        <div className="mb-6 flex items-center gap-3">
          <span className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl border text-sm font-bold ${meta.badgeClass}`}>
            {meta.code}
          </span>
          <div className="min-w-0">
            <h1 className="text-2xl font-bold text-brand-dark sm:text-3xl">{bookName}</h1>
            {corpusBook && (
              <p className="text-sm text-ink-subtle">
                {corpusBook.author} · <span className="font-latin">{corpusBook.hadithsCount}</span> حديث
              </p>
            )}
          </div>
        </div>
        <ChapterList bookName={bookName} />
      </main>
      <SiteFooter />
    </>
  );
}
