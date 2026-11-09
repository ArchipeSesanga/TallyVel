import type { Metadata } from "next";
import { Figtree } from "next/font/google";
import { NuqsAdapter } from "nuqs/adapters/next/app";
import { Toaster } from "@/components/ui/sonner";
import { WebVitalsLogger } from "@/components/WebVitalsLogger";
import { OG_IMAGE, SITE_DESCRIPTION, SITE_NAME, SITE_TITLE, siteUrl } from "@/lib/siteMetadata";
import "./globals.css";

// Figtree is the site's ONE typeface: --font-sans and --font-mono both point at it (see globals.css), so nothing
// on the site can render in another family.
const figtree = Figtree({
  variable: "--font-figtree",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
});

// The default head, for pages that do not set their own: the sign-in and register pages (indexable, so people can find
// the product). The signed-in pages say noindex in (app)/layout.tsx; the invite page has its own in join/page.tsx.
export const metadata: Metadata = {
  metadataBase: siteUrl(),
  title: { default: SITE_TITLE, template: `%s · ${SITE_NAME}` },
  description: SITE_DESCRIPTION,
  openGraph: { type: "website", siteName: SITE_NAME, title: SITE_TITLE, description: SITE_DESCRIPTION, images: [OG_IMAGE] },
  twitter: { card: "summary_large_image", title: SITE_TITLE, description: SITE_DESCRIPTION, images: [OG_IMAGE.url] },
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
        {/* Measurement only: logs INP to the console when built with NEXT_PUBLIC_LOG_WEB_VITALS=1, otherwise does nothing. */}
        <WebVitalsLogger />
      </body>
    </html>
  );
}
