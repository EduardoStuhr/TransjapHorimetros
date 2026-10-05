"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Menu } from "lucide-react";

import {
  isNavigationItemActive,
  navigationItems,
} from "@/components/layout/navigation";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";

export function MobileNavigation() {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button
          variant="outline"
          size="icon"
          className="lg:hidden"
          aria-label="Abrir navegação"
        >
          <Menu />
        </Button>
      </SheetTrigger>
      <SheetContent
        side="left"
        className="w-[86vw] max-w-80 border-r-0 bg-[var(--sidebar)] p-0 text-white"
      >
        <SheetHeader className="border-b border-white/10 p-5 text-left">
          <div className="rounded-md bg-white px-3 py-3">
            <Image
              src="/logo-transjap.png"
              alt="Transjap — Terraplenagem e Construções"
              width={190}
              height={53}
              className="h-auto w-full"
            />
          </div>
          <SheetTitle className="sr-only">Navegação principal</SheetTitle>
          <SheetDescription className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-100/70">
            Gestão de horímetros
          </SheetDescription>
        </SheetHeader>

        <nav aria-label="Navegação principal" className="space-y-1 p-3">
          {navigationItems.map((item) => {
            const isActive = isNavigationItemActive(pathname, item.href);
            const Icon = item.icon;

            return (
              <Link
                key={item.href}
                href={item.href}
                onClick={() => setOpen(false)}
                aria-current={isActive ? "page" : undefined}
                className={
                  isActive
                    ? "flex min-h-11 items-center gap-3 rounded-md border-l-4 border-amber-400 bg-white/10 px-3 py-2.5 text-sm font-semibold text-white"
                    : "flex min-h-11 items-center gap-3 rounded-md border-l-4 border-transparent px-3 py-2.5 text-sm font-medium text-blue-50/80"
                }
              >
                <Icon className="size-5" />
                {item.label}
              </Link>
            );
          })}
        </nav>
      </SheetContent>
    </Sheet>
  );
}
