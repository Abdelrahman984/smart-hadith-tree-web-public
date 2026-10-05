import type { Metadata } from "next";
import VerifyPageClient from "@/features/verify/VerifyPageClient";

export const metadata: Metadata = {
  title: "التحقق من نص حديث",
  description: "ابحث عن نص يُنسب إلى النبي ﷺ في الدواوين المتاحة، دون إصدار حكم بالصحة أو الضعف.",
};

export default function VerifyPage() {
  return <VerifyPageClient />;
}
