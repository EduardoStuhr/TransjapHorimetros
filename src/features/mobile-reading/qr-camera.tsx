"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Camera, CameraOff, RefreshCw } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  cameraDiagnosticDetails,
  cameraErrorMessage,
  startQrScanner,
  type QrScannerSession,
} from "@/features/mobile-reading/qr-scanner-controller";

interface QrCameraProps {
  onDetected: (value: string) => void;
  onClose: () => void;
}

export function QrCamera({ onDetected, onClose }: QrCameraProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const sessionRef = useRef<QrScannerSession | null>(null);
  const onDetectedRef = useRef(onDetected);
  const [starting, setStarting] = useState(true);
  const [mode, setMode] = useState<QrScannerSession["mode"] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [switching, setSwitching] = useState(false);

  useEffect(() => {
    onDetectedRef.current = onDetected;
  }, [onDetected]);

  const stop = useCallback(() => {
    sessionRef.current?.stop();
    sessionRef.current = null;
  }, []);

  const reportTechnicalError = useCallback((stage: string, reason: unknown, fallbackActivated = false) => {
    if (process.env.NODE_ENV !== "development") return;
    console.error("[qr-camera] Falha técnica da câmera", {
      ...cameraDiagnosticDetails(reason),
      fallbackActivated,
      stage,
    });
  }, []);

  useEffect(() => {
    let cancelled = false;
    const video = videoRef.current;
    if (!video) return;

    setStarting(true);
    setError(null);
    void startQrScanner({
      video,
      onResult: (value) => {
        stop();
        if (!cancelled) onDetectedRef.current(value);
      },
      onModeChange: (nextMode) => {
        if (!cancelled) setMode(nextMode);
      },
      onDiagnostic: ({ error: reason, fallbackActivated, stage }) => {
        reportTechnicalError(stage, reason, fallbackActivated);
      },
      onError: (reason) => {
        reportTechnicalError("RUNTIME", reason);
        if (!cancelled) setError(cameraErrorMessage(reason));
      },
    }).then((session) => {
      if (cancelled) {
        session.stop();
        return;
      }
      sessionRef.current = session;
      setMode(session.mode);
      setStarting(false);
    }).catch((reason: unknown) => {
      reportTechnicalError("START", reason);
      if (!cancelled) {
        setError(cameraErrorMessage(reason));
        setStarting(false);
      }
    });

    return () => {
      cancelled = true;
      stop();
    };
  }, [reportTechnicalError, stop]);

  async function switchCamera() {
    const session = sessionRef.current;
    if (!session) return;
    setSwitching(true);
    try {
      const switched = await session.switchCamera();
      if (!switched) setError("Não há outra câmera disponível neste dispositivo.");
    } catch (reason) {
      reportTechnicalError("CAMERA_SWITCH", reason);
      setError(cameraErrorMessage(reason));
    } finally {
      setSwitching(false);
    }
  }

  function close() {
    stop();
    onClose();
  }

  return (
    <section className="overflow-hidden rounded-xl border border-slate-700 bg-slate-950 text-white" aria-label="Leitor de QR code">
      <div className="relative aspect-[3/4] max-h-[68vh] w-full bg-black sm:aspect-video">
        <video
          ref={videoRef}
          autoPlay
          muted
          playsInline
          aria-label="Imagem ao vivo da câmera para leitura do QR code"
          className="h-full w-full object-cover"
        />
        <div className="pointer-events-none absolute inset-[16%] rounded-2xl border-2 border-amber-400 shadow-[0_0_0_999px_rgba(0,0,0,.28)]" />
        <p className="absolute inset-x-4 bottom-4 rounded-lg bg-slate-950/80 px-3 py-2 text-center text-sm">
          Aponte para o QR fixado na máquina
        </p>
        {starting ? (
          <div className="absolute inset-0 grid place-items-center bg-slate-950/70" role="status">
            <span className="flex items-center gap-2"><RefreshCw className="size-5 animate-spin" /> Iniciando câmera…</span>
          </div>
        ) : null}
      </div>

      <div className="space-y-3 p-4">
        {mode ? (
          <p className="text-xs text-slate-300" aria-live="polite">
            Leitor {mode === "NATIVE" ? "nativo" : "compatível"} ativo
          </p>
        ) : null}
        {error ? <p className="rounded-lg bg-amber-400/15 p-3 text-sm text-amber-100" role="alert">{error}</p> : null}
        <div className="grid grid-cols-2 gap-3">
          <Button type="button" variant="secondary" className="min-h-11" onClick={() => void switchCamera()} disabled={!mode || switching}>
            <Camera className="size-5" /> Trocar
          </Button>
          <Button type="button" variant="outline" className="min-h-11 border-white/30 bg-transparent text-white hover:bg-white/10 hover:text-white" onClick={close}>
            <CameraOff className="size-5" /> Fechar
          </Button>
        </div>
      </div>
    </section>
  );
}
