# Regras de negócio operacionais — Fase 1

Este documento descreve somente comportamentos presentes na Fase 1 ou decisões explicitamente reservadas. Os valores operacionais configurados são provisórios quando indicado em [decisoes.md](decisoes.md). OCR, QR avançado, armazenamento de evidências, mobile, autenticação e correção formal não são declarados como implementados.

## RN-001 — Imutabilidade da leitura original

- **Objetivo:** preservar a trilha original do horímetro.
- **Condição:** qualquer tentativa da aplicação de atualizar ou excluir uma leitura persistida.
- **Resultado:** o contexto EF Core bloqueia `UPDATE` e `DELETE` para `HourMeterReading`.
- **Observações:** leitura original não é sobrescrita. A regra não cria um fluxo completo de correção nesta fase.

## RN-002 — Horário de recebimento definido pelo servidor

- **Objetivo:** distinguir o horário informado pelo dispositivo do horário confiável de recebimento.
- **Condição:** uma leitura é recebida pela API.
- **Resultado:** `ReceivedAtServer` é obtido pelo `TimeProvider` do servidor e normalizado para UTC; o cliente não o fornece nem o substitui.
- **Observações:** `CapturedAtDevice` continua sendo o horário reportado pelo dispositivo e pode gerar `CLOCK_SKEW`.

## RN-003 — Idempotência por `ClientEventId`

- **Objetivo:** permitir reenvio seguro do mesmo evento pelo cliente.
- **Condição:** chega uma requisição com `ClientEventId` já persistido.
- **Resultado:** a API retorna a leitura existente sem adicionar outra leitura, auditoria ou anomalia.
- **Observações:** existe também índice único no banco para proteger concorrência; a leitura original permanece inalterada.

## RN-004 — Regressão de leitura

- **Objetivo:** detectar redução inesperada do horímetro.
- **Condição:** o valor atual é menor que a leitura válida anterior da máquina.
- **Resultado:** cria `READING_DECREASE`, marca a nova leitura como `SUSPECT` e mantém ambos os registros.
- **Observações:** uma leitura menor não é corrigida ou descartada automaticamente. Situações legítimas precisam ser validadas pela Transjap.

## RN-005 — Incremento implausível

- **Objetivo:** apontar aumento incompatível com o tempo transcorrido.
- **Condição:** o incremento excede o menor limite entre horas plausíveis por tempo decorrido e horas plausíveis por dia, incluindo a tolerância configurada.
- **Resultado:** cria `IMPOSSIBLE_HOUR_INCREASE` e persiste a leitura como `SUSPECT`.
- **Observações:** limite diário, taxa horária e tolerância são configuráveis e provisórios; não representam validação oficial da operação.

## RN-006 — Máquina ativa sem leitura recente

- **Objetivo:** mostrar no dashboard máquinas ativas sem leitura capturada no período configurado.
- **Condição:** máquina `ACTIVE` sem leitura com `CapturedAtDevice` igual ou posterior ao limite UTC (`agora - MissingReadingThresholdDays`).
- **Resultado:** a máquina é contada em `WithoutReading`. Máquina inativa nunca entra na métrica.
- **Observações:** o padrão de dois dias é provisório. A API calcula a métrica; o frontend só apresenta o resultado e não replica a regra. O horário de captura é comparado em UTC; divergência do relógio pode também produzir `CLOCK_SKEW`.

## RN-007 — Unidade do medidor

- **Objetivo:** distinguir horas de quilômetros sem conversão implícita.
- **Condição:** cadastro de máquina tem unidade confirmada.
- **Resultado:** persistir `HOURS` ou `KM` como enum serializado em string.
- **Observações:** unidade pendente é `NULL`; não preencher por inferência baseada no modelo. Leituras e limites de `HOURS` e `KM` não devem ser misturados. A classificação da frota aguarda confirmação da Transjap.

## RN-008 — Correção sem sobrescrever o original

- **Objetivo:** preservar histórico e autoria em eventual correção.
- **Condição:** uma leitura exige correção formal.
- **Resultado:** quando o fluxo for definido e implementado, a correção deverá ser um novo registro vinculado, com motivo, responsável e instante, mantendo o original.
- **Observações:** esta API ainda não implementa solicitação/aprovação de correção; não existe operação para alterar a leitura original. Responsáveis e procedimento aguardam validação.

## RN-009 — Não fabricar leitura ou evidência

