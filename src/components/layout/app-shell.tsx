"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";

import { AuthProvider } from "@/components/auth/auth-provider";
import { Header } from "@/components/layout/header";
import { Sidebar } from "@/components/layout/sidebar";
import { SyncProvider } from "@/components/offline/sync-provider";

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
            <main className="min-w-0 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
              {children}
            </main>
          </div>
        </div>
      </SyncProvider>
    </AuthProvider>
  );
}
