import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { cache } from "react";
import { getIsnadTree, NotFoundError } from "@/lib/api";
import TreeWorkspace from "@/features/isnad-tree/components/TreeWorkspace";

type Params = Promise<{ hadithId: string }>;

/** One request per render, shared by the metadata and the page. A 404 shows the not-found page; other errors the error page. */
const loadTree = cache(async (hadithId: string) => {
  try {
    return await getIsnadTree(hadithId);
  } catch (error) {
    if (error instanceof NotFoundError) notFound();
    throw error;
  }
});

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { hadithId } = await params;
  const tree = await loadTree(hadithId);
  return { title: `شجرة إسناد ${tree.bookName} ${tree.hadithNumber}` };
}

export default async function TreePage({ params }: { params: Params }) {
  const { hadithId } = await params;
  const treeData = await loadTree(hadithId);

  return <TreeWorkspace treeData={treeData} hadithId={hadithId} />;
}
