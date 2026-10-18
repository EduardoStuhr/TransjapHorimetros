"use client";

import { useTransition } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Filter, RotateCcw } from "lucide-react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  emptyReadingsFilters,
  readingsFilterSchema,
  type ReadingsFilterValues,
} from "@/schemas/readings-filter.schema";
import type { Machine, WorkSite } from "@/types/domain";

const fieldClassName =
  "flex h-9 min-w-0 w-full rounded-md border border-input bg-white px-3 py-1 text-sm shadow-xs outline-none transition-[color,box-shadow] focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

export function ReadingsFilters({
  filters,
  machines,
  workSites,
}: {
  filters: ReadingsFilterValues;
  machines: readonly Machine[];
  workSites: readonly WorkSite[];
}) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ReadingsFilterValues>({
    resolver: zodResolver(readingsFilterSchema),
    defaultValues: filters,
  });

  function applyFilters(values: ReadingsFilterValues) {
    const searchParams = new URLSearchParams();

    Object.entries(values).forEach(([key, value]) => {
      if (value) {
        searchParams.set(key, value);
      }
    });

    startTransition(() => {
      router.push(
        searchParams.size > 0
          ? `/horimetros?${searchParams.toString()}`
          : "/horimetros",
      );
    });
  }

  function clearFilters() {
    reset(emptyReadingsFilters);
    startTransition(() => router.push("/horimetros"));
  }

  return (
    <form
      onSubmit={handleSubmit(applyFilters)}
      className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
    >
      <input type="hidden" {...register("machineId")} />
      <div className="grid min-w-0 gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <div className="space-y-2">
          <Label htmlFor="startDate">Período inicial</Label>
          <Input id="startDate" type="date" {...register("startDate")} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="endDate">Período final</Label>
          <Input
            id="endDate"
            type="date"
            aria-invalid={Boolean(errors.endDate)}
            {...register("endDate")}
          />
          {errors.endDate ? (
            <p className="text-xs text-red-700">{errors.endDate.message}</p>
          ) : null}
        </div>
        <div className="space-y-2">
          <Label htmlFor="fleetNumber">Frota</Label>
          <select
            id="fleetNumber"
            className={fieldClassName}
            {...register("fleetNumber")}
          >
            <option value="">Todas as frotas</option>
            {machines.map((machine) => (
              <option key={machine.fleetNumber} value={machine.fleetNumber}>
                {machine.fleetNumber} · {machine.model}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="workSiteId">Obra</Label>
          <select
            id="workSiteId"
            className={fieldClassName}
            {...register("workSiteId")}
          >
            <option value="">Todas as obras</option>
            {workSites.map((workSite) => (
              <option key={workSite.id} value={workSite.id}>
                {workSite.name}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="status">Status</Label>
          <select id="status" className={fieldClassName} {...register("status")}>
            <option value="">Todos os status</option>
            <option value="VALIDATED">Validado</option>
            <option value="PENDING">Pendente</option>
            <option value="SUSPECT">Suspeito</option>
            <option value="CORRECTED">Corrigido</option>
            <option value="REJECTED">Rejeitado</option>
          </select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="meterUnit">Unidade</Label>
          <select id="meterUnit" className={fieldClassName} {...register("meterUnit")}>
            <option value="">Todas as unidades</option><option value="HOURS">Horas (HOURS)</option><option value="KM">Quilômetros (KM)</option>
          </select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="readingType">Tipo de leitura</Label>
          <select id="readingType" className={fieldClassName} {...register("readingType")}>
            <option value="">Todos os tipos</option><option value="OPENING">Abertura</option><option value="CLOSING">Fechamento</option><option value="INTERMEDIATE">Intermediária</option><option value="MAINTENANCE">Manutenção</option><option value="CORRECTION">Proposta de correção</option>
          </select>
        </div>
        {([ ["hasPhotoEvidence", "Evidência fotográfica"], ["hasAnomaly", "Anomalias"] ] as const).map(([name, label]) => (
          <div key={name} className="space-y-2"><Label htmlFor={name}>{label}</Label>
            <select id={name} className={fieldClassName} {...register(name)}><option value="">Todas</option><option value="true">Com registro</option><option value="false">Sem registro</option></select>
          </div>
        ))}
        <div className="flex items-end gap-2 sm:col-span-2 xl:col-span-5 xl:justify-end">
          <Button type="submit" disabled={pending}>
            <Filter />
            {pending ? "Aplicando..." : "Aplicar filtros"}
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={clearFilters}
            disabled={pending}
          >
            <RotateCcw />
            Limpar
          </Button>
        </div>
      </div>
      <p aria-live="polite" className="mt-3 min-h-5 text-sm text-slate-600">
        {pending ? "Atualizando resultados..." : ""}
      </p>
    </form>
  );
}
