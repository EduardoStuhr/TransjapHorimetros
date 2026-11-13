export const utilizationReasons: Record<string, string> = {
  CALCULATED: "Calculado",
  MISSING_INITIAL: "Sem leitura inicial no período",
  CROSSES_PERIOD: "Intervalo cruza o período",
  DUPLICATE_TIME: "Capturas no mesmo instante",
  REVIEW_REQUIRED: "Aguardando decisão de revisão",
  CORRECTION_BOUNDARY: "Leitura corrigida ou substituída",
  UNIT_NOT_HOURS: "Unidade diferente de HOURS",
  NEGATIVE_OR_RESET: "Diferença negativa ou reinício",
  SITE_UNCERTAIN: "Obra do intervalo indeterminada",
};
export function utilizationReason(reason: string) {
  return utilizationReasons[reason] ?? reason;
}
