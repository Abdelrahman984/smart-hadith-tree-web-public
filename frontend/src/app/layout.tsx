import type { Metadata, Viewport } from "next";
import { Noto_Sans_Arabic, Inter } from "next/font/google";
import { QueryProvider } from "@/components/QueryProvider";
import "./globals.css";

const notoSansArabic = Noto_Sans_Arabic({
  variable: "--font-noto-sans-arabic",
  subsets: ["arabic"],
  weight: ["400", "500", "600", "700"],
});

const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: {
    default: "شجرة الأسانيد الذكية | Smart Hadith Tree",
    template: "%s | شجرة الأسانيد الذكية",
  },
  description: "A platform for digitizing and visualizing Hadith narrator chains (Isnad)",
};

// Browser UI colour on phones: the brand navy.
export const viewport: Viewport = {
  themeColor: "#1A3A5C",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ar" dir="rtl" className={`${notoSansArabic.variable} ${inter.variable} font-arabic h-full antialiased`}>
      <body suppressHydrationWarning className="min-h-full flex flex-col bg-surface-muted text-slate-900">
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:fixed focus:start-3 focus:top-3 focus:z-[100] focus:rounded-lg focus:bg-surface focus:px-4 focus:py-2 focus:text-sm focus:font-bold focus:text-brand-dark focus:shadow-lg focus:ring-2 focus:ring-brand-blue"
        >
          تخطَّ إلى المحتوى
        </a>
        <QueryProvider>
          {children}
        </QueryProvider>
      </body>
    </html>
  );
}