- **Objetivo:** não apresentar dado inferido como medição real.
- **Condição:** abertura/fechamento ausente, transição divergente ou evidência não fornecida.
- **Resultado:** o sistema preserva as leituras efetivamente recebidas e não cria `INFERRED`, foto, texto ou outra evidência fabricada.
- **Observações:** tolerância da virada não autoriza síntese de leitura. Evidência de foto não é implementada nesta fase.

## RN-010 — Auditoria de leitura suspeita

- **Objetivo:** manter rastreáveis leituras com anomalias.
- **Condição:** a política classifica a leitura como `SUSPECT`.
- **Resultado:** a leitura é persistida, cada detecção gera uma `Anomaly`, e `AuditLog` registra a criação e a marcação como suspeita.
- **Observações:** suspeita não é sinônimo de exclusão nem de rejeição; a resolução operacional de anomalias não é presumida.

## RN-011 — Divergência do relógio do dispositivo

- **Objetivo:** revelar captura com relógio possivelmente incorreto.
- **Condição:** diferença absoluta entre `CapturedAtDevice` e `ReceivedAtServer` ultrapassa `ClockSkewToleranceSeconds`.
- **Resultado:** gerar `CLOCK_SKEW` e persistir a leitura como `SUSPECT`.
- **Observações:** padrão de 300 segundos é provisório; comparar instantes normalizados em UTC não depende do offset textual recebido.

## RN-012 — Repetição do mesmo evento não duplica

- **Objetivo:** explicitar que retry de transporte não cria nova leitura.
- **Condição:** um evento já processado é reenviado com o mesmo `ClientEventId`.
- **Resultado:** responder com a leitura original existente e manter a contagem de registros inalterada.
- **Observações:** não gera `DUPLICATE_READING`; essa detecção é reservada a um `ClientEventId` diferente (RN-013).

## RN-013 — Possível duplicidade com outro evento

- **Objetivo:** identificar leituras repetidas que parecem eventos distintos.
- **Condição:** mesma máquina, mesmo valor e mesmo instante de `CapturedAtDevice`, mas `ClientEventId` diferente.
- **Resultado:** criar `DUPLICATE_READING`, marcar a nova leitura como `SUSPECT` e preservar as duas.
- **Observações:** a comparação atual exige timestamp exato; não existe janela aproximada de duplicidade e nenhuma é presumida.

## RN-014 — Transição `CLOSING` para `OPENING`

- **Objetivo:** verificar divergência entre fechamento e abertura seguinte.
- **Condição:** uma leitura válida anterior é `CLOSING` e a atual é `OPENING`.
- **Resultado:** diferença absoluta acima de `DayTransitionToleranceHours` cria `DAY_TRANSITION_MISMATCH` e torna a nova leitura `SUSPECT`; dentro ou exatamente na tolerância não cria essa anomalia.
- **Observações:** as duas leituras permanecem preservadas. O padrão de 0,5 hora é provisório. Não criar fechamento inferido, nova leitura ou evidência.

## Status das leituras

| Status | Significado operacional |
| :--- | :--- |
| `VALIDATED` | Passou pelas validações automáticas executadas. Não é uma garantia de confirmação humana da medição. |
| `PENDING` | Aguarda uma etapa posterior; não há nesta fase um fluxo que deva ser presumido para sua resolução. |
| `SUSPECT` | Foi persistida com uma ou mais anomalias para análise; permanece no histórico e na auditoria. |
| `REJECTED` | Não é operacionalmente válida. Valor negativo resulta em rejeição pela política de domínio, mas o endpoint atual valida a entrada e responde `400` sem persistir uma leitura `REJECTED`. Isso é intencional para não registrar valor impossível como leitura operacional. |
| `CORRECTED` | Estado reservado a correção formal futura; não existe fluxo que altere ou substitua o original nesta fase. |

## Auditoria e limites desta fase

- Criação/atualização de máquina (`CREATE_MACHINE`, `UPDATE_MACHINE`), criação/atualização de obra (`CREATE_WORKSITE`, `UPDATE_WORKSITE`) e criação de leitura (`CREATE_READING`) produzem `AuditLog`; logs são append-only na aplicação.
- A auditoria não substitui autenticação ou identificação de operador: o sistema ainda não tem a autenticação da Fase 3.
- Não foi ativada detecção `LATE_SYNC`: não há limiar aprovado. OCR, QR, geofence, fotos e sincronização mobile também não são afirmados como implementados.
