---
name: transjap-domain-architecture
description: Aplicar as regras arquiteturais e de negócio oficiais do sistema Transjap Horímetros. Usar em qualquer decisão de backend, frontend, mobile, banco, sincronização offline, QR Code, evidência fotográfica, OCR, anomalias, relatórios, auditoria, segurança ou modelagem relacionada ao projeto.
---

# Transjap Domain Architecture

Tratar esta skill como fonte das invariantes do Transjap Horímetros. Priorizar integridade, evidência e rastreabilidade sobre conveniência de implementação.

## Aplicar os princípios

Seguir sempre:

~~~text
Offline First
Evidence First
Audit Everything
API First
Server Validates
Never Trust Client
Never Delete History
Human in the Loop
~~~

Usar o servidor como fonte oficial. Tratar o armazenamento local do app como origem temporária até a sincronização.

## Respeitar papéis

- Permitir ao operador autenticar, ler QR, fotografar, confirmar valor, informar obra e sincronizar.
- Permitir ao supervisor revisar inconsistências, validar correções e acompanhar pendências.
- Permitir ao administrativo consultar, filtrar, conferir e emitir relatórios.
- Permitir ao gestor acompanhar indicadores e dados consolidados.
- Permitir ao administrador gerenciar usuários, permissões, máquinas, QR Codes e configurações.

## Modelar máquinas e QR Codes

Modelar Machine com Id, FleetNumber, Model, Manufacturer, Status, CreatedAt e UpdatedAt. Tratar FleetNumber como número oficial da frota.

Modelar QR separadamente:

~~~text
MachineQrCode
- Id
- MachineId
- Token
- Active
- CreatedAt
- RevokedAt
~~~

Identificar a máquina pelo QR, impedir substituição silenciosa e permitir revogação sem excluir a máquina. Preservar compatibilidade com QR Codes existentes.

## Modelar leituras

Usar HourMeterReading como entidade central:

~~~text
Id
MachineId
OperatorId
WorkSiteId
Value
ReadingType
Status
CapturedAtDevice
ReceivedAtServer
SyncedAt
ClientEventId
CreatedAt
~~~

Aceitar os tipos OPENING, CLOSING, INTERMEDIATE, MAINTENANCE e CORRECTION.

Usar os status:

~~~text
VALIDATED
PENDING
SUSPECT
REJECTED
CORRECTED
~~~

Não apagar a leitura original ao corrigir. Registrar correção, motivo, autor e data.

## Preservar evidências

Exigir preferencialmente uma foto para cada leitura. Nunca guardar apenas o número nem substituir a foto silenciosamente.

Modelar:

~~~text
PhotoEvidence
- Id
- ReadingId
- StoragePath
- Hash
- MimeType
- Size
- CreatedAt
~~~

Armazenar separadamente valor confirmado pelo operador, valor detectado pelo OCR e confiança. Tratar OCR como auxiliar; nunca substituir automaticamente a evidência ou a decisão humana sem regra explícita.

## Implementar o fluxo offline

Seguir o fluxo:

~~~text
QR → Foto → Valor → Obra → Salvar localmente
→ Criar ClientEventId → PENDING_SYNC
→ Sincronizar quando houver internet → Servidor confirmar
~~~

- Não bloquear o trabalho de campo por falta de rede.
- Impedir perda do registro ao fechar o app.
- Exibir claramente pendente e sincronizado.
- Gerar UUID único para cada evento.
- Impedir duplicação por reenvio com constraint única no servidor.
- Não permitir edição silenciosa depois da sincronização.

## Tratar datas corretamente

Guardar CapturedAtDevice, ReceivedAtServer e SyncedAt sem substituir uma pela outra. Considerar que o relógio do celular pode estar incorreto e que a sincronização pode ocorrer horas depois.

## Validar no servidor

Ao receber uma leitura, validar nesta ordem lógica:

~~~text
usuário → permissão → máquina → QR → obra → clientEventId
→ valor → leitura anterior → intervalo → evidência → OCR → anomalias
~~~

Nunca considerar a validação do cliente suficiente.

## Detectar anomalias

Criar eventos rastreáveis para:

- READING_DECREASE: leitura menor que a anterior; tratar troca de horímetro como exceção explícita.
- IMPOSSIBLE_HOUR_INCREASE: delta incompatível com o tempo transcorrido.
- DUPLICATE_READING: evento ou leitura repetida.
- MISSING_READING: máquina ativa sem registro dentro de janela configurável.
- LOW_OCR_CONFIDENCE: confiança abaixo do limiar.
- OCR_OPERATOR_DIVERGENCE: diferença relevante entre OCR e operador.
- LATE_SYNC: atraso relevante de sincronização.

Não interpretar automaticamente atraso de sincronização como fraude. Permitir revisão humana e manter todos os dados originais.

## Auditar operações

Registrar eventos como:

~~~text
USER_LOGIN
MACHINE_CREATED
QR_REVOKED
READING_CREATED
READING_REVIEWED
READING_CORRECTED
READING_REJECTED
REPORT_EXPORTED
USER_PERMISSION_CHANGED
~~~

Nunca apagar histórico crítico nem usar cascade delete sem justificativa arquitetural.

## Produzir relatórios e dashboard

Deixar explícito quando um relatório inclui registros suspeitos. Em Excel, considerar frota, modelo, obra, data, horímetro anterior e atual, diferença, operador, status e anomalia.

Em PDF, considerar período, totais, máquinas sem leitura, anomalias, horas por máquina e resumo executivo.

Preparar fechamento diário com máquinas ativas, atualizadas, sem leitura, suspeitas e pendentes de sync. Priorizar no dashboard pendências operacionais, não apenas totais.

## Orientar cada cliente

No frontend, exibir status e anomalias sem mascaramento, mostrar origem e horário, abrir a foto associada e diferenciar valor informado de OCR.

No mobile, usar QR como fluxo principal, câmera integrada, banco local e fila de sincronização.

No banco, aplicar foreign keys, constraints únicas, timestamps, auditoria, índices por frota/data/status e UUIDs.

## Resolver conflitos

- Entre facilidade e integridade, escolher integridade.
- Entre automação e evidência humana, manter a evidência e permitir revisão.
- Entre correção rápida e rastreabilidade, manter rastreabilidade.
