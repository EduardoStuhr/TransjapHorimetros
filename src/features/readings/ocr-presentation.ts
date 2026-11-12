export function ocrReviewReason(reason: string | null | undefined) {
  const labels: Record<string, string> = {
    SELECTED: "Um valor foi identificado no contexto do medidor.",
    CONFIDENCE_MISSING:
      "Confiança não informada pelo motor. Confirme o valor na fotografia.",
    LOW_CONFIDENCE:
      "O escore do motor ficou abaixo do mínimo configurado. Revisão necessária.",
    AMBIGUOUS_FORMAT:
      "O separador pode indicar decimal ou milhar. Confirme o formato na fotografia.",
    AMBIGUOUS_VALUES:
      "Há múltiplos valores plausíveis. É necessária confirmação humana.",
    UNLABELLED_CONTEXT:
      "Os números encontrados não têm contexto suficiente para identificar o medidor.",
    TOO_MANY_CANDIDATES:
      "A imagem contém muitos números para uma seleção segura.",
    NO_VALUE: "Nenhum valor válido de medidor foi identificado.",
    UNIT_MISMATCH: "A unidade encontrada não corresponde à unidade da máquina.",
    OCR_INTERRUPTED:
      "A análise anterior foi interrompida. A supervisão pode reprocessar a evidência.",
  };
  return reason
    ? (labels[reason] ?? "A análise precisa de verificação pela supervisão.")
    : "Análise histórica sem classificação de qualidade.";
}
