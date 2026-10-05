import type { Metadata } from "next";
import TakhreejPageClient from "@/features/isnad-tree/TakhreejPageClient";

export const metadata: Metadata = {
  title: "شجرة التخريج المقارنة",
};

export default function TakhreejPage() {
  return <TakhreejPageClient />;
}
