import type { Metadata } from "next";
import SearchPageClient from "@/features/search/SearchPageClient";

export const metadata: Metadata = {
  title: "البحث والتخريج",
  description: "ابحث في متون الأحاديث وأسانيدها عبر 31 ديواناً، وحدد الروايات لرسم شجرة التخريج المقارنة.",
};

export default function SearchPage() {
  return <SearchPageClient />;
}
