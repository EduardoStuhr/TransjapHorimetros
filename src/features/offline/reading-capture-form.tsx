"use client";

import Image from "next/image";
import Link from "next/link";
import {
  type ChangeEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  ArrowLeft,
  ArrowRight,
  Building2,
  Camera,
  Check,
  CheckCircle2,
  CircleAlert,
  Clock3,
  Gauge,
  HardHat,
  LoaderCircle,
  QrCode,
  RefreshCw,
  RotateCcw,
  Save,
  Wifi,
  WifiOff,
} from "lucide-react";

import { useAuth } from "@/components/auth/auth-provider";
import { useDeviceIdentity } from "@/features/device-identity/device-identity-provider";
import { useSync } from "@/components/offline/sync-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { QrCamera } from "@/features/mobile-reading/qr-camera";
import { resolveMachineQr } from "@/features/mobile-reading/qr-resolution";
import {
  cacheAgeState,
  canTransitionReadingStep,
  isReadingBelowPrevious,
  localDateTimeValue,
  meterUnitLabel,
  MISSING_METER_UNIT_MESSAGE,
  parseReadingValue,
  READING_TYPE_OPTIONS,
  tryAcquireSubmissionLock,
  type MobileReadingStep,
  type ReadingTypeOption,
  validatePhoto,
  validateReadingDetails,
} from "@/features/mobile-reading/reading-flow";
import { sha256Hex } from "@/offline/crypto";
import { getOfflineStore } from "@/offline/indexed-db-store";
import type { CachedMachine, OfflinePhotoPayload } from "@/offline/types";

type CompletionState = "LOCAL" | "SYNCING" | "SYNCED";
type MachineWithOptionalReading = CachedMachine & { latestReadingValue?: number | null };

const fieldClassName = "mt-2 min-h-12 w-full rounded-lg border border-slate-300 bg-white px-3 text-base text-slate-950 shadow-sm focus:border-blue-700";

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatCacheDate(value: string | null) {
  return value ? formatDateTime(value) : "Ainda não preparado";
}

function StepProgress({ step }: { step: MobileReadingStep }) {
  const position = step === "IDENTIFY_MACHINE"
    ? 1
    : step === "ENTER_READING"
      ? 2
      : step === "CAPTURE_PHOTO"
        ? 3
        : 4;

  return (
    <div aria-label={`Etapa ${position} de 4`} className="mb-5">
      <div className="mb-2 flex items-center justify-between text-xs font-semibold uppercase tracking-wide text-slate-500">
        <span>Etapa {position} de 4</span>
        <span>{position === 1 ? "Máquina" : position === 2 ? "Leitura" : position === 3 ? "Foto" : "Confirmação"}</span>
      </div>
      <div className="grid grid-cols-4 gap-2" aria-hidden="true">
        {[1, 2, 3, 4].map((item) => (
          <span key={item} className={`h-1.5 rounded-full ${item <= position ? "bg-blue-800" : "bg-slate-200"}`} />
        ))}
      </div>
    </div>
  );
}

function MachineCard({ machine, onChange }: { machine: CachedMachine; onChange?: () => void }) {
  return (
    <section className="rounded-xl border border-blue-200 bg-blue-50 p-4" aria-label="Máquina identificada">
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 gap-3">
          <span className="grid size-11 shrink-0 place-items-center rounded-lg bg-blue-900 text-white"><HardHat className="size-6" /></span>
          <div className="min-w-0">
            <p className="text-xs font-bold uppercase tracking-wide text-blue-700">Máquina identificada</p>
            <p className="text-xl font-black text-slate-950">Frota {machine.fleetNumber}</p>
            <p className="truncate text-sm text-slate-600">{machine.model}</p>
          </div>
        </div>
        {onChange ? <Button type="button" variant="ghost" className="min-h-11 px-3 text-blue-800" onClick={onChange}>Trocar</Button> : null}
      </div>
      <div className="mt-3 flex items-center justify-between border-t border-blue-200 pt-3 text-sm">
        <span className="text-slate-600">Unidade do horímetro</span>
        <strong className="uppercase text-slate-950">{meterUnitLabel(machine.meterUnit)}</strong>
      </div>
    </section>
  );
}

