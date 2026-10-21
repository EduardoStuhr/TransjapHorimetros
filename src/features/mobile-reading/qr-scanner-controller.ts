export type QrScannerMode = "NATIVE" | "FALLBACK";

export interface QrScannerSession {
  readonly mode: QrScannerMode;
  stop: () => void;
  switchCamera: () => Promise<boolean>;
}

interface BarcodeResultLike {
  rawValue?: string;
}

interface BarcodeDetectorLike {
  detect(source: HTMLVideoElement): Promise<BarcodeResultLike[]>;
}

interface BarcodeDetectorConstructorLike {
  new (options: { formats: string[] }): BarcodeDetectorLike;
  getSupportedFormats?: () => Promise<string[]>;
}

export type QrScannerDiagnosticStage =
  | "MEDIA_DEVICES"
  | "OPEN_STREAM"
  | "VIDEO_TRACK"
  | "VIDEO_PLAYBACK"
  | "NATIVE_DETECTOR"
  | "FALLBACK_ENGINE"
  | "CAMERA_SWITCH";

export interface QrScannerDiagnostic {
  error: unknown;
  fallbackActivated: boolean;
  stage: QrScannerDiagnosticStage;
}

type ScannerOptions = {
  video: HTMLVideoElement;
  onResult: (value: string) => void;
  onError?: (error: unknown) => void;
  onDiagnostic?: (diagnostic: QrScannerDiagnostic) => void;
  onModeChange?: (mode: QrScannerMode) => void;
  cooldownMs?: number;
};

const nativeFallbackDelayMs = 2_500;
const nativeErrorLimit = 2;
const scanIntervalMs = 125;
const videoReadyTimeoutMs = 8_000;

function barcodeDetectorConstructor() {
  return (globalThis as typeof globalThis & {
    BarcodeDetector?: BarcodeDetectorConstructorLike;
  }).BarcodeDetector;
}

function namedError(name: string, message: string) {
  return typeof DOMException === "undefined"
    ? Object.assign(new Error(message), { name })
    : new DOMException(message, name);
}

export function cameraErrorName(error: unknown) {
  return error instanceof DOMException
    ? error.name
    : typeof error === "object" && error && "name" in error
      ? String(error.name)
      : "";
}

export function cameraErrorMessage(error: unknown) {
  const name = cameraErrorName(error);
  if (name === "NotAllowedError" || name === "PermissionDeniedError") {
    return "A câmera foi bloqueada. Libere a permissão nas configurações do navegador ou informe o QR manualmente.";
  }
  if (name === "NotFoundError" || name === "DevicesNotFoundError") {
    return "Nenhuma câmera foi encontrada neste dispositivo. Informe o QR manualmente.";
  }
  if (name === "NotReadableError" || name === "TrackStartError") {
    return "A câmera está indisponível ou em uso por outro aplicativo. Feche-o e tente novamente.";
  }
  if (name === "AbortError") {
    return "A abertura da câmera foi interrompida. Feche o leitor e tente novamente.";
  }
  if (name === "OverconstrainedError" || name === "ConstraintNotSatisfiedError") {
    return "A câmera solicitada não está disponível. Tente trocar de câmera ou informe o QR manualmente.";
  }
  if (name === "SecurityError" || name === "TypeError") {
    return "A câmera exige uma conexão segura. Use HTTPS ou localhost (inclusive via adb reverse) e tente novamente.";
  }
  return "Não foi possível iniciar a câmera. Feche o leitor, tente novamente ou informe o QR manualmente.";
}

export function cameraDiagnosticDetails(error: unknown) {
  return {
    name: cameraErrorName(error) || "UnknownError",
    message: error instanceof Error ? error.message : String(error),
  };
}

export async function supportsNativeQrScanner() {
  const Detector = barcodeDetectorConstructor();
  if (!Detector) return false;
  if (!Detector.getSupportedFormats) return true;
  try {
    return (await Detector.getSupportedFormats()).includes("qr_code");
  } catch {
    return false;
  }
}

export function stopMediaStream(stream: MediaStream | null | undefined) {
  stream?.getTracks().forEach((track) => track.stop());
}

export function getLiveVideoTrack(stream: MediaStream) {
  const track = stream.getVideoTracks()[0];
  if (!track) {
    throw namedError("NotFoundError", "O stream aberto não contém uma faixa de vídeo.");
  }
  if (track.readyState !== "live") {
    throw namedError("NotReadableError", "A faixa de vídeo não está ativa.");
  }
  return track;
}

