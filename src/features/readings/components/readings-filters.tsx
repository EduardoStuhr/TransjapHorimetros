"use client";

import { useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Filter, RotateCcw } from "lucide-react";
import { useForm } from "react-hook-form";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  readingsFilterSchema,
  type ReadingsFilterValues,
} from "@/schemas/readings-filter.schema";
import type { Machine } from "@/types/domain";

const fieldClassName =
  "flex h-9 min-w-0 w-full rounded-md border border-input bg-white px-3 py-1 text-sm shadow-xs outline-none transition-[color,box-shadow] focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

export function ReadingsFilters({
  machines,
}: {
  machines: readonly Machine[];
}) {
  const [feedback, setFeedback] = useState("");
  const models = useMemo(
    () => [...new Set(machines.map((machine) => machine.model))].sort(),
    [machines],
  );

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ReadingsFilterValues>({
    resolver: zodResolver(readingsFilterSchema),
    defaultValues: {
      startDate: "",
      endDate: "",
      fleetNumber: "",
      model: "",
      workSite: "",
      status: "",
    },
  });

  function applyFilters() {
    setFeedback("Filtros aplicados. Nenhum registro está disponível.");
  }

  function clearFilters() {
    reset();
    setFeedback("");
  }

  return (
    <form
      onSubmit={handleSubmit(applyFilters)}
      className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
    >
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
                {machine.fleetNumber}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="model">Modelo</Label>
          <select id="model" className={fieldClassName} {...register("model")}>
            <option value="">Todos os modelos</option>
            {models.map((model) => (
              <option key={model} value={model}>
                {model}
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
        <div className="space-y-2 sm:col-span-2 xl:col-span-2">
          <Label htmlFor="workSite">Obra</Label>
          <select
            id="workSite"
            className={fieldClassName}
            disabled
            {...register("workSite")}
          >
            <option value="">Nenhuma obra cadastrada</option>
          </select>
        </div>
        <div className="flex items-end gap-2 sm:col-span-2 xl:col-span-3 xl:justify-end">
          <Button type="submit">
            <Filter />
            Aplicar filtros
          </Button>
          <Button type="button" variant="outline" onClick={clearFilters}>
            <RotateCcw />
            Limpar
          </Button>
        </div>
      </div>
      <p aria-live="polite" className="mt-3 min-h-5 text-sm text-slate-600">
        {feedback}
      </p>
    </form>
  );
}
