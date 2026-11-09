import type { Metadata } from "next";
import { Figtree } from "next/font/google";
import { NuqsAdapter } from "nuqs/adapters/next/app";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

// Figtree is the site's ONE typeface: --font-sans and --font-mono both point at it (see globals.css), so nothing
// on the site can render in another family.
const figtree = Figtree({
  variable: "--font-figtree",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
});

export const metadata: Metadata = {
  title: "RondiTrack",
  description: "Track your stokvel contributions and payouts.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${figtree.variable} h-full antialiased`}
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
