"use client";

import Image from "next/image";
import { usePathname } from "next/navigation";
import { Cable } from "lucide-react";

import { MobileNavigation } from "@/components/layout/mobile-navigation";

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

        <div
          className="hidden items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-600 sm:flex"
          title="A API será integrada em uma etapa futura"
        >
          <Cable className="size-4 text-slate-500" aria-hidden="true" />
          <span>API pendente</span>
        </div>
      </div>
    </header>
  );
}
