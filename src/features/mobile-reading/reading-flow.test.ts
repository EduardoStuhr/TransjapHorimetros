import { describe, expect, it } from "vitest";

import {
  cacheAgeState,
  canTransitionReadingStep,
  isReadingBelowPrevious,
  parseReadingValue,
  tryAcquireSubmissionLock,
  validatePhoto,
  validateReadingDetails,
} from "@/features/mobile-reading/reading-flow";
import type { CachedMachine } from "@/offline/types";

const machine: CachedMachine = {
  id: "machine-1",
  fleetNumber: 68,
  model: "Escavadeira",
  meterUnit: "HOURS",
  status: "ACTIVE",
  activeQrToken: "QR-68",
  updatedAtServer: "2026-10-20T10:00:00.000Z",
  fetchedAt: "2026-10-20T10:00:00.000Z",
  cacheVersion: 1,
};

describe("fluxo mobile de coleta", () => {
  it.each([
    ["1250", 1250],
    ["1250,5", 1250.5],
    ["1250.50", 1250.5],
    ["0,00", 0],
  ])("aceita a leitura %s", (input, expected) => {
    expect(parseReadingValue(input)).toBe(expected);
  });

  it.each(["", "-1", "1,234", "1.2.3", "abc", ",5"])("rejeita a leitura inválida %s", (input) => {
    expect(parseReadingValue(input)).toBeNull();
  });

  it("valida máquina, unidade, obra e horário sem permitir unidade escolhida pelo operador", () => {
    expect(validateReadingDetails({ machine, readingValue: "120,5", workSiteId: "work-1", capturedAtLocal: "2026-10-20T10:30" })).toBeNull();
    expect(validateReadingDetails({ machine: { ...machine, meterUnit: null }, readingValue: "120", workSiteId: "work-1", capturedAtLocal: "2026-10-20T10:30" })).toContain("unidade");
    expect(validateReadingDetails({ machine, readingValue: "120", workSiteId: "", capturedAtLocal: "2026-10-20T10:30" })).toContain("obra");
  });

  it("valida formato e limite de 10 MB da foto", () => {
    expect(validatePhoto(new Blob(["ok"], { type: "image/jpeg" }))).toBeNull();
    expect(validatePhoto(new Blob(["x"], { type: "text/plain" }))).toContain("JPEG");
    expect(validatePhoto({ size: 10 * 1024 * 1024 + 1, type: "image/jpeg" } as Blob)).toContain("10 MB");
  });

  it("mantém o alerta de regressão de leitura como não bloqueante", () => {
    expect(isReadingBelowPrevious("99,9", 100)).toBe(true);
    expect(isReadingBelowPrevious("100", 100)).toBe(false);
    expect(isReadingBelowPrevious("99", null)).toBe(false);
  });

  it("só permite transições válidas e bloqueia confirmação duplicada", () => {
    expect(canTransitionReadingStep("IDENTIFY_MACHINE", "ENTER_READING")).toBe(true);
    expect(canTransitionReadingStep("IDENTIFY_MACHINE", "REVIEW")).toBe(false);
    expect(canTransitionReadingStep("SAVING", "SUCCESS")).toBe(true);
    const lock = { current: false };
    expect(tryAcquireSubmissionLock(lock)).toBe(true);
    expect(tryAcquireSubmissionLock(lock)).toBe(false);
  });

  it("classifica cache ausente, atual e vencido em 24 horas", () => {
    const now = new Date("2026-10-21T12:00:00.000Z");
    expect(cacheAgeState(null, now)).toBe("EMPTY");
    expect(cacheAgeState("2026-10-21T11:00:00.000Z", now)).toBe("FRESH");
    expect(cacheAgeState("2026-10-20T11:59:59.000Z", now)).toBe("STALE");
  });
});
