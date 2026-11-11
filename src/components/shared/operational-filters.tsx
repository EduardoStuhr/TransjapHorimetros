import type { ReadingsFilterValues } from "@/schemas/readings-filter.schema";

export function OperationalFilters({ filters }: { filters: ReadingsFilterValues }) {
  const fields = [
    ["startDate", "Período inicial", "date"], ["endDate", "Período final", "date"],
    ["fleetNumber", "Frota", "number"], ["machineId", "ID da máquina", "text"],
    ["workSiteId", "ID da obra", "text"], ["operatorId", "ID do operador", "text"],
  ] as const;
  return <form className="grid gap-3 rounded-lg border bg-white p-4 sm:grid-cols-3">
    {fields.map(([key, label, type]) => <label key={key} className="text-sm">{label}
      <input className="mt-1 block w-full rounded border p-2" name={key} type={type} defaultValue={filters[key]} />
    </label>)}
    <label className="text-sm">Status<select name="status" defaultValue={filters.status} className="mt-1 block w-full rounded border p-2">
      <option value="">Todos</option>{["VALIDATED", "PENDING", "SUSPECT", "CORRECTED", "REJECTED"].map(s => <option key={s}>{s}</option>)}
    </select></label>
    <label className="text-sm">Tipo<select name="readingType" defaultValue={filters.readingType} className="mt-1 block w-full rounded border p-2">
      <option value="">Todos</option>{["OPENING","CLOSING","INTERMEDIATE","MAINTENANCE","CORRECTION"].map(s => <option key={s}>{s}</option>)}
    </select></label>
    <label className="text-sm">Anomalias<select name="hasAnomaly" defaultValue={filters.hasAnomaly} className="mt-1 block w-full rounded border p-2">
      <option value="">Todas</option><option value="true">Com anomalia</option><option value="false">Sem anomalia</option>
    </select></label>
    <button className="rounded bg-[#173b67] p-2 text-white" type="submit">Aplicar filtros</button>
  </form>;
}
