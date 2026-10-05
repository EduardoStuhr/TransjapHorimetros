import { Info } from "lucide-react";

export function DataSourceNotice({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-start gap-3 rounded-md border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-950">
      <Info className="mt-0.5 size-4 shrink-0 text-[#2c5b9e]" />
      <p>{children}</p>
    </div>
  );
}
