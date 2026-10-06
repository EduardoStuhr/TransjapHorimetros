"use client";

import { CircleAlert, RotateCcw } from "lucide-react";

import { Button } from "@/components/ui/button";

export default function ErrorPage({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <div className="max-w-md text-center">
        <div className="mx-auto flex size-12 items-center justify-center rounded-md bg-red-50 text-red-700">
          <CircleAlert />
        </div>
        <h2 className="mt-5 text-xl font-bold text-slate-950">
          Não foi possível conectar à API.
        </h2>
        <p className="mt-2 text-slate-600">
          Verifique se a API e o PostgreSQL estão em execução e tente novamente.
        </p>
        <Button onClick={reset} className="mt-5">
          <RotateCcw />
          Tentar novamente
        </Button>
      </div>
    </div>
  );
}
