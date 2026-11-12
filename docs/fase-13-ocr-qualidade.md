# Fase 13 — diagnóstico inicial do OCR

Base f44650d, merge do PR #22, conferida antes de alterar código em 12/11/2026.

| Área | Classificação | Evidência e decisão |
| --- | --- | --- |
| Evidência original, hash e validação de arquivo | Existente e adequada | PhotoFileValidator limita bytes e inspeciona assinatura e dimensões. Limite de resolução para OCR ainda precisa ser definido. Storage e metadata são imutáveis. |
| Motor | Existente e adequada | AzureAiVisionOcrProvider com HTTPS, managed identity ou segredo e resposta limitada a 1 MB. Fake local não fabrica valores. |
| Extração | Existente, precisa de melhorias | Não aceita milhares brasileiros/internacionais. Rejeita ambiguidades mas não explica nem preserva candidatos. |
| Confiança | Existente, precisa de melhorias | Usa confiança da palavra numérica, mas null acaba em SUCCEEDED. Tornar revisão necessária sem inventar score. |
| Resultados e auditoria | Existente, precisa de melhorias | Histórico append-only com texto bruto restrito. Falta versão de extração, candidatos e motivo de revisão. |
| Comparação | Existente e adequada | Tolerâncias absolutas e percentuais configuráveis. Acrescentar contexto histórico sem alterar confiança visual. |
| Alertas | Existente, precisa de melhorias | Chave por resultado/tipo evita duplicidade no mesmo resultado. Inconclusivo ainda não gera aviso e reprocessamentos podem criar alerta equivalente. |
| Reprocessamento | Existente, precisa de melhorias | Cooldown e lock SQL existem. Acrescentar chave idempotente e recuperação de processamento abandonado. |
| Upload e fila offline | Existente, precisa de melhorias | Upload aguarda reconhecimento. Desacoplar com worker leve que consulta fotos confirmadas sem OCR inicial no mesmo banco. |
| Limite global de OCR | Ausente e necessário | Aplicar concorrência limitada por processo, timeout incluindo espera e lote pequeno. |
| Painel administrativo | Existente, precisa de melhorias | Foto, leitura manual, divergência e reprocessamento já presentes. Acrescentar candidatos, interpretação, histórico e versão. |
| Autorização das fotos | Existente, precisa de melhorias | Permissão operacional permite consultar qualquer foto pelo UUID. Restringir operador à autoria da leitura e manter supervisão. |
| Pré-processamento | Avaliado, sem transformação adicional justificada | Não há corpus autorizado representativo para provar benefício. Preservar bytes originais e usar o tratamento do motor. Não anunciar melhoria de acurácia visual. |

A Fase 13 reutiliza o provider, as entidades, os serviços e a revisão administrativa. A Fase 16 cobre imagens reais e aparelhos físicos. Mudanças de schema serão incrementais e nenhuma migration será aplicada em produção nesta execução.
