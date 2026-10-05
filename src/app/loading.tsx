import { Skeleton } from "@/components/ui/skeleton";

export default function Loading() {
  return (
    <div className="space-y-6" aria-label="Carregando conteúdo">
      <div className="space-y-3 border-b border-slate-200 pb-5">
        <Skeleton className="h-3 w-28" />
        <Skeleton className="h-9 w-72 max-w-full" />
        <Skeleton className="h-5 w-[520px] max-w-full" />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {Array.from({ length: 6 }, (_, index) => (
          <Skeleton key={index} className="h-32 rounded-lg" />
        ))}
      </div>
      <Skeleton className="h-80 rounded-lg" />
    </div>
  );
}
