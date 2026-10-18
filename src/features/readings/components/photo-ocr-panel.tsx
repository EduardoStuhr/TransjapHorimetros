"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/auth-provider";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { formatDateTime } from "@/lib/domain-formatters";
import type { PhotoOcrResult } from "@/types/domain";

const labels: Record<PhotoOcrResult["status"], string> = {
  PENDING: "Aguardando processamento", PROCESSING: "Processando",
  SUCCEEDED: "Valor detectado", LOW_CONFIDENCE: "Baixa confiança",
  NO_VALUE_DETECTED: "Não foi possível identificar valor", FAILED: "Erro no processamento",
};
const permittedRoles = new Set(["SUPERVISOR", "ADMINISTRATIVE", "MANAGER", "ADMIN"]);
const number = (value: number | null | undefined) => value == null ? "Não disponível" : new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 2 }).format(value);

export function PhotoOcrPanel({ readingId, operatorValue, result, loadError }: {
  readingId: string; operatorValue: number; result: PhotoOcrResult | null; loadError: boolean;
}) {
  const { user } = useAuth();
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const inFlight = useRef(false);
  const canReprocess = user.roles.some(role => permittedRoles.has(role));
  async function reprocess() {
    if (inFlight.current) return;
    inFlight.current = true;
    setBusy(true); setMessage(null);
    try {
      const response = await fetch(`/api/readings/${encodeURIComponent(readingId)}/photo/ocr/reprocess`, { method: "POST" });
      if (response.status === 401) { router.push("/login?reason=session-expired"); return; }
      if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string } | null;
        setMessage(problem?.detail ?? "Não foi possível reprocessar OCR.");
        return;
      }
      setOpen(false); setMessage("Processamento registrado. O valor informado foi preservado.");
      router.refresh();
    } catch { setMessage("Falha de conexão. Atualize o resultado antes de tentar novamente."); }
    finally { inFlight.current = false; setBusy(false); }
  }
  return <section className="mt-6 space-y-4 rounded-lg border border-slate-200 p-5" aria-label="Leitura automática da imagem">
    <h2 className="font-semibold text-slate-900">Leitura automática da imagem</h2>
    <p className="text-sm text-slate-600">O OCR é auxiliar e pode errar. A leitura informada pelo operador permanece registrada; divergências exigem revisão humana.</p>
    <p role="status" className="font-medium">{result ? labels[result.status] : loadError ? "Resultado OCR temporariamente indisponível" : "Sem processamento OCR registrado"}</p>
    {result?.matchesOperatorValue === false && <p role="alert" className="rounded bg-amber-50 p-3 text-amber-900">Valor detectado na imagem difere da leitura informada.</p>}
    <dl className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
      {[
        ["Valor informado pelo operador", number(operatorValue)],
        ["Valor detectado na imagem", number(result?.detectedValue)],
        ["Confiança", result?.confidence == null ? "Não disponível" : `${number(result.confidence * 100)}%`],
        ["Diferença absoluta", number(result?.absoluteDifference)],
        ["Diferença percentual", result?.percentageDifference == null ? "Não disponível" : `${number(result.percentageDifference)}%`],
        ["Provider", result?.provider ?? "Não disponível"],
        ["Processado em", formatDateTime(result?.processedAtServer ?? null)],
      ].map(([label, value]) => <div key={label}><dt className="text-slate-500">{label}</dt><dd className="mt-1 font-semibold">{value}</dd></div>)}
    </dl>
    {result?.errorMessage && <p className="text-sm text-slate-600">{result.errorMessage}</p>}
    <div className="flex flex-wrap gap-3">
      <Button variant="outline" onClick={() => router.refresh()} disabled={busy}>Atualizar resultado</Button>
      {canReprocess && <Dialog open={open} onOpenChange={value => { if (!busy) setOpen(value); }}>
        <DialogTrigger asChild><Button disabled={busy}>Reprocessar OCR</Button></DialogTrigger>
        <DialogContent><DialogHeader><DialogTitle>Reprocessar OCR?</DialogTitle><DialogDescription>Uma nova análise será registrada no histórico e pode gerar custo. A foto e a leitura do operador serão preservadas. Há um intervalo mínimo entre tentativas.</DialogDescription></DialogHeader>
          <Button onClick={reprocess} disabled={busy}>{busy ? "Processando..." : "Confirmar reprocessamento"}</Button>
          {message && <p role="alert">{message}</p>}
        </DialogContent>
      </Dialog>}
    </div>
    {message && <p role="status" className="text-sm">{message}</p>}
  </section>;
}
