"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ClipboardPlus, RefreshCw } from "lucide-react";

import { useAuth } from "@/components/auth/auth-provider";
import { isNavigationItemActive, isOperatorOnly } from "@/components/layout/navigation";

const items = [
  { href: "/coleta", label: "Coleta", icon: ClipboardPlus },
  { href: "/sincronizacao", label: "Sincronização", icon: RefreshCw },
] as const;

export function OperatorBottomNavigation() {
  const pathname = usePathname();
  const { user } = useAuth();
  if (!isOperatorOnly(user.roles)) return null;

  return (
    <nav aria-label="Navegação do operador" className="fixed inset-x-0 bottom-0 z-40 border-t bg-white/98 px-3 pb-[max(.5rem,env(safe-area-inset-bottom))] pt-2 shadow-[0_-8px_24px_rgba(15,23,42,.08)] lg:hidden">
      <div className="mx-auto grid max-w-sm grid-cols-2 gap-2">
        {items.map((item) => {
          const active = isNavigationItemActive(pathname, item.href);
          const Icon = item.icon;
          return (
            <Link key={item.href} href={item.href} aria-current={active ? "page" : undefined} className={`flex min-h-12 flex-col items-center justify-center rounded-lg px-3 text-xs font-bold ${active ? "bg-blue-950 text-white" : "text-slate-600"}`}>
              <Icon className="mb-0.5 size-5" aria-hidden="true" />{item.label}
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
