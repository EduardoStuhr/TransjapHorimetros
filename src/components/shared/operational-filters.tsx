import { getMachines } from "@/services/machines.service";
import { getWorkSites } from "@/services/worksites.service";
import type { ReadingsFilterValues } from "@/schemas/readings-filter.schema";

export async function OperationalFilters({
  filters,
}: {
  filters: ReadingsFilterValues;
}) {
  const [machines, worksites] = await Promise.all([
    getMachines(),
    getWorkSites(),
  ]);
  const fields = [
    ["startDate", "Período inicial", "date"],
    ["endDate", "Período final", "date"],
    ["fleetNumber", "Frota", "number"],
    ["operatorId", "ID do operador", "text"],
  ] as const;
  return (
    <form className="grid gap-3 rounded-lg border bg-white p-4 sm:grid-cols-3">
      {fields.map(([key, label, type]) => (
        <label key={key} className="text-sm">
          {label}
          <input
            className="mt-1 block w-full rounded border p-2"
            name={key}
            type={type}
            defaultValue={filters[key]}
          />
        </label>
      ))}
      <label className="text-sm">Revisão
        <select className="mt-1 block w-full rounded border p-2" name="reviewed" defaultValue={filters.reviewed}>
          <option value="">Todas</option><option value="true">Com decisão</option><option value="false">Sem decisão</option>
        </select>
      </label>
      <label className="text-sm">OCR
        <select className="mt-1 block w-full rounded border p-2" name="ocrStatus" defaultValue={filters.ocrStatus}>
          <option value="">Todos</option>
          {["PENDING", "PROCESSING", "SUCCEEDED", "LOW_CONFIDENCE", "NO_VALUE_DETECTED", "FAILED"].map(s => <option key={s}>{s}</option>)}
        </select>
      </label>
      <label className="text-sm">
        Máquina
        <select
          name="machineId"
          defaultValue={filters.machineId}
          className="mt-1 block w-full rounded border p-2"
        >
          <option value="">Todas</option>
          {machines.map((m) => (
            <option key={m.id} value={m.id}>
              {m.fleetNumber} · {m.model}
            </option>
          ))}
        </select>
      </label>
      <label className="text-sm">
        Obra
        <select
          name="workSiteId"
          defaultValue={filters.workSiteId}
          className="mt-1 block w-full rounded border p-2"
        >
          <option value="">Todas</option>
          {worksites.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
            </option>
          ))}
        </select>
      </label>
      <label className="text-sm">
        Status
        <select
          name="status"
          defaultValue={filters.status}
          className="mt-1 block w-full rounded border p-2"
        >
          <option value="">Todos</option>
          {["VALIDATED", "PENDING", "SUSPECT", "CORRECTED", "REJECTED"].map(
            (s) => (
              <option key={s}>{s}</option>
            ),
          )}
        </select>
      </label>
      <label className="text-sm">
        Tipo
        <select
          name="readingType"
          defaultValue={filters.readingType}
          className="mt-1 block w-full rounded border p-2"
        >
          <option value="">Todos</option>
          {[
            "OPENING",
            "CLOSING",
            "INTERMEDIATE",
            "MAINTENANCE",
            "CORRECTION",
          ].map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
      </label>
      <label className="text-sm">
        Anomalias
        <select
          name="hasAnomaly"
          defaultValue={filters.hasAnomaly}
          className="mt-1 block w-full rounded border p-2"
        >
          <option value="">Todas</option>
          <option value="true">Com anomalia</option>
          <option value="false">Sem anomalia</option>
        </select>
      </label>
      <button className="rounded bg-[#173b67] p-2 text-white" type="submit">
        Aplicar filtros
      </button>
    </form>
  );
}
