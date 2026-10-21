import { describe, expect, it, vi } from "vitest";

import {
  cameraDiagnosticDetails,
  cameraErrorMessage,
  getLiveVideoTrack,
  shouldActivateNativeFallback,
  stopMediaStream,
} from "./qr-scanner-controller";

function errorNamed(name: string, message = name) {
  return Object.assign(new Error(message), { name });
}

describe("cameraErrorMessage", () => {
  it.each([
    ["NotAllowedError", "permissão"],
    ["NotFoundError", "Nenhuma câmera"],
    ["NotReadableError", "outro aplicativo"],
    ["AbortError", "interrompida"],
    ["OverconstrainedError", "câmera solicitada"],
    ["SecurityError", "HTTPS ou localhost"],
  ])("explica %s sem expor o erro técnico", (name, expected) => {
    expect(cameraErrorMessage(errorNamed(name))).toContain(expected);
  });

  it("mantém apenas nome e mensagem no diagnóstico técnico", () => {
    const error = Object.assign(errorNamed("OverconstrainedError", "facingMode inválido"), {
      constraint: "deviceId-secret",
    });
    expect(cameraDiagnosticDetails(error)).toEqual({
      name: "OverconstrainedError",
      message: "facingMode inválido",
    });
  });
});

describe("stream da câmera", () => {
  it("exige uma video track viva", () => {
    expect(() => getLiveVideoTrack({ getVideoTracks: () => [] } as unknown as MediaStream))
      .toThrow(/faixa de vídeo/);
    expect(() => getLiveVideoTrack({
      getVideoTracks: () => [{ readyState: "ended" }],
    } as unknown as MediaStream)).toThrow(/não está ativa/);

    const track = { readyState: "live" };
    expect(getLiveVideoTrack({
      getVideoTracks: () => [track],
    } as unknown as MediaStream)).toBe(track);
  });

  it("fecha todas as tracks no cleanup", () => {
    const tracks = [{ stop: vi.fn() }, { stop: vi.fn() }];
    stopMediaStream({ getTracks: () => tracks } as unknown as MediaStream);
    expect(tracks.every((track) => track.stop.mock.calls.length === 1)).toBe(true);
  });
});

describe("fallback Android", () => {
  it("troca de BarcodeDetector após falhas técnicas consecutivas", () => {
    expect(shouldActivateNativeFallback(100, 1)).toBe(false);
    expect(shouldActivateNativeFallback(100, 2)).toBe(true);
  });

  it("troca para o worker se o detector nativo não confirmar QR no prazo", () => {
    expect(shouldActivateNativeFallback(2_499, 0)).toBe(false);
    expect(shouldActivateNativeFallback(2_500, 0)).toBe(true);
  });
});
