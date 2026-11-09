import type { Metadata } from "next";
import { Figtree, Geist_Mono } from "next/font/google";
import { NuqsAdapter } from "nuqs/adapters/next/app";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

// Figtree is the site's only sans-serif family (see --font-sans in globals.css). Geist Mono stays for codes.
const figtree = Figtree({
  variable: "--font-figtree",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "RondiTrack",
  description: "Track your stokvel contributions and payouts.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${geistMono.variable} ${figtree.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        {/* nuqs keeps filters in the address bar (?member=&from=&to=). Its adapter must wrap every page that uses it. */}
        <NuqsAdapter>{children}</NuqsAdapter>
        {/* Toasts (sonner). One place for the whole site; each toast sets its own duration. */}
        <Toaster position="bottom-right" />
      </body>
    </html>
  );
}
