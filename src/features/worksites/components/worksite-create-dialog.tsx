"use client";

import { useActionState, useEffect, useRef, useState } from "react";
import { CheckCircle2, Pencil, Plus } from "lucide-react";
import { useRouter } from "next/navigation";

import { saveWorkSiteAction } from "@/app/obras/actions";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { WorkSiteActionState } from "@/schemas/worksite.schema";
import type { WorkSite } from "@/types/domain";

const initialState: WorkSiteActionState = {
  message: "",
  status: "idle",
};

export function WorkSiteDialog({ workSite }: { workSite?: WorkSite }) {
  const router = useRouter();
  const formRef = useRef<HTMLFormElement>(null);
  const [open, setOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState("");
  const [state, formAction, pending] = useActionState(
    saveWorkSiteAction,
    initialState,
  );
  const isEditing = Boolean(workSite);

  useEffect(() => {
    if (state.status !== "success" || !state.submissionId) {
      return;
    }

    const closeTimeout = window.setTimeout(() => {
      setOpen(false);
      setSuccessMessage(state.message);
      formRef.current?.reset();
      router.refresh();
    }, 0);
    const messageTimeout = window.setTimeout(
      () => setSuccessMessage(""),
      5000,
    );

    return () => {
      window.clearTimeout(closeTimeout);
      window.clearTimeout(messageTimeout);
    };
  }, [router, state.message, state.status, state.submissionId]);

  return (
    <>
      <Dialog
        open={open}
        onOpenChange={(nextOpen) => {
          if (!pending) {
            setOpen(nextOpen);
          }
        }}
      >
        <DialogTrigger asChild>
          <Button variant={isEditing ? "outline" : "default"} size="sm">
            {isEditing ? <Pencil /> : <Plus />}
            {isEditing ? "Editar" : "Cadastrar obra"}
          </Button>
        </DialogTrigger>
        <DialogContent>
          <form ref={formRef} action={formAction} className="space-y-5">
            <DialogHeader>
              <DialogTitle>
                {isEditing ? "Editar obra" : "Cadastro de obra"}
              </DialogTitle>
              <DialogDescription>
                Os dados são gravados na API oficial e preservam o histórico de
                alterações.
              </DialogDescription>
            </DialogHeader>

            {workSite ? (
              <input type="hidden" name="id" value={workSite.id} />
            ) : null}

            <div className="space-y-2">
              <Label htmlFor={`worksite-name-${workSite?.id ?? "new"}`}>
                Nome
              </Label>
              <Input
                id={`worksite-name-${workSite?.id ?? "new"}`}
                name="name"
                defaultValue={workSite?.name}
                maxLength={160}
                required
                aria-invalid={Boolean(state.fieldErrors?.name)}
                aria-describedby={
                  state.fieldErrors?.name
                    ? `worksite-name-error-${workSite?.id ?? "new"}`
                    : undefined
                }
              />
              {state.fieldErrors?.name ? (
                <p
                  id={`worksite-name-error-${workSite?.id ?? "new"}`}
                  className="text-sm text-red-700"
                >
                  {state.fieldErrors.name.join(" ")}
                </p>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label htmlFor={`worksite-code-${workSite?.id ?? "new"}`}>
                Código
              </Label>
              <Input
                id={`worksite-code-${workSite?.id ?? "new"}`}
                name="code"
                defaultValue={workSite?.code}
                maxLength={40}
                required
                aria-invalid={Boolean(state.fieldErrors?.code)}
                aria-describedby={
                  state.fieldErrors?.code
                    ? `worksite-code-error-${workSite?.id ?? "new"}`
                    : undefined
                }
              />
              {state.fieldErrors?.code ? (
                <p
                  id={`worksite-code-error-${workSite?.id ?? "new"}`}
                  className="text-sm text-red-700"
                >
                  {state.fieldErrors.code.join(" ")}
                </p>
              ) : null}
            </div>

            <label className="flex items-center gap-3 rounded-md border border-slate-200 p-3 text-sm font-medium text-slate-800">
              <input
                type="checkbox"
                name="active"
                defaultChecked={workSite?.active ?? true}
                className="size-4 rounded border-slate-300 accent-[#173665]"
              />
              Obra ativa
            </label>

            {state.status === "error" ? (
              <p role="alert" className="text-sm font-medium text-red-700">
                {state.message}
              </p>
            ) : null}

            <DialogFooter>
              <DialogClose asChild>
                <Button type="button" variant="outline" disabled={pending}>
                  Cancelar
                </Button>
              </DialogClose>
              <Button type="submit" disabled={pending}>
                {pending ? "Salvando..." : "Salvar obra"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {successMessage ? (
        <div
          role="status"
          className="fixed right-4 bottom-4 z-50 flex items-center gap-2 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-900 shadow-lg"
        >
          <CheckCircle2 className="size-4" />
          {successMessage}
        </div>
      ) : null}
    </>
  );
}
