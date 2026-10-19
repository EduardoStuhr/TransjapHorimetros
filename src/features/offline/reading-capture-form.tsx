"use client";

import { type FormEvent, useMemo, useState } from "react";
import { Camera, CheckCircle2, LoaderCircle, QrCode, Save } from "lucide-react";

import { useAuth } from "@/components/auth/auth-provider";
import { useSync } from "@/components/offline/sync-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getOfflineStore } from "@/offline/indexed-db-store";
import { sha256Hex } from "@/offline/crypto";
import type { OfflinePhotoPayload, OfflineReadingPayload } from "@/offline/types";

const maximumFileSizeBytes = 10 * 1024 * 1024;
const allowedTypes = new Set<OfflinePhotoPayload["mimeType"]>([
  "image/jpeg",
  "image/png",
  "image/webp",
]);

function localDateTimeValue() {
  const now = new Date();
  const offset = now.getTimezoneOffset() * 60_000;
  return new Date(now.getTime() - offset).toISOString().slice(0, 16);
}

function formatCachedAt(value: string | null) {
  return value
    ? new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value))
    : "cache ainda não preparado";
}

export function ReadingCaptureForm() {
  const { user } = useAuth();
  const {
    connectivity,
    machines,
    workSites,
    referenceDataFetchedAt,
    refresh,
    refreshReferenceData,
    syncNow,
  } = useSync();
  const store = useMemo(() => getOfflineStore(), []);
  const [selectedMachineId, setSelectedMachineId] = useState("");
  const [qrValue, setQrValue] = useState("");
  const [capturedAt, setCapturedAt] = useState(localDateTimeValue);
  const [resolvingQr, setResolvingQr] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const selectedMachine = machines.find((machine) => machine.id === selectedMachineId);

  async function resolveQr() {
    const value = qrValue.trim();
    if (!value) {
      setMessage("Leia ou informe o conteúdo do QR code.");
      return;
    }
    setResolvingQr(true);
    setMessage(null);
    try {
      let machine = await store.resolveCachedMachine(value);
      if (!machine && navigator.onLine) {
        const response = await fetch(`/api/qr-codes/resolve?value=${encodeURIComponent(value)}`, { cache: "no-store" });
        if (response.ok) {
          const resolved = await response.json() as { machineId: string };
          await refreshReferenceData();
          machine = (await store.getCachedMachines()).find((item) => item.id === resolved.machineId);
        }
      }
      if (!machine) {
        setMessage(
          connectivity === "OFFLINE"
            ? "QR não encontrado no cache deste dispositivo. Conecte-se para atualizar as máquinas."
            : "Não foi possível localizar uma máquina ativa para este QR code.",
        );
        return;
      }
      setSelectedMachineId(machine.id);
      setMessage(`Frota ${machine.fleetNumber} identificada pelo ${connectivity === "OFFLINE" ? "cache local" : "cadastro disponível"}.`);
    } finally {
      setResolvingQr(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const formData = new FormData(form);
    const file = formData.get("photo");
    const rawValue = String(formData.get("value") ?? "").trim();
    const value = Number(rawValue.replace(",", "."));
    const workSiteId = String(formData.get("workSiteId") ?? "");
    const readingType = String(formData.get("readingType") ?? "") as OfflineReadingPayload["readingType"];

    if (!selectedMachine || selectedMachine.status !== "ACTIVE") {
      setMessage("Selecione uma máquina ativa do cache confiável.");
      return;
    }
    if (!selectedMachine.meterUnit) {
      setMessage("A unidade do medidor desta máquina ainda não foi confirmada.");
      return;
    }
    if (!workSiteId || !workSites.some((workSite) => workSite.id === workSiteId && workSite.active)) {
      setMessage("Selecione uma obra ativa.");
      return;
    }
    if (!/^\d{1,10}(?:[.,]\d{1,2})?$/.test(rawValue) || !Number.isFinite(value) || value > 9_999_999_999.99) {
      setMessage("Informe um valor positivo com no máximo duas casas decimais.");
      return;
    }
    if (!(file instanceof File) || file.size === 0 || !allowedTypes.has(file.type as OfflinePhotoPayload["mimeType"])) {
      setMessage("Selecione uma foto JPEG, PNG ou WEBP.");
      return;
    }
    if (file.size > maximumFileSizeBytes) {
      setMessage("A foto deve possuir no máximo 10 MB.");
      return;
    }
    const capturedDate = new Date(capturedAt);
    if (Number.isNaN(capturedDate.getTime())) {
      setMessage("Informe a data e hora de captura.");
      return;
    }

    setSaving(true);
    setMessage(null);
    try {
      const deviceId = await store.getOrCreateDeviceId();
      const photoSha256 = await sha256Hex(file);
      await store.enqueueReadingWithPhoto({
        reading: {
          machineId: selectedMachine.id,
          workSiteId,
          value,
          readingType,
          capturedAtDevice: capturedDate.toISOString(),
          deviceId,
        },
        photo: file,
        photoFileName: file.name || "evidencia.jpg",
        photoMimeType: file.type as OfflinePhotoPayload["mimeType"],
        photoSha256,
        user,
      });
      await refresh();
      setMessage(
        connectivity === "OFFLINE"
          ? "Sem conexão. A leitura e a foto foram salvas neste dispositivo e serão enviadas quando a internet voltar."
          : "Leitura e foto salvas neste dispositivo. Iniciando sincronização.",
      );
      form.reset();
      setCapturedAt(localDateTimeValue());
      setQrValue("");
      setSelectedMachineId("");
      const result = await syncNow(false);
      if (result.synced === 2) setMessage("Sincronização concluída. Leitura e foto confirmadas pelo servidor.");
    } catch {
      setMessage("Não foi possível salvar no dispositivo. Verifique o espaço disponível e tente novamente.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
      <form onSubmit={handleSubmit} className="rounded-lg border bg-white p-5 shadow-sm sm:p-6">
        <div className="grid gap-5 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Label htmlFor="qr-value">QR code da máquina</Label>
            <div className="mt-2 flex gap-2">
              <Input
                id="qr-value"
                value={qrValue}
                onChange={(event) => setQrValue(event.target.value)}
                placeholder="Leia o QR ou informe FROTA 1234"
                autoComplete="off"
              />
              <Button type="button" variant="outline" onClick={() => void resolveQr()} disabled={resolvingQr}>
                {resolvingQr ? <LoaderCircle className="animate-spin" /> : <QrCode />}
                Resolver
              </Button>
            </div>
          </div>

          <div className="sm:col-span-2">
            <Label htmlFor="machineId">Máquina</Label>
            <select
              id="machineId"
              value={selectedMachineId}
              onChange={(event) => setSelectedMachineId(event.target.value)}
              required
              className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm"
            >
              <option value="">Selecione uma máquina do cache</option>
              {machines.map((machine) => (
                <option key={machine.id} value={machine.id}>
                  Frota {machine.fleetNumber} · {machine.model} · {machine.meterUnit ?? "unidade pendente"}
                </option>
              ))}
            </select>
          </div>

          <div>
            <Label htmlFor="value">Leitura {selectedMachine?.meterUnit ? `(${selectedMachine.meterUnit})` : ""}</Label>
            <Input id="value" name="value" type="text" inputMode="decimal" placeholder="0,00" required className="mt-2" />
          </div>
          <div>
            <Label htmlFor="readingType">Tipo</Label>
            <select id="readingType" name="readingType" required className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm">
              <option value="INTERMEDIATE">Intermediária</option>
              <option value="OPENING">Abertura</option>
              <option value="CLOSING">Fechamento</option>
              <option value="MAINTENANCE">Manutenção</option>
            </select>
          </div>
          <div>
            <Label htmlFor="workSiteId">Obra</Label>
            <select id="workSiteId" name="workSiteId" required className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm">
              <option value="">Selecione uma obra</option>
              {workSites.map((workSite) => <option key={workSite.id} value={workSite.id}>{workSite.name}</option>)}
            </select>
          </div>
          <div>
            <Label htmlFor="capturedAtDevice">Capturada no dispositivo em</Label>
            <Input
              id="capturedAtDevice"
              type="datetime-local"
              value={capturedAt}
              onChange={(event) => setCapturedAt(event.target.value)}
              required
              className="mt-2"
            />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="photo">Evidência fotográfica</Label>
            <Input id="photo" name="photo" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" required className="mt-2" />
            <p className="mt-1 text-xs text-slate-500">JPEG, PNG ou WEBP, até 10 MB. A foto fica no IndexedDB até a confirmação do servidor.</p>
          </div>
        </div>

        <div className="mt-6 flex flex-wrap items-center gap-3">
          <Button type="submit" disabled={saving || machines.length === 0 || workSites.length === 0}>
            {saving ? <LoaderCircle className="animate-spin" /> : <Save />}
            {saving ? "Salvando no dispositivo..." : "Confirmar leitura e foto"}
          </Button>
          {message && <p className="max-w-2xl text-sm text-slate-700" aria-live="polite">{message}</p>}
        </div>
      </form>

      <aside className="space-y-4">
        <div className="rounded-lg border bg-slate-50 p-5">
          <div className="flex items-center gap-2 font-semibold text-slate-900"><CheckCircle2 className="size-5 text-emerald-700" /> Pronto para campo</div>
          <dl className="mt-4 space-y-3 text-sm">
            <div><dt className="text-slate-500">Conexão</dt><dd className="font-semibold">{connectivity === "OFFLINE" ? "Offline · salvamento local ativo" : "Online ou em verificação"}</dd></div>
            <div><dt className="text-slate-500">Máquinas em cache</dt><dd className="font-semibold">{machines.length}</dd></div>
            <div><dt className="text-slate-500">Obras em cache</dt><dd className="font-semibold">{workSites.length}</dd></div>
            <div><dt className="text-slate-500">Cache atualizado</dt><dd className="font-semibold">{formatCachedAt(referenceDataFetchedAt)}</dd></div>
          </dl>
          <Button type="button" variant="outline" className="mt-4 w-full" onClick={() => void refreshReferenceData()}>
            Atualizar dados de campo
          </Button>
        </div>
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-5 text-sm text-amber-950">
          <div className="flex gap-2 font-semibold"><Camera className="size-5" /> A foto é a prova</div>
          <p className="mt-2">O texto é a leitura e o servidor é a verdade. Os cálculos e as regras operacionais serão refeitos na sincronização.</p>
        </div>
      </aside>
    </div>
  );
}
