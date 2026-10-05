import type { AnomalyType } from "@/types/domain";

export const anomalyDefinitions: readonly {
  type: AnomalyType;
  label: string;
  description: string;
}[] = [
  {
    type: "READING_DECREASE",
    label: "Leitura menor que a anterior",
    description: "Indica redução inesperada no valor acumulado do horímetro.",
  },
  {
    type: "IMPOSSIBLE_HOUR_INCREASE",
    label: "Aumento impossível de horas",
    description: "Indica variação incompatível com o intervalo entre leituras.",
  },
  {
    type: "DUPLICATE_READING",
    label: "Leitura duplicada",
    description: "Indica possível reenvio ou registro repetido.",
  },
  {
    type: "MISSING_READING",
    label: "Leitura ausente",
    description: "Indica máquina sem registro dentro da janela esperada.",
  },
  {
    type: "LOW_OCR_CONFIDENCE",
    label: "Baixa confiança do OCR",
    description: "Indica leitura automática com confiança abaixo do limite.",
  },
  {
    type: "OCR_OPERATOR_DIVERGENCE",
    label: "Divergência entre OCR e operador",
    description: "Indica diferença entre o valor detectado e o confirmado.",
  },
  {
    type: "LATE_SYNC",
    label: "Sincronização atrasada",
    description: "Indica envio posterior ao intervalo operacional esperado.",
  },
] as const;
