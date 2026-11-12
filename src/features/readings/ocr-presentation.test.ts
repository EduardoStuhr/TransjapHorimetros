import { describe, expect, it, vi } from "vitest";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { PhotoOcrPanel } from "@/features/readings/components/photo-ocr-panel";
import type { PhotoOcrResult } from "@/types/domain";

const identity = vi.hoisted(() => ({ roles: ["SUPERVISOR"] }));
vi.mock("@/components/auth/auth-provider", () => ({
  useAuth: () => ({ user: identity }),
}));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn() }) }));
const result: PhotoOcrResult = {
  id: "result",
  photoEvidenceId: "photo",
  status: "NO_VALUE_DETECTED",
  detectedValue: null,
  confidence: null,
  processedAtServer: null,
  provider: "Test",
  providerModel: "test-v1",
  matchesOperatorValue: null,
  absoluteDifference: null,
  percentageDifference: null,
  errorCode: null,
  errorMessage: null,
  createdAt: "2026-11-12T12:00:00Z",
  reviewReason: "AMBIGUOUS_VALUES",
  candidates: [
    { token: "123", value: 123, hasUnit: true },
    { token: "456", value: 456, hasUnit: true },
  ],
  rawText: "HOURS 123 HOURS 456",
  algorithmVersion: "meter-normalization-v2",
};

describe("painel de qualidade OCR", () => {
  it("mostra ambiguidade, candidatos e contexto de confiança para supervisão", () => {
    identity.roles = ["SUPERVISOR"];
    const html = renderToStaticMarkup(
      createElement(PhotoOcrPanel, {
        readingId: "reading",
        operatorValue: 123,
        result,
        loadError: false,
        history: [result],
      }),
    );
    expect(html).toContain("múltiplos valores plausíveis");
    expect(html).toContain("Motor não informou confiança");
    expect(html).toContain("HOURS 123 HOURS 456");
    expect(html).toContain("meter-normalization-v2");
    expect(html).toContain("Histórico de análises");
    expect(html).toContain("Reprocessar OCR");
  });
  it("oculta texto bruto, histórico e reprocessamento para operadores", () => {
    identity.roles = ["OPERATOR"];
    const html = renderToStaticMarkup(
      createElement(PhotoOcrPanel, {
        readingId: "reading",
        operatorValue: 123,
        result,
        loadError: false,
        history: [result],
      }),
    );
    expect(html).not.toContain("HOURS 123 HOURS 456");
    expect(html).not.toContain("Histórico de análises");
    expect(html).not.toContain("Reprocessar OCR");
  });
  it("informa espera, indisponibilidade e divergência", () => {
    identity.roles = ["SUPERVISOR"];
    const render = (value: PhotoOcrResult | null, loadError = false) =>
      renderToStaticMarkup(
        createElement(PhotoOcrPanel, {
          readingId: "reading",
          operatorValue: 123,
          result: value,
          loadError,
        }),
      );
    expect(render(null)).toContain("Aguardando reconhecimento");
    expect(render(null, true)).toContain("temporariamente indisponível");
    expect(render({ ...result, status: "PROCESSING" })).toContain(
      "Processando",
    );
    expect(
      render({
        ...result,
        matchesOperatorValue: false,
        belowPreviousReading: true,
      }),
    ).toContain("difere da leitura informada");
    expect(render({ ...result, belowPreviousReading: true })).toContain(
      "abaixo da leitura anterior",
    );
  });
});
