"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import {
  type ColumnDef,
  type ColumnFiltersState,
  type PaginationState,
  type SortingState,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from "@tanstack/react-table";
import {
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  Search,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  formatDateTime,
  formatHourMeter,
  formatMachineRegistrationStatus,
  formatQrCodeStatus,
} from "@/lib/domain-formatters";
import type { Machine } from "@/types/domain";

const columns: ColumnDef<Machine>[] = [
  {
    accessorKey: "fleetNumber",
    header: ({ column }) => (
      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="-ml-3 font-bold"
        onClick={() => column.toggleSorting(column.getIsSorted() === "asc")}
      >
        Frota
        <ArrowUpDown />
      </Button>
    ),
    cell: ({ row }) => (
      <Link
        href={"/frota/" + row.original.fleetNumber}
        className="font-bold text-[#234f8c] underline-offset-4 hover:underline"
      >
        {row.original.fleetNumber}
      </Link>
    ),
  },
  {
    accessorKey: "model",
    header: ({ column }) => (
      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="-ml-3 font-bold"
        onClick={() => column.toggleSorting(column.getIsSorted() === "asc")}
      >
        Modelo
        <ArrowUpDown />
      </Button>
    ),
    cell: ({ row }) => (
      <span className="min-w-52 font-medium text-slate-800">
        {row.original.model}
      </span>
    ),
  },
  {
    id: "status",
    header: "Status",
    cell: ({ row }) => (
      <span className="text-slate-500">
        {formatMachineRegistrationStatus(row.original.registrationStatus)}
      </span>
    ),
  },
  {
    id: "lastHourMeter",
    header: "Último horímetro",
    cell: ({ row }) => (
      <span className="text-slate-500">
        {formatHourMeter(row.original.currentHourMeter)}
      </span>
    ),
  },
  {
    id: "lastReading",
    header: "Última leitura",
    cell: ({ row }) => (
      <span className="text-slate-500">
        {formatDateTime(row.original.lastReadingAt)}
      </span>
    ),
  },
  {
    id: "workSite",
    header: "Obra",
    cell: ({ row }) => (
      <span className="text-slate-500">
        {row.original.workSiteName ?? "Sem registro"}
      </span>
    ),
  },
  {
    id: "qrCode",
    header: "QR Code",
    cell: ({ row }) => (
      <Badge variant="outline" className="font-medium text-slate-600">
        {formatQrCodeStatus(row.original.qrCodeStatus)}
      </Badge>
    ),
  },
  {
    id: "situation",
    header: "Situação",
    cell: ({ row }) => (
      <Badge variant="secondary" className="font-medium">
        {row.original.operationalStatus ?? "Sem registro"}
      </Badge>
    ),
  },
  {
    id: "actions",
    header: () => <span className="sr-only">Ações</span>,
    cell: ({ row }) => (
      <div className="flex justify-end">
        <Button asChild variant="ghost" size="icon-sm">
          <Link
            href={"/frota/" + row.original.fleetNumber}
            aria-label={"Abrir frota " + row.original.fleetNumber}
          >
            <ExternalLink />
          </Link>
        </Button>
      </div>
    ),
    enableSorting: false,
  },
];

export function FleetTable({ data }: { data: readonly Machine[] }) {
  const [globalFilter, setGlobalFilter] = useState("");
  const [sorting, setSorting] = useState<SortingState>([
    { id: "fleetNumber", desc: false },
  ]);
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });

  const models = useMemo(
    () => [...new Set(data.map((machine) => machine.model))].sort(),
    [data],
  );

  const table = useReactTable({
    data: [...data],
    columns,
    state: {
      globalFilter,
      sorting,
      columnFilters,
      pagination,
    },
    onGlobalFilterChange: setGlobalFilter,
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    onPaginationChange: setPagination,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
  });

  const filteredCount = table.getFilteredRowModel().rows.length;
  const start = filteredCount === 0 ? 0 : pagination.pageIndex * pagination.pageSize + 1;
  const end = Math.min(
    (pagination.pageIndex + 1) * pagination.pageSize,
    filteredCount,
  );

  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="flex flex-col gap-3 border-b border-slate-200 bg-slate-50/70 p-4 md:flex-row md:items-center md:justify-between">
        <div className="relative w-full md:max-w-md">
          <Search
            className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-slate-400"
            aria-hidden="true"
          />
          <Input
            value={globalFilter}
            onChange={(event) => {
              setGlobalFilter(event.target.value);
              table.setPageIndex(0);
            }}
            placeholder="Buscar por frota ou modelo"
            aria-label="Buscar na frota"
            className="bg-white pl-9"
          />
        </div>

        <Select
          value={(table.getColumn("model")?.getFilterValue() as string) || "all"}
          onValueChange={(value) => {
            table
              .getColumn("model")
              ?.setFilterValue(value === "all" ? undefined : value);
            table.setPageIndex(0);
          }}
        >
          <SelectTrigger className="w-full bg-white md:w-72">
            <SelectValue placeholder="Todos os modelos" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Todos os modelos</SelectItem>
            {models.map((model) => (
              <SelectItem key={model} value={model}>
                {model}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="max-h-[620px] overflow-auto">
        <Table>
          <TableHeader className="sticky top-0 z-10 bg-slate-100">
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead
                    key={header.id}
                    className="h-12 whitespace-nowrap text-xs font-bold uppercase tracking-wide text-slate-600"
                  >
                    {header.isPlaceholder
                      ? null
                      : flexRender(
                          header.column.columnDef.header,
                          header.getContext(),
                        )}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows.length ? (
              table.getRowModel().rows.map((row) => (
                <TableRow
                  key={row.id}
                  className="h-14 border-slate-100 hover:bg-blue-50/50"
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} className="whitespace-nowrap">
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-40 text-center text-slate-600"
                >
                  Nenhuma máquina encontrada para os filtros aplicados.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <div className="flex flex-col gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-600 sm:flex-row sm:items-center sm:justify-between">
        <p>
          Mostrando {start}–{end} de {filteredCount} máquinas
        </p>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => table.previousPage()}
            disabled={!table.getCanPreviousPage()}
          >
            <ChevronLeft />
            Anterior
          </Button>
          <span className="min-w-20 text-center text-xs font-semibold">
            Página {table.getState().pagination.pageIndex + 1} de{" "}
            {Math.max(table.getPageCount(), 1)}
          </span>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => table.nextPage()}
            disabled={!table.getCanNextPage()}
          >
            Próxima
            <ChevronRight />
          </Button>
        </div>
      </div>
    </div>
  );
}
