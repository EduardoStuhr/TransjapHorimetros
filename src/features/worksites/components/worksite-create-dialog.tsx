"use client";

import { Plus } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";

export function WorkSiteCreateDialog() {
  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          Cadastrar obra
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cadastro de obra</DialogTitle>
          <DialogDescription>
            O formulário será habilitado após a definição do contrato da API.
            Nenhuma informação será salva apenas no navegador.
          </DialogDescription>
        </DialogHeader>
        <div className="rounded-md border border-blue-200 bg-blue-50 p-4 text-sm leading-6 text-blue-950">
          Esta etapa preserva a API como fonte oficial e evita criar cadastros
          locais sem auditoria.
        </div>
        <DialogFooter>
          <Button type="button" disabled>
            Aguardando API
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
