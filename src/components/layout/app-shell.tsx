"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";

import { AuthProvider } from "@/components/auth/auth-provider";
import { Header } from "@/components/layout/header";
import { OperatorBottomNavigation } from "@/components/layout/operator-bottom-navigation";
import { Sidebar } from "@/components/layout/sidebar";
import { SyncProvider } from "@/components/offline/sync-provider";
import { InstallPrompt } from "@/components/pwa/install-prompt";

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();

  if (pathname === "/login") {
    return children;
  }

  return (
    <AuthProvider>
      <SyncProvider>
        <div className="min-h-screen">
          <Sidebar />
          <div className="min-h-screen min-w-0 overflow-x-hidden lg:pl-68">
            <Header />
            <main className="min-w-0 px-4 pb-[calc(6.5rem+env(safe-area-inset-bottom))] pt-5 sm:px-6 lg:px-8 lg:py-8">
              {children}
            </main>
          </div>
          <InstallPrompt />
          <OperatorBottomNavigation />
        </div>
      </SyncProvider>
    </AuthProvider>
  );
}
