import type { LucideIcon } from "lucide-react";

import { Card, CardContent } from "@/components/ui/card";

interface MetricCardProps {
  label: string;
  value: number;
  helper: string;
  icon: LucideIcon;
  emphasized?: boolean;
}

export function MetricCard({
  label,
  value,
  helper,
  icon: Icon,
  emphasized = false,
}: MetricCardProps) {
  return (
    <Card className="gap-0 overflow-hidden border-slate-200 py-0 shadow-sm">
      <div
        className={
          emphasized ? "h-1 bg-amber-400" : "h-1 bg-[#2c5b9e]"
        }
      />
      <CardContent className="flex items-start justify-between gap-4 p-5">
        <div>
          <p className="text-sm font-semibold text-slate-600">{label}</p>
          <p className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
            {value}
          </p>
          <p className="mt-1 text-xs text-slate-500">{helper}</p>
        </div>
        <div
          className={
            emphasized
              ? "flex size-10 items-center justify-center rounded-md bg-amber-100 text-amber-800"
              : "flex size-10 items-center justify-center rounded-md bg-blue-50 text-[#2c5b9e]"
          }
        >
          <Icon className="size-5" aria-hidden="true" />
        </div>
      </CardContent>
    </Card>
  );
}
