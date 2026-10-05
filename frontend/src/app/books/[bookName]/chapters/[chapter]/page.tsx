import type { Metadata } from "next";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";
import Breadcrumbs from "@/features/books/Breadcrumbs";
import ChapterHadithList from "@/features/books/ChapterHadithList";
import { bookHref, decodeParam } from "@/features/books/bookParams";

type Params = Promise<{ bookName: string; chapter: string }>;

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { bookName, chapter } = await params;
  return { title: `${decodeParam(chapter)} - ${decodeParam(bookName)}` };
}

export default async function ChapterHadithsPage({ params }: { params: Params }) {
  const resolved = await params;
  const bookName = decodeParam(resolved.bookName);
  const chapter = decodeParam(resolved.chapter);

  return (
    <>
      <SiteHeader />
      <main id="main-content" className="mx-auto w-full max-w-4xl p-4 pt-6 sm:p-6 sm:pt-8">
        <Breadcrumbs
          items={[
            { label: "فهرس الدواوين", href: "/books" },
            { label: bookName, href: bookHref(bookName) },
            { label: chapter },
          ]}
        />
        <h1 className="mb-6 text-2xl font-bold text-brand-dark sm:text-3xl">{chapter}</h1>
        <ChapterHadithList bookName={bookName} chapter={chapter} />
      </main>
      <SiteFooter />
    </>
  );
}
