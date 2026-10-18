# FASE 6 — RELATÓRIO DO MODELO COMPLETO DE LEITURA

Data: 18/10/2026. Branch `feat/phase-6-complete-reading-model`. Arquitetura e regras em [fase-6-modelo-completo-leitura.md](fase-6-modelo-completo-leitura.md).

| Item | Entrega / validação |
| --- | --- |
| 1. Modelo anterior | Ids, valor decimal(12,2), tipo/status, três timestamps, ClientEventId, datas de auditoria e relações foto/anomalias reutilizados. |
| 2. Alterações | Snapshot de unidade, autoria, DeviceId, referência anterior/delta e vínculo de correção; projeções e frontend ampliados. |
| 3. Campos finais | Modelo anterior + MeterUnit, CreatedByUserId, CreatedByEmail, DeviceId, PreviousReadingId, DifferenceFromPrevious e CorrectionOfReadingId. |
| 4. MeterUnit | Obrigatório nas novas leituras; histórico desconhecido permanece null, sem copiar retroativamente a máquina. |
| 5. Autoria | ICurrentUser autenticado; campos de autoria no body são rejeitados. FK para usuário com Restrict. |
| 6. DeviceId | Opcional, trim, máximo 100 caracteres e controles rejeitados; sem função de autenticação. |
| 7. ClientEventId | UUID obrigatório, índice único, retry retorna 200 com evento aceito; uso para outra máquina retorna 409. |
| 8. Timestamps | Captura do cliente normalizada UTC; recebimento/sincronização definidos pelo servidor e iguais no aceite online. |
| 9. PreviousReadingId | Seleção SQL VALIDATED da mesma máquina/unidade, captura anterior ou igual, desempate por recebimento e Id; FK Restrict. |
| 10. DifferenceFromPrevious | Calculado pelo servidor e persistido; null sem referência confiável; não recalcula eventos históricos. |
| 11. ReadingType | Enum validado; OPENING, CLOSING, INTERMEDIATE, MAINTENANCE e CORRECTION com regras documentadas. |
| 12. Status | Classificação central e ReadingStatusTransitionPolicy; agregado permanece imutável. |
| 13. Regras HOURS | Aumento impossível considera tempo/tolerância e limite diário; transição fechamento/abertura considera fronteira operacional conhecida. |
| 14. Regras KM | Sem limite físico de horas nem velocidade inventada; redução/duplicação e validações gerais aplicadas. |
| 15. Anomalias | READING_DECREASE, IMPOSSIBLE_HOUR_INCREASE, DUPLICATE_READING, CLOCK_SKEW, sequência e LATE_SYNC configurável; sem inferir WRONG_WORKSITE/scheduler. |
| 16. Correction linkage | Proposta PENDING, supervisão, original da mesma máquina/unidade, sem cadeia/ciclo; original e último valor operacional preservados. |
| 17. API create | Dados exclusivos do cliente, autoria/derivação no servidor, precisão sem arredondamento, transação leitura + anomalias + auditoria. |
| 18. API detail | Contexto completo, snapshot, autoria, dispositivo, timestamps, referências/delta, foto, latest OCR e anomalias/correções. |
| 19. Filtros | machineId, fleetNumber, workSiteId, meterUnit, readingType, status, from/to, hasPhotoEvidence e hasAnomaly, todos em SQL. |
| 20. Frontend | Lista paginada, filtros, badges compartilhados, precisão de duas casas, contexto/linha do tempo, anomalias e propostas no detalhe/histórico da máquina. |
| 21. Migration | 20261018155742_CompleteReadingModel, somente gerada. Sete colunas nullable para compatibilidade histórica; sem recriar tabela/backfill fictício. |
| 22. Índices | FKs de autor/anterior/correção, ordem global de captura/recebimento/Id e por máquina/unidade; unicidade ClientEventId preservada. |
| 23. Testes unitários | 198 aprovados, sem falhas ou ignorados; inclui domínio, serviço, precisão, unidade, autoria, delta, idempotência, sequências, late sync, status e correção imutável. |
| 24. Integração | Testes novos para snapshot, autoria, replay, precisão, segurança do request, correção, filtros, paginação, OCR histórico, migration legada e atomicidade. Validação SQL remota pendente. |
| 25. Regressões | Suíte Fases 1–5 mantida, com fixtures sintéticas de unidade/autor e leituras intermediárias nos testes genéricos. Resultado SQL remoto pendente. |
| 26. Backend build | Restore/build Release concluídos com zero erros/avisos na última compilação completa. |
| 27. Frontend lint | npm ci e npm run lint aprovados. Dependências transitivas emitiram avisos de depreciação. |
| 28. Frontend build | npm run build aprovado; aviso de lockfile fora do repositório, sem bloquear. |
| 29. Branch | feat/phase-6-complete-reading-model. |
| 30. Commit | Principal: d2aaafb — feat: complete operational reading model; ajustes de validação posteriores no histórico da branch. |
| 31. PR | [#10](https://github.com/EduardoStuhr/TransjapHorimetros/pull/10), contra main, sem merge; em rascunho até a validação SQL completa. |
| 32. Riscos restantes | Confirmar unidades oficiais e parâmetros de sequência/atraso, revisar migration/índices, validar SQL e snapshots legados; aprovação de correção, reconciliação e scheduler ficam para fases posteriores. |

O comando completo de testes foi tentado localmente. Naquela rodada, 194 unitários e 43 testes de integração sem dependência do banco passaram; 91 testes de integração não inicializaram por ausência de TEST_SQLSERVER_CONNECTION_STRING/SQL Server/Docker. Não foi considerado sucesso da suíte completa. O workflow de PR existente será utilizado com SQL Server descartável; nenhum banco Azure foi acessado.

MODELO COMPLETO DE LEITURA? **SIM**

METER UNIT HISTÓRICO? **SIM — novos eventos; legado desconhecido permanece null.**

PREVIOUS READING? **SIM**

DELTA CALCULADO PELO SERVIDOR? **SIM**

IDEMPOTÊNCIA? **SIM**

READING DECREASE? **SIM**

IMPOSSIBLE HOUR INCREASE? **SIM — somente HOURS.**

CORREÇÃO IMUTÁVEL PREPARADA? **SIM**

TESTES DE REGRESSÃO FASES 1-5? **NÃO — validação SQL completa pendente.**

MIGRATION APLICADA NO AZURE? **NÃO**

PR MERGEADO? **NÃO**

FASE 6 EM PRODUÇÃO? **NÃO**

FASE 6 PRONTA PARA REVISÃO? **NÃO — validação SQL completa pendente.**