function ReadinessCard() {
  const {
    connectivity,
    machines,
    workSites,
    referenceDataFetchedAt,
    refreshReferenceData,
    serviceWorkerReady,
  } = useSync();
  const [refreshing, setRefreshing] = useState(false);
  const age = cacheAgeState(referenceDataFetchedAt);
  const ready = machines.length > 0 && workSites.length > 0 && serviceWorkerReady;

  async function updateCache() {
    setRefreshing(true);
    try {
      await refreshReferenceData();
    } finally {
      setRefreshing(false);
    }
  }

  return (
    <section className={`rounded-xl border p-4 ${ready ? "border-emerald-200 bg-emerald-50" : "border-amber-200 bg-amber-50"}`} aria-labelledby="readiness-title">
      <div className="flex items-start justify-between gap-3">
        <div className="flex gap-3">
          {ready ? <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-700" /> : <CircleAlert className="mt-0.5 size-5 shrink-0 text-amber-700" />}
          <div>
            <h2 id="readiness-title" className="font-bold text-slate-950">{ready ? "Pronto para trabalhar offline" : "Prepare este dispositivo"}</h2>
            <p className="mt-1 text-sm text-slate-700">
              {machines.length} máquinas · {workSites.length} obras · shell {serviceWorkerReady ? "pronto" : "pendente"}
            </p>
            <p className={`mt-1 text-xs ${age === "STALE" ? "font-semibold text-amber-800" : "text-slate-600"}`}>
              Cache: {formatCacheDate(referenceDataFetchedAt)}{age === "STALE" ? " · atualização recomendada" : ""}
            </p>
          </div>
        </div>
        {connectivity === "OFFLINE" ? <WifiOff className="size-5 shrink-0 text-amber-800" aria-label="Offline" /> : <Wifi className="size-5 shrink-0 text-emerald-700" aria-label="Online" />}
      </div>
      {!ready && connectivity === "OFFLINE" ? (
        <p className="mt-3 rounded-lg bg-white/70 p-3 text-sm font-medium text-amber-950" role="alert">
          Sem cache suficiente e sem internet. Conecte este dispositivo e atualize os dados antes da primeira coleta.
        </p>
      ) : null}
      <Button type="button" variant="outline" className="mt-3 min-h-11 w-full bg-white" onClick={() => void updateCache()} disabled={refreshing || connectivity === "OFFLINE"}>
        <RefreshCw className={refreshing ? "animate-spin" : ""} /> {refreshing ? "Atualizando…" : "Atualizar dados de campo"}
      </Button>
    </section>
  );
}

export function ReadingCaptureForm() {
  const { user } = useAuth();
  const { identity } = useDeviceIdentity();
  const {
    workSites,
    refresh,
    refreshReferenceData,
    syncNow,
  } = useSync();
  const store = useMemo(() => getOfflineStore(), []);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const savingRef = useRef(false);
  const syncGenerationRef = useRef(0);
  const [step, setStep] = useState<MobileReadingStep>("IDENTIFY_MACHINE");
  const [machine, setMachine] = useState<CachedMachine | null>(null);
  const [qrValue, setQrValue] = useState("");
  const [cameraOpen, setCameraOpen] = useState(false);
  const [resolvingQr, setResolvingQr] = useState(false);
  const [readingValue, setReadingValue] = useState("");
  const [readingType, setReadingType] = useState<ReadingTypeOption>("INTERMEDIATE");
  const [workSiteId, setWorkSiteId] = useState("");
  const [capturedAt, setCapturedAt] = useState(localDateTimeValue);
  const [editingTime, setEditingTime] = useState(false);
  const [photo, setPhoto] = useState<File | null>(null);
  const [photoPreviewUrl, setPhotoPreviewUrl] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [completionState, setCompletionState] = useState<CompletionState>("LOCAL");

  const effectiveWorkSiteId = workSiteId || (workSites.length === 1 ? workSites[0].id : "");
  const selectedWorkSite = workSites.find((item) => item.id === effectiveWorkSiteId);
  const previousReading = (machine as MachineWithOptionalReading | null)?.latestReadingValue ?? null;
  const belowPrevious = isReadingBelowPrevious(readingValue, previousReading);

  useEffect(() => () => {
    if (photoPreviewUrl) URL.revokeObjectURL(photoPreviewUrl);
    syncGenerationRef.current += 1;
  }, [photoPreviewUrl]);

  function transition(to: MobileReadingStep) {
    if (!canTransitionReadingStep(step, to)) return false;
    setStep(to);
    setMessage(null);
    return true;
  }

  const resolveQrValue = useCallback(async (value: string) => {
    if (resolvingQr) return;
    setResolvingQr(true);
    setMessage(null);
    try {
      const result = await resolveMachineQr(value, {
        online: navigator.onLine,
        resolveCached: (candidate) => store.resolveCachedMachine(candidate),
        requestServer: (candidate) => fetch(`/api/qr-codes/resolve?value=${encodeURIComponent(candidate)}`, { cache: "no-store" }),
        refreshReferenceData,
        getCachedMachines: () => store.getCachedMachines(),
      });
      if (result.status === "RESOLVED") {
        setMachine(result.machine);
        setQrValue(value.trim());
        setCameraOpen(false);
        setStep("ENTER_READING");
        setMessage(result.source === "CACHE" ? "Máquina confirmada pelo cadastro local." : "Máquina confirmada pelo servidor.");
        return;
      }
      const messages = {
        EMPTY: "Leia ou informe o conteúdo do QR code.",
        NOT_FOUND: "QR não vinculado a uma máquina ativa.",
        NOT_FOUND_OFFLINE: "QR não encontrado no cache deste dispositivo. Conecte-se para atualizar as máquinas.",
        NETWORK_ERROR: "Não foi possível consultar o servidor. Verifique a conexão ou tente pelo cache local.",
      } as const;
      setMessage(messages[result.status]);
    } finally {
      setResolvingQr(false);
    }
  }, [refreshReferenceData, resolvingQr, store]);

  function chooseAnotherMachine() {
    setCameraOpen(false);
    setMachine(null);
    setQrValue("");
    setReadingValue("");
    setMessage(null);
    setStep("IDENTIFY_MACHINE");
  }

  function continueToPhoto() {
    const error = validateReadingDetails({ machine, readingValue, workSiteId: effectiveWorkSiteId, capturedAtLocal: capturedAt });
    if (error) {
      setMessage(error);
      return;
    }
    transition("CAPTURE_PHOTO");
  }

  function handlePhoto(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    const error = validatePhoto(selected);
    if (error) {
      setMessage(error);
      event.target.value = "";
      return;
    }
    if (photoPreviewUrl) URL.revokeObjectURL(photoPreviewUrl);
    setPhoto(selected);
    setPhotoPreviewUrl(selected ? URL.createObjectURL(selected) : null);
    setMessage(null);
  }

  function retakePhoto() {
    if (photoPreviewUrl) URL.revokeObjectURL(photoPreviewUrl);
    setPhoto(null);
    setPhotoPreviewUrl(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
    fileInputRef.current?.click();
  }

  function usePhoto() {
    const error = validatePhoto(photo);
    if (error) {
      setMessage(error);
      return;
    }
    transition("REVIEW");
  }

  async function saveReading() {
    if (step !== "REVIEW" || savingRef.current) return;
    if (!identity?.registeredDeviceId || identity.status !== "AUTHORIZED") {
      setMessage("Este dispositivo não está autorizado para registrar leituras.");
      return;
    }
    const detailError = validateReadingDetails({ machine, readingValue, workSiteId: effectiveWorkSiteId, capturedAtLocal: capturedAt });
    const photoError = validatePhoto(photo);
    if (detailError || photoError || !machine || !photo) {
      setMessage(detailError ?? photoError ?? "Revise os dados da coleta.");
      return;
    }
    const value = parseReadingValue(readingValue);
    if (value === null) return;
    if (!tryAcquireSubmissionLock(savingRef)) return;

    setStep("SAVING");
    setMessage(null);
    const generation = syncGenerationRef.current + 1;
    syncGenerationRef.current = generation;
    try {
      const deviceId = identity.localDeviceId;
      const photoSha256 = await sha256Hex(photo);
      const queued = await store.enqueueReadingWithPhoto({
        reading: {
          machineId: machine.id,
          workSiteId: effectiveWorkSiteId,
          value,
          readingType,
          capturedAtDevice: new Date(capturedAt).toISOString(),
          deviceId,
          deviceIdentity: {
            localDeviceId: identity.localDeviceId,
            registeredDeviceId: identity.registeredDeviceId,
            status: "AUTHORIZED",
          },
        },
        photo,
        photoFileName: photo.name || `horimetro-frota-${machine.fleetNumber}.jpg`,
        photoMimeType: photo.type as OfflinePhotoPayload["mimeType"],
        photoSha256,
        user,
      });

      setCompletionState(navigator.onLine ? "SYNCING" : "LOCAL");
      setStep("SUCCESS");
      void refresh();

      if (navigator.onLine) {
        void syncNow(false).then(async () => {
          if (syncGenerationRef.current !== generation) return;
          const [readingEvent, photoEvent] = await Promise.all([
            store.getEvent(queued.readingEvent.localEventId),
            store.getEvent(queued.photoEvent.localEventId),
          ]);
          if (syncGenerationRef.current !== generation) return;
          setCompletionState(readingEvent?.status === "SYNCED" && photoEvent?.status === "SYNCED" ? "SYNCED" : "LOCAL");
        }).catch(() => {
          if (syncGenerationRef.current === generation) setCompletionState("LOCAL");
        });
      }
    } catch {
      savingRef.current = false;
      setStep("REVIEW");
      setMessage("Não foi possível salvar no dispositivo. Verifique o espaço disponível e tente novamente.");
    }
  }

  function nextMachine() {
    syncGenerationRef.current += 1;
    savingRef.current = false;
    if (photoPreviewUrl) URL.revokeObjectURL(photoPreviewUrl);
    setPhotoPreviewUrl(null);
    setPhoto(null);
    setMachine(null);
    setQrValue("");
    setReadingValue("");
    setReadingType("INTERMEDIATE");
    setCapturedAt(localDateTimeValue());
    setEditingTime(false);
    setMessage(null);
    setCompletionState("LOCAL");
    setStep("IDENTIFY_MACHINE");
  }

  return (
    <div className="mx-auto grid max-w-5xl items-start gap-5 xl:grid-cols-[minmax(0,1fr)_320px]">
      <div className="order-2 xl:order-1">
        <section className="rounded-xl border bg-white p-4 shadow-sm sm:p-6">
          <StepProgress step={step} />

          {step === "IDENTIFY_MACHINE" ? (
            <div className="space-y-5">
              <div>
                <h2 className="text-2xl font-black text-slate-950">Identifique a máquina</h2>
                <p className="mt-1 text-sm text-slate-600">Leia o QR da máquina ou informe o código impresso.</p>
              </div>
              {cameraOpen ? (
                <QrCamera onDetected={(value) => void resolveQrValue(value)} onClose={() => setCameraOpen(false)} />
              ) : (
                <Button type="button" className="min-h-14 w-full text-base" onClick={() => setCameraOpen(true)}>
                  <Camera className="size-6" /> Ler QR com a câmera
                </Button>
              )}
              <div className="relative text-center text-xs font-bold uppercase tracking-wide text-slate-400 before:absolute before:left-0 before:right-0 before:top-1/2 before:border-t">
                <span className="relative bg-white px-3">ou digite</span>
              </div>
              <div>
                <Label htmlFor="qr-value">Código da máquina</Label>
                <div className="mt-2 grid gap-2 sm:grid-cols-[1fr_auto]">
                  <Input id="qr-value" value={qrValue} onChange={(event) => setQrValue(event.target.value)} onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      event.preventDefault();
                      void resolveQrValue(qrValue);
                    }
                  }} placeholder="Ex.: 0068 ou FROTA 68" autoComplete="off" className="min-h-12 text-base" />
                  <Button type="button" variant="outline" className="min-h-12" onClick={() => void resolveQrValue(qrValue)} disabled={resolvingQr}>
                    {resolvingQr ? <LoaderCircle className="animate-spin" /> : <QrCode />} Identificar
                  </Button>
                </div>
              </div>
            </div>
          ) : null}

          {step === "ENTER_READING" && machine ? (
            <div className="space-y-5">
              <MachineCard machine={machine} onChange={chooseAnotherMachine} />
              {machine.meterUnit === null ? (
                <div className="rounded-xl border border-amber-300 bg-amber-50 p-4" role="alert">
                  <div className="flex gap-3">
                    <CircleAlert className="mt-0.5 size-5 shrink-0 text-amber-800" />
                    <p className="font-semibold text-amber-950">
                      {MISSING_METER_UNIT_MESSAGE}
                    </p>
                  </div>
                  <Button type="button" variant="outline" className="mt-4 min-h-12 w-full bg-white" onClick={chooseAnotherMachine}>
                    <RotateCcw /> Trocar máquina
                  </Button>
                </div>
              ) : (
                <>
              <div>
                <Label htmlFor="reading-value" className="text-base">Leitura do horímetro</Label>
                <div className="relative mt-2">
                  <Input id="reading-value" value={readingValue} onChange={(event) => setReadingValue(event.target.value)} inputMode="decimal" autoComplete="off" placeholder="0,00" autoFocus className="h-16 pr-24 text-2xl font-black tabular-nums" aria-describedby="reading-help" />
                  <span className="pointer-events-none absolute inset-y-0 right-4 flex items-center font-bold uppercase text-slate-500">{meterUnitLabel(machine.meterUnit)}</span>
                </div>
                <p id="reading-help" className="mt-2 text-xs text-slate-500">Use vírgula ou ponto e até duas casas decimais.</p>
                {previousReading == null ? <p className="mt-2 text-xs text-slate-500">Última leitura não disponível no cache; o servidor fará a validação ao sincronizar.</p> : <p className="mt-2 text-sm text-slate-600">Última leitura: <strong>{previousReading.toLocaleString("pt-BR")} {meterUnitLabel(machine.meterUnit)}</strong></p>}
                {belowPrevious ? <p className="mt-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm font-medium text-amber-950" role="alert">A leitura informada é menor que a anterior. Você ainda pode prosseguir; o servidor revisará a divergência.</p> : null}
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <Label htmlFor="reading-type">Tipo de leitura</Label>
                  <select id="reading-type" value={readingType} onChange={(event) => setReadingType(event.target.value as ReadingTypeOption)} className={fieldClassName}>
                    {READING_TYPE_OPTIONS.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                </div>
                <div>
                  <Label htmlFor="worksite">Obra</Label>
                  <select id="worksite" value={effectiveWorkSiteId} onChange={(event) => setWorkSiteId(event.target.value)} className={fieldClassName} required>
                    <option value="">Selecione a obra</option>
                    {workSites.map((workSite) => <option key={workSite.id} value={workSite.id}>{workSite.name}</option>)}
                  </select>
                </div>
              </div>
              <div className="rounded-lg border bg-slate-50 p-4">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2 text-sm"><Clock3 className="size-5 text-slate-500" /><span>Horário: <strong>{formatDateTime(new Date(capturedAt).toISOString())}</strong></span></div>
                  <Button type="button" variant="ghost" className="min-h-11" onClick={() => setEditingTime((value) => !value)}>{editingTime ? "Concluir" : "Alterar"}</Button>
                </div>
                {editingTime ? <Input type="datetime-local" value={capturedAt} onChange={(event) => setCapturedAt(event.target.value)} className="mt-3 min-h-12" aria-label="Horário da coleta" /> : null}
              </div>
              <Button type="button" className="min-h-14 w-full text-base" onClick={continueToPhoto}>Continuar para foto <ArrowRight /></Button>
                </>
              )}
            </div>
          ) : null}

          {step === "CAPTURE_PHOTO" && machine ? (
            <div className="space-y-5">
              <MachineCard machine={machine} />
              <div>
                <h2 className="text-2xl font-black text-slate-950">Fotografe o mostrador</h2>
                <p className="mt-1 text-sm text-slate-600">Enquadre os números com nitidez. A data EXIF não substitui o horário registrado pelo aplicativo.</p>
              </div>
              <Input ref={fileInputRef} id="meter-photo" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" onChange={handlePhoto} className="sr-only" />
              {photoPreviewUrl ? (
                <div className="space-y-4">
                  <div className="relative aspect-[4/3] overflow-hidden rounded-xl border bg-slate-950">
                    <Image src={photoPreviewUrl} alt="Prévia da foto do horímetro" fill unoptimized className="object-contain" />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <Button type="button" variant="outline" className="min-h-12" onClick={retakePhoto}><RotateCcw /> Tirar novamente</Button>
                    <Button type="button" className="min-h-12" onClick={usePhoto}><Check /> Usar foto</Button>
                  </div>
                </div>
              ) : (
                <button type="button" onClick={() => fileInputRef.current?.click()} className="flex min-h-64 w-full flex-col items-center justify-center rounded-xl border-2 border-dashed border-blue-300 bg-blue-50 p-6 text-center text-blue-950 transition hover:bg-blue-100">
                  <span className="grid size-16 place-items-center rounded-full bg-blue-900 text-white"><Camera className="size-8" /></span>
                  <strong className="mt-4 text-lg">Abrir câmera</strong>
                  <span className="mt-1 text-sm text-blue-800">JPEG, PNG ou WEBP · máximo 10 MB</span>
                </button>
              )}
              <Button type="button" variant="ghost" className="min-h-11" onClick={() => transition("ENTER_READING")}><ArrowLeft /> Voltar aos dados</Button>
            </div>
          ) : null}

          {step === "REVIEW" && machine && photoPreviewUrl ? (
            <div className="space-y-5">
              <div>
                <h2 className="text-2xl font-black text-slate-950">Revise antes de salvar</h2>
                <p className="mt-1 text-sm text-slate-600">Depois de confirmar, a coleta entra na fila local e não é alterada.</p>
              </div>
              <div className="grid gap-4 sm:grid-cols-[1fr_180px]">
                <dl className="divide-y rounded-xl border bg-slate-50 px-4 text-sm">
                  <div className="flex justify-between gap-4 py-3"><dt className="text-slate-500">Máquina</dt><dd className="text-right font-bold">Frota {machine.fleetNumber} · {machine.model}</dd></div>
                  <div className="flex justify-between gap-4 py-3"><dt className="text-slate-500">Leitura</dt><dd className="font-bold tabular-nums">{readingValue.replace(".", ",")} {meterUnitLabel(machine.meterUnit)}</dd></div>
                  <div className="flex justify-between gap-4 py-3"><dt className="text-slate-500">Tipo</dt><dd className="font-bold">{READING_TYPE_OPTIONS.find((item) => item.value === readingType)?.label}</dd></div>
                  <div className="flex justify-between gap-4 py-3"><dt className="text-slate-500">Obra</dt><dd className="text-right font-bold">{selectedWorkSite?.name}</dd></div>
                  <div className="flex justify-between gap-4 py-3"><dt className="text-slate-500">Horário</dt><dd className="font-bold">{formatDateTime(new Date(capturedAt).toISOString())}</dd></div>
                </dl>
                <div className="relative aspect-[4/3] overflow-hidden rounded-xl border bg-slate-950 sm:aspect-auto">
                  <Image src={photoPreviewUrl} alt="Foto que será anexada à leitura" fill unoptimized className="object-contain" />
                </div>
              </div>
              <Button type="button" className="min-h-14 w-full text-base" onClick={() => void saveReading()}><Save /> Confirmar e salvar</Button>
              <Button type="button" variant="ghost" className="min-h-11" onClick={() => transition("CAPTURE_PHOTO")}><ArrowLeft /> Voltar à foto</Button>
            </div>
          ) : null}

          {step === "SAVING" ? (
            <div className="grid min-h-72 place-items-center text-center" role="status">
              <div><LoaderCircle className="mx-auto size-10 animate-spin text-blue-800" /><h2 className="mt-4 text-xl font-black">Salvando no dispositivo…</h2><p className="mt-2 text-sm text-slate-600">Mantenha esta tela aberta por alguns segundos.</p></div>
            </div>
          ) : null}

          {step === "SUCCESS" && machine ? (
            <div className="py-4 text-center">
              <span className="mx-auto grid size-20 place-items-center rounded-full bg-emerald-100 text-emerald-700"><CheckCircle2 className="size-11" /></span>
              <h2 className="mt-5 text-2xl font-black text-slate-950">Coleta salva</h2>
              <p className="mt-2 text-slate-600">Frota {machine.fleetNumber} · {readingValue.replace(".", ",")} {meterUnitLabel(machine.meterUnit)}</p>
              <div className={`mx-auto mt-4 max-w-md rounded-lg p-3 text-sm font-semibold ${completionState === "SYNCED" ? "bg-emerald-50 text-emerald-800" : "bg-blue-50 text-blue-900"}`} aria-live="polite">
                {completionState === "SYNCED" ? "Leitura e foto confirmadas pelo servidor." : completionState === "SYNCING" ? "Salva no dispositivo. Sincronizando em segundo plano…" : "Salva no dispositivo. Será enviada automaticamente quando houver conexão."}
              </div>
              <Button type="button" className="mt-6 min-h-14 w-full text-base" onClick={nextMachine}><QrCode /> Registrar próxima máquina</Button>
              <Button asChild variant="ghost" className="mt-2 min-h-11"><Link href="/sincronizacao">Ver fila de sincronização</Link></Button>
            </div>
          ) : null}

          {message ? <p className="mt-5 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm font-medium text-amber-950" role="alert" aria-live="assertive">{message}</p> : null}
        </section>
      </div>

      <aside className="order-1 space-y-4 xl:order-2 xl:sticky xl:top-24">
        <ReadinessCard />
        <section className="hidden rounded-xl border bg-white p-4 text-sm text-slate-600 xl:block">
          <h2 className="flex items-center gap-2 font-bold text-slate-950"><Gauge className="size-5 text-blue-800" /> Coleta confiável</h2>
          <p className="mt-2">A unidade vem do cadastro da máquina. Leitura, foto e autoria são preservadas localmente até a confirmação do servidor.</p>
          <p className="mt-3 flex items-center gap-2"><Building2 className="size-4" /> Obra mantida para agilizar a próxima máquina.</p>
        </section>
      </aside>
    </div>
  );
}
