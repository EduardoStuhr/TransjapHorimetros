import type { LucideIcon } from "lucide-react";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
  compact?: boolean;
}

export function EmptyState({
  icon: Icon,
  title,
  description,
  compact = false,
}: EmptyStateProps) {
  return (
    <div
      className={
        compact
          ? "flex min-h-44 flex-col items-center justify-center px-5 py-8 text-center"
          : "flex min-h-64 flex-col items-center justify-center px-5 py-12 text-center"
      }
    >
      <div className="mb-4 flex size-11 items-center justify-center rounded-md border border-slate-200 bg-slate-50 text-[#2c5b9e]">
        <Icon className="size-5" aria-hidden="true" />
      </div>
      <h3 className="text-base font-bold text-slate-900">{title}</h3>
      <p className="mt-2 max-w-md text-sm leading-6 text-slate-600">
        {description}
      </p>
    </div>
  );
}
