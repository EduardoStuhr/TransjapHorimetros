"use client";

import Image from "next/image";
import { usePathname } from "next/navigation";
import { LogOut } from "lucide-react";

import { useAuth } from "@/components/auth/auth-provider";
import { MobileNavigation } from "@/components/layout/mobile-navigation";
import { Button } from "@/components/ui/button";

const routeTitles: Record<string, string> = {
  "/": "Dashboard",
  "/frota": "Frota",
  "/horimetros": "Horímetros",
  "/obras": "Obras",
  "/alertas": "Alertas",
  "/relatorios": "Relatórios",
};

export function Header() {
  const pathname = usePathname();
  const { logout, user } = useAuth();
  const title = pathname.startsWith("/frota/")
    ? "Detalhes da máquina"
    : (routeTitles[pathname] ?? "Transjap Horímetros");

  return (
    <header className="sticky top-0 z-30 border-b bg-white/95 backdrop-blur-sm">
      <div className="flex h-18 items-center justify-between gap-4 px-4 sm:px-6 lg:px-8">
        <div className="flex min-w-0 items-center gap-3">
          <MobileNavigation />
          <div className="relative size-10 shrink-0 overflow-hidden rounded-md border bg-slate-900 lg:hidden">
            <Image
              src="/simbolo-transjap.png"
              alt=""
              fill
              sizes="40px"
              className="object-cover"
            />
          </div>
          <div className="min-w-0">
            <p className="truncate text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">
              Transjap · Administração
            </p>
            <h1 className="truncate text-lg font-bold text-slate-900 sm:text-xl">
              {title}
            </h1>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className="hidden text-right sm:block">
            <p className="max-w-52 truncate text-sm font-semibold text-slate-900">{user.fullName}</p>
            <p className="max-w-52 truncate text-xs text-slate-500">{user.email}</p>
          </div>
          <Button variant="outline" size="icon" onClick={() => void logout()} aria-label="Sair">
            <LogOut />
          </Button>
        </div>
      </div>
    </header>
  );
}