export function shouldActivateNativeFallback(elapsedMs: number, consecutiveErrors: number) {
  return elapsedMs >= nativeFallbackDelayMs || consecutiveErrors >= nativeErrorLimit;
}

function waitForVideoEvent(
  video: HTMLVideoElement,
  readyState: number,
  events: string[],
  timeoutMs = videoReadyTimeoutMs,
) {
  if (video.readyState >= readyState) return Promise.resolve();
  return new Promise<void>((resolve, reject) => {
    const timeout = window.setTimeout(() => {
      cleanup();
      reject(namedError("AbortError", "A câmera não entregou quadros de vídeo dentro do tempo esperado."));
    }, timeoutMs);
    const onReady = () => {
      cleanup();
      resolve();
    };
    const onError = () => {
      cleanup();
      reject(namedError("NotReadableError", "O elemento de vídeo não conseguiu reproduzir a câmera."));
    };
    const cleanup = () => {
      window.clearTimeout(timeout);
      events.forEach((event) => video.removeEventListener(event, onReady));
      video.removeEventListener("error", onError);
      video.removeEventListener("abort", onError);
    };
    events.forEach((event) => video.addEventListener(event, onReady, { once: true }));
    video.addEventListener("error", onError, { once: true });
    video.addEventListener("abort", onError, { once: true });
  });
}

async function attachAndPlay(video: HTMLVideoElement, stream: MediaStream) {
  getLiveVideoTrack(stream);
  video.autoplay = true;
  video.muted = true;
  video.playsInline = true;
  video.setAttribute("autoplay", "");
  video.setAttribute("muted", "");
  video.setAttribute("playsinline", "");
  video.srcObject = stream;
  await waitForVideoEvent(video, HTMLMediaElement.HAVE_METADATA, ["loadedmetadata"]);
  await video.play();
  await waitForVideoEvent(video, HTMLMediaElement.HAVE_CURRENT_DATA, ["loadeddata", "canplay", "playing"]);
}

async function createFallbackEngine() {
  const [{ default: QrScanner }, { createWorker }] = await Promise.all([
    import("qr-scanner"),
    import("qr-scanner/qr-scanner-worker.min.js"),
  ]);
  const worker = createWorker();
  const canvas = document.createElement("canvas");
  return {
    async detect(video: HTMLVideoElement) {
      try {
        const result = await QrScanner.scanImage(video, {
          canvas,
          qrEngine: worker,
          returnDetailedScanResult: true,
        });
        return result.data;
      } catch (error) {
        if (error === QrScanner.NO_QR_CODE_FOUND || String(error).includes(QrScanner.NO_QR_CODE_FOUND)) {
          return null;
        }
        throw error;
      }
    },
    stop() {
      worker.terminate();
    },
  };
}

