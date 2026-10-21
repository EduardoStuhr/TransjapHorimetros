"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { CircleAlert, Gauge, LoaderCircle } from "lucide-react";

import { useAuth } from "@/components/auth/auth-provider";
import { useSync } from "@/components/offline/sync-provider";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import type { MeterUnit } from "@/types/domain";

const meterUnitManagers = new Set(["ADMIN", "MANAGER"]);

export function canConfigureMachineMeterUnit(roles: readonly string[]) {
  return roles.some((role) => meterUnitManagers.has(role));
}

interface MachineMeterUnitFormProps {
  machineId: string;
  meterUnit: MeterUnit | null;
}

async function responseError(response: Response) {
  try {
    const problem = await response.json() as { detail?: string; title?: string; errors?: Record<string, string[]> };
    return Object.values(problem.errors ?? {}).flat().join(" ")
      || problem.detail
      || problem.title
      || "Não foi possível salvar a unidade.";
  } catch {
    return "Não foi possível salvar a unidade.";
  }
}

export function MachineMeterUnitForm({ machineId, meterUnit }: MachineMeterUnitFormProps) {
  const { user } = useAuth();
  const { refreshReferenceData } = useSync();
  const router = useRouter();
  const [selectedUnit, setSelectedUnit] = useState<MeterUnit | "">(meterUnit ?? "");
  const [savedUnit, setSavedUnit] = useState<MeterUnit | null>(meterUnit);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  if (!canConfigureMachineMeterUnit(user.roles)) return null;

  async function save() {
    if (!selectedUnit || selectedUnit === savedUnit || saving) return;
    setSaving(true);
    setMessage(null);
    try {
      const response = await fetch(`/api/machines/${encodeURIComponent(machineId)}/meter-unit`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ meterUnit: selectedUnit }),
      });
      if (!response.ok) throw new Error(await responseError(response));
      setSavedUnit(selectedUnit);
      await refreshReferenceData();
      router.refresh();
      setMessage("Unidade oficial salva. O cache de campo deste dispositivo foi atualizado.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Não foi possível salvar a unidade.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="mt-5 rounded-lg border border-amber-200 bg-amber-50 p-4" aria-labelledby="meter-unit-title">
      <div className="flex gap-3">
        {savedUnit ? <Gauge className="mt-0.5 size-5 shrink-0 text-blue-800" /> : <CircleAlert className="mt-0.5 size-5 shrink-0 text-amber-800" />}
        <div className="min-w-0 flex-1">
          <h2 id="meter-unit-title" className="font-bold text-slate-950">Unidade oficial da máquina</h2>
          <p className="mt-1 text-sm text-slate-700">
            Configure apenas após confirmar no equipamento ou cadastro operacional. A alteração vale para novas coletas e não modifica o histórico.
          </p>
          <div className="mt-4 grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
            <div>
              <Label htmlFor="machine-meter-unit">Horímetro/odômetro</Label>
              <select
                id="machine-meter-unit"
                value={selectedUnit}
                onChange={(event) => setSelectedUnit(event.target.value as MeterUnit | "")}
                className="mt-2 min-h-11 w-full rounded-md border border-slate-300 bg-white px-3 text-sm text-slate-950"
              >
                <option value="">Selecione a unidade confirmada</option>
                <option value="HOURS">HOURS — horas</option>
                <option value="KM">KM — quilômetros</option>
              </select>
            </div>
            <Button type="button" className="min-h-11" onClick={() => void save()} disabled={!selectedUnit || selectedUnit === savedUnit || saving}>
              {saving ? <LoaderCircle className="animate-spin" /> : <Gauge />} {saving ? "Salvando…" : "Salvar unidade"}
            </Button>
          </div>
          {message ? <p className="mt-3 text-sm font-medium text-slate-800" role="status">{message}</p> : null}
        </div>
      </div>
    </section>
  );
}
