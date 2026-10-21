import { readFileSync } from "node:fs";
import { join } from "node:path";

import { afterEach, describe, expect, it } from "vitest";

import manifest from "@/app/manifest";
import {
  defaultAuthenticatedPath,
  isOperatorOnly,
  isOperatorPathAllowed,
  navigationItemsForRoles,
} from "@/components/layout/navigation";
import {
  cameraErrorMessage,
  stopMediaStream,
  supportsNativeQrScanner,
} from "@/features/mobile-reading/qr-scanner-controller";

const previousDetector = Object.getOwnPropertyDescriptor(globalThis, "BarcodeDetector");

afterEach(() => {
  if (previousDetector) Object.defineProperty(globalThis, "BarcodeDetector", previousDetector);
  else Reflect.deleteProperty(globalThis, "BarcodeDetector");
});

describe("plataforma mobile da Fase 8", () => {
  it("usa scanner nativo somente quando QR é suportado", async () => {
    class Detector {
      static async getSupportedFormats() { return ["qr_code", "ean_13"]; }
      async detect() { return []; }
    }
    Object.defineProperty(globalThis, "BarcodeDetector", { configurable: true, value: Detector });
    expect(await supportsNativeQrScanner()).toBe(true);
    Detector.getSupportedFormats = async () => ["ean_13"];
    expect(await supportsNativeQrScanner()).toBe(false);
  });

  it("libera todas as trilhas da câmera e oferece mensagem acionável quando a permissão é negada", () => {
    let stopped = 0;
    stopMediaStream({ getTracks: () => [{ stop: () => { stopped += 1; } }, { stop: () => { stopped += 1; } }] } as unknown as MediaStream);
    expect(stopped).toBe(2);
    expect(cameraErrorMessage({ name: "NotAllowedError" })).toContain("Libere a permissão");
  });

  it("reduz a navegação do OPERATOR e redireciona seu login para coleta", () => {
    expect(isOperatorOnly(["OPERATOR"])).toBe(true);
    expect(navigationItemsForRoles(["OPERATOR"]).map((item) => item.href)).toEqual(["/coleta", "/sincronizacao"]);
    expect(defaultAuthenticatedPath(["OPERATOR"])).toBe("/coleta");
    expect(defaultAuthenticatedPath(["ADMIN"])).toBe("/");
    expect(isOperatorPathAllowed("/coleta")).toBe(true);
    expect(isOperatorPathAllowed("/sincronizacao")).toBe(true);
    expect(isOperatorPathAllowed("/frota")).toBe(false);
  });

  it("publica manifesto instalável com ícones reais 192 e 512", () => {
    const value = manifest();
    expect(value.start_url).toBe("/coleta");
    expect(value.display).toBe("standalone");
    expect(value.icons?.some((icon) => icon.sizes === "192x192")).toBe(true);
    expect(value.icons?.some((icon) => icon.sizes === "512x512")).toBe(true);
  });

  it("mantém o service worker restrito ao shell e sem cache de API", () => {
    const worker = readFileSync(join(process.cwd(), "public", "sw.js"), "utf8");
    expect(worker).toContain('url.pathname.startsWith("/api/")');
    expect(worker).toContain('"/coleta"');
    expect(worker).toContain('"/sincronizacao"');
    expect(worker).toContain('"/pwa-icon-512.png"');
  });
});
