import type { Metadata } from "next";

import { AppShell } from "@/components/layout/app-shell";
import { TooltipProvider } from "@/components/ui/tooltip";

import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Transjap Horímetros",
    template: "%s | Transjap Horímetros",
  },
  description:
    "Sistema administrativo para gestão de horímetros da frota Transjap.",
  icons: {
    icon: "/simbolo-transjap.png",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="pt-BR">
      <body>
        <TooltipProvider>
          <AppShell>{children}</AppShell>
        </TooltipProvider>
      </body>
    </html>
  );
}
