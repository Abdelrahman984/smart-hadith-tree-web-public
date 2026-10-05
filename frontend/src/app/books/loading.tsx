import SiteHeader from "@/components/SiteHeader";
import { LoadingState } from "@/components/StateViews";

export default function BooksLoading() {
  return (
    <>
      <SiteHeader />
      <main id="main-content" className="flex flex-1 items-center justify-center">
        <LoadingState />
      </main>
    </>
  );
}
