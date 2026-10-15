import Link from "next/link";
import { ShieldX } from "lucide-react";

import { Button } from "@/components/ui/button";

export default function AccessDeniedPage() {
  return (
    <div className="flex min-h-[65vh] items-center justify-center">
      <div className="max-w-md text-center">
        <div className="mx-auto flex size-14 items-center justify-center rounded-lg bg-amber-100 text-amber-800">
          <ShieldX className="size-7" />
        </div>
        <h2 className="mt-5 text-2xl font-bold text-slate-950">Acesso nao autorizado</h2>
        <p className="mt-2 text-slate-600">Seu perfil nao possui permissao para acessar este recurso. A API manteve a operacao protegida.</p>
        <Button asChild className="mt-6">
          <Link href="/frota">Ir para a frota</Link>
        </Button>
      </div>
    </div>
  );
}