export async function startQrScanner(options: ScannerOptions): Promise<QrScannerSession> {
  if (!globalThis.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
    throw namedError("SecurityError", "navigator.mediaDevices.getUserMedia não está disponível neste contexto.");
  }

  const nativeSupported = await supportsNativeQrScanner();
  const Detector = barcodeDetectorConstructor();
  let detector = nativeSupported && Detector ? new Detector({ formats: ["qr_code"] }) : null;
  let fallbackEngine: Awaited<ReturnType<typeof createFallbackEngine>> | null = null;
  let fallbackPromise: Promise<Awaited<ReturnType<typeof createFallbackEngine>>> | null = null;
  let stream: MediaStream | null = null;
  let activeTrack: MediaStreamTrack | null = null;
  let activeDeviceId: string | undefined;
  let animationFrame = 0;
  let stopped = false;
  let detecting = false;
  let switching = false;
  let consecutiveNativeErrors = 0;
  let mode: QrScannerMode = detector ? "NATIVE" : "FALLBACK";
  let nativeStartedAt = performance.now();
  let lastScanStartedAt = Number.NEGATIVE_INFINITY;
  let lastValue = "";
  let lastSeenAt = 0;

  const diagnostic = (
    stage: QrScannerDiagnosticStage,
    error: unknown,
    fallbackActivated = false,
  ) => options.onDiagnostic?.({ error, fallbackActivated, stage });

  const ensureFallbackEngine = async () => {
    fallbackPromise ??= createFallbackEngine();
    fallbackEngine = await fallbackPromise;
    return fallbackEngine;
  };

  const activateFallback = async (reason?: unknown) => {
    if (mode === "FALLBACK" && fallbackEngine) return;
    if (reason) diagnostic("NATIVE_DETECTOR", reason, true);
    await ensureFallbackEngine();
    detector = null;
    mode = "FALLBACK";
    options.onModeChange?.(mode);
  };

  const handleTrackEnded = () => {
    if (stopped || switching) return;
    const error = namedError("NotReadableError", "A faixa de vídeo foi encerrada pelo dispositivo.");
    diagnostic("VIDEO_TRACK", error);
    options.onError?.(error);
  };

  const detachCurrentStream = () => {
    activeTrack?.removeEventListener("ended", handleTrackEnded);
    activeTrack = null;
    stopMediaStream(stream);
    stream = null;
    options.video.pause();
    options.video.srcObject = null;
  };

  const openStream = async (deviceId?: string) => {
    let opened: MediaStream;
    try {
      opened = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: deviceId
          ? { deviceId: { exact: deviceId } }
          : { facingMode: { ideal: "environment" } },
      });
    } catch (error) {
      diagnostic("OPEN_STREAM", error);
      throw error;
    }

    try {
      const track = getLiveVideoTrack(opened);
      stream = opened;
      activeTrack = track;
      activeDeviceId = track.getSettings().deviceId ?? deviceId;
      track.addEventListener("ended", handleTrackEnded);
      await attachAndPlay(options.video, opened);
    } catch (error) {
      diagnostic(cameraErrorName(error) === "NotReadableError" ? "VIDEO_TRACK" : "VIDEO_PLAYBACK", error);
      stopMediaStream(opened);
      if (stream === opened) stream = null;
      throw error;
    }
  };

  const stop = () => {
    if (stopped) return;
    stopped = true;
    cancelAnimationFrame(animationFrame);
    detachCurrentStream();
    fallbackEngine?.stop();
    fallbackEngine = null;
  };

  const scan = async () => {
    if (stopped) return;
    const scanStartedAt = performance.now();
    if (!switching
      && !detecting
      && scanStartedAt - lastScanStartedAt >= scanIntervalMs
      && options.video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
      detecting = true;
      lastScanStartedAt = scanStartedAt;
      try {
        let value: string | null | undefined;
        if (mode === "NATIVE" && detector) {
          try {
            const [result] = await detector.detect(options.video);
            value = result?.rawValue?.trim();
            consecutiveNativeErrors = 0;
          } catch (error) {
            consecutiveNativeErrors += 1;
            diagnostic("NATIVE_DETECTOR", error);
            if (shouldActivateNativeFallback(performance.now() - nativeStartedAt, consecutiveNativeErrors)) {
              await activateFallback(error);
            }
          }
          if (!value && mode === "NATIVE" && shouldActivateNativeFallback(performance.now() - nativeStartedAt, consecutiveNativeErrors)) {
            await activateFallback(namedError("NotSupportedError", "BarcodeDetector não confirmou um QR no intervalo de compatibilidade."));
          }
        } else {
          value = await (await ensureFallbackEngine()).detect(options.video);
        }

        const now = Date.now();
        if (value && (value !== lastValue || now - lastSeenAt >= (options.cooldownMs ?? 2_000))) {
          lastValue = value;
          lastSeenAt = now;
          stop();
          options.onResult(value);
          return;
        }
      } catch (error) {
        diagnostic("FALLBACK_ENGINE", error);
      } finally {
        detecting = false;
      }
    }
    animationFrame = requestAnimationFrame(() => void scan());
  };

  try {
    if (!detector) {
      await activateFallback();
    } else {
      options.onModeChange?.(mode);
    }
    await openStream();
    nativeStartedAt = performance.now();
    animationFrame = requestAnimationFrame(() => void scan());
  } catch (error) {
    stop();
    throw error;
  }

  return {
    get mode() {
      return mode;
    },
    stop,
    switchCamera: async () => {
      const cameras = (await navigator.mediaDevices.enumerateDevices())
        .filter((device) => device.kind === "videoinput");
      if (cameras.length < 2) return false;
      const currentIndex = cameras.findIndex((camera) => camera.deviceId === activeDeviceId);
      const next = cameras[(currentIndex + 1 + cameras.length) % cameras.length];
      if (!next) return false;
      const previousDeviceId = activeDeviceId;
      switching = true;
      try {
        detachCurrentStream();
        await openStream(next.deviceId);
        nativeStartedAt = performance.now();
        lastScanStartedAt = Number.NEGATIVE_INFINITY;
        consecutiveNativeErrors = 0;
        return true;
      } catch (error) {
        diagnostic("CAMERA_SWITCH", error);
        if (!stopped && previousDeviceId) {
          try {
            await openStream(previousDeviceId);
          } catch (recoveryError) {
            diagnostic("CAMERA_SWITCH", recoveryError);
          }
        }
        throw error;
      } finally {
        switching = false;
      }
    },
  };
}
