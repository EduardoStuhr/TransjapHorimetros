import Link from "next/link";
import { SearchX } from "lucide-react";

import { Button } from "@/components/ui/button";

export default function NotFound() {
  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <div className="max-w-md text-center">
        <div className="mx-auto flex size-12 items-center justify-center rounded-md bg-slate-100 text-slate-600">
          <SearchX />
        </div>
        <h2 className="mt-5 text-xl font-bold text-slate-950">
          Página não encontrada
        </h2>
        <p className="mt-2 text-slate-600">
          O endereço informado não corresponde a uma página ou máquina
          cadastrada.
        </p>
        <Button asChild className="mt-5">
          <Link href="/">Voltar ao Dashboard</Link>
        </Button>
      </div>
    </div>
  );
}
