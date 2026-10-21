export interface QrScannerSession {
  mode: "NATIVE" | "FALLBACK";
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

type ScannerOptions = {
  video: HTMLVideoElement;
  onResult: (value: string) => void;
  onError?: (error: unknown) => void;
  cooldownMs?: number;
};

function barcodeDetectorConstructor() {
  return (globalThis as typeof globalThis & {
    BarcodeDetector?: BarcodeDetectorConstructorLike;
  }).BarcodeDetector;
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

export function cameraErrorMessage(error: unknown) {
  const name = error instanceof DOMException
    ? error.name
    : typeof error === "object" && error && "name" in error
      ? String(error.name)
      : "";
  if (name === "NotAllowedError" || name === "PermissionDeniedError") {
    return "A câmera foi bloqueada. Libere a permissão nas configurações do navegador ou informe o QR manualmente.";
  }
  if (name === "NotFoundError" || name === "DevicesNotFoundError") {
    return "Nenhuma câmera foi encontrada neste dispositivo. Informe o QR manualmente.";
  }
  return "Não foi possível iniciar a câmera. Informe o QR manualmente ou tente novamente.";
}

async function startNativeScanner(options: ScannerOptions): Promise<QrScannerSession> {
  const Detector = barcodeDetectorConstructor();
  if (!Detector) throw new Error("BarcodeDetector indisponível.");

  const detector = new Detector({ formats: ["qr_code"] });
  let stream: MediaStream | null = null;
  let animationFrame = 0;
  let stopped = false;
  let detecting = false;
  let activeDeviceId: string | undefined;
  let lastValue = "";
  let lastSeenAt = 0;

  const openStream = async (deviceId?: string) => {
    stopMediaStream(stream);
    stream = await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: deviceId
        ? { deviceId: { exact: deviceId } }
        : { facingMode: { ideal: "environment" } },
    });
    const track = stream.getVideoTracks()[0];
    activeDeviceId = track?.getSettings().deviceId ?? deviceId;
    options.video.srcObject = stream;
    await options.video.play();
  };

  const stop = () => {
    if (stopped) return;
    stopped = true;
    cancelAnimationFrame(animationFrame);
    stopMediaStream(stream);
    stream = null;
    options.video.pause();
    options.video.srcObject = null;
  };

  const scan = async () => {
    if (stopped) return;
    if (!detecting && options.video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
      detecting = true;
      try {
        const [result] = await detector.detect(options.video);
        const value = result?.rawValue?.trim();
        const now = Date.now();
        if (value && (value !== lastValue || now - lastSeenAt >= (options.cooldownMs ?? 2_000))) {
          lastValue = value;
          lastSeenAt = now;
          stop();
          options.onResult(value);
          return;
        }
      } catch (error) {
        options.onError?.(error);
      } finally {
        detecting = false;
      }
    }
    animationFrame = requestAnimationFrame(() => void scan());
  };

  try {
    await openStream();
    animationFrame = requestAnimationFrame(() => void scan());
  } catch (error) {
    stop();
    throw error;
  }

  return {
    mode: "NATIVE",
    stop,
    switchCamera: async () => {
      const cameras = (await navigator.mediaDevices.enumerateDevices())
        .filter((device) => device.kind === "videoinput");
      if (cameras.length < 2) return false;
      const currentIndex = cameras.findIndex((camera) => camera.deviceId === activeDeviceId);
      const next = cameras[(currentIndex + 1 + cameras.length) % cameras.length];
      if (!next) return false;
      await openStream(next.deviceId);
      return true;
    },
  };
}

async function startFallbackScanner(options: ScannerOptions): Promise<QrScannerSession> {
  const { default: QrScanner } = await import("qr-scanner");
  let stopped = false;
  let cameraIndex = 0;
  let lastValue = "";
  let lastSeenAt = 0;
  const scanner = new QrScanner(
    options.video,
    (result) => {
      const value = (typeof result === "string" ? result : result.data).trim();
      const now = Date.now();
      if (!value || (value === lastValue && now - lastSeenAt < (options.cooldownMs ?? 2_000))) return;
      lastValue = value;
      lastSeenAt = now;
      scanner.stop();
      options.onResult(value);
    },
    {
      preferredCamera: "environment",
      highlightScanRegion: true,
      highlightCodeOutline: true,
      maxScansPerSecond: 8,
      returnDetailedScanResult: true,
    },
  );

  try {
    await scanner.start();
  } catch (error) {
    scanner.destroy();
    throw error;
  }

  return {
    mode: "FALLBACK",
    stop: () => {
      if (stopped) return;
      stopped = true;
      scanner.stop();
      scanner.destroy();
      options.video.srcObject = null;
    },
    switchCamera: async () => {
      const cameras = await QrScanner.listCameras(true);
      if (cameras.length < 2) return false;
      cameraIndex = (cameraIndex + 1) % cameras.length;
      const next = cameras[cameraIndex];
      if (!next) return false;
      await scanner.setCamera(next.id);
      return true;
    },
  };
}

export async function startQrScanner(options: ScannerOptions): Promise<QrScannerSession> {
  return (await supportsNativeQrScanner())
    ? startNativeScanner(options)
    : startFallbackScanner(options);
}
