"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { DatabaseZap } from "lucide-react";

import {
  isNavigationItemActive,
  navigationItems,
} from "@/components/layout/navigation";

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className="fixed inset-y-0 left-0 z-40 hidden w-68 flex-col border-r border-white/10 bg-[var(--sidebar)] text-[var(--sidebar-foreground)] lg:flex">
      <div className="border-b border-white/10 px-5 py-5">
        <Link
          href="/"
          aria-label="Ir para o Dashboard"
          className="block rounded-md bg-white px-3 py-3 shadow-sm"
        >
          <Image
            src="/logo-transjap.png"
            alt="Transjap — Terraplenagem e Construções"
            width={190}
            height={53}
            priority
            className="h-auto w-full"
          />
        </Link>
        <p className="mt-3 text-xs font-semibold uppercase tracking-[0.16em] text-blue-100/70">
          Gestão de horímetros
        </p>
      </div>

      <nav aria-label="Navegação principal" className="flex-1 space-y-1 px-3 py-5">
        {navigationItems.map((item) => {
          const isActive = isNavigationItemActive(pathname, item.href);
          const Icon = item.icon;

          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={isActive ? "page" : undefined}
              className={
                isActive
                  ? "flex min-h-11 items-center gap-3 rounded-md border-l-4 border-amber-400 bg-white/10 px-3 py-2.5 text-sm font-semibold text-white"
                  : "flex min-h-11 items-center gap-3 rounded-md border-l-4 border-transparent px-3 py-2.5 text-sm font-medium text-blue-50/80 transition-colors hover:bg-white/7 hover:text-white"
              }
            >
              <Icon className="size-5 shrink-0" aria-hidden="true" />
              {item.label}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-white/10 px-5 py-4">
        <div className="flex items-start gap-3 text-xs text-blue-100/70">
          <DatabaseZap className="mt-0.5 size-4 shrink-0 text-amber-400" />
          <p>
            Interface preparada para integração com a API oficial.
          </p>
        </div>
      </div>
    </aside>
  );
}
