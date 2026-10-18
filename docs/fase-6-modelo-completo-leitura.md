# Fase 6 — Modelo completo de leitura operacional

A foto é a prova, o texto é a leitura, o servidor é a verdade. Esta fase consolida o evento operacional para o futuro fluxo offline, sem implementar fila, sincronização offline, autenticação forte de dispositivo, scheduler ou aprovação administrativa completa.

## Auditoria do modelo anterior

`HourMeterReading` já possuía Id, MachineId, WorkSiteId, Value, ReadingType, Status, CapturedAtDevice, ReceivedAtServer, SyncedAt, ClientEventId, CreatedAt, UpdatedAt, relação com anomalias e PhotoEvidence. O valor já utilizava decimal(12,2), ClientEventId tinha índice único e o DbContext bloqueava alterações/exclusão de leituras. Esses elementos foram reutilizados.

Faltavam unidade histórica, autoria diretamente na leitura, identificação de dispositivo, referência anterior/delta e vínculo de correção. Os novos campos são nullable no banco para representar registros legados sem inventar informação. Novas leituras operacionais exigem unidade e autoria válidas na Application/domínio.

## Campos e autoridade

| Campo | Origem e regra |
| --- | --- |
| Id | Guid gerado pelo servidor |
| MachineId | Cliente referencia uma máquina existente |
| WorkSiteId | Cliente opcional; servidor exige existência e obra ativa |
| Value | Cliente; não negativo, até 9999999999,99, no máximo duas casas decimais |
| MeterUnit | Snapshot imutável de Machine.MeterUnit; HOURS ou KM |
| ReadingType | Cliente, enum validado: OPENING/CLOSING/INTERMEDIATE/MAINTENANCE/CORRECTION |
| Status | Policy do servidor; sem campo editável no request |
| CapturedAtDevice | Cliente; normalizado para UTC; não é tratado como horário confiável do servidor |
| ReceivedAtServer | Relógio UTC do servidor |
| SyncedAt | Recebimento efetivo online; igual a ReceivedAtServer nesta fase |
| ClientEventId | UUID obrigatório do cliente, único globalmente |
| CreatedByUserId / CreatedByEmail | ICurrentUser autenticado; nunca dados do body |
| DeviceId | Cliente opcional; trim, vazio vira null, máximo 100 caracteres, controles rejeitados |
| PreviousReadingId | Leitura escolhida pelo servidor; FK Restrict |
| DifferenceFromPrevious | Value atual menos Value da referência escolhida; null sem referência |
| CorrectionOfReadingId | Cliente indica o original em uma proposta CORRECTION; servidor valida vínculo |
| CreatedAt / UpdatedAt | Servidor; permanecem imutáveis com o registro |

O request rejeita propriedades desconhecidas. Assim, tentativas de enviar Status, MeterUnit, autoria, delta, timestamps do servidor, OCR ou anomalias recebem 400. Valor ausente também recebe 400; zero só é aceito quando explicitamente informado. A validação decimal rejeita precisão excedente em vez de depender de arredondamento SQL. JSON inválido, NaN e infinito não são valores decimal aceitos.

`DeviceId` é apenas uma informação declarada pelo cliente, sem poder de autenticação. Não existe OperatorId/OperatorName persistido duplicado: no frontend, o nome legado de exibição usa o e-mail armazenado de autoria.

## Unidade histórica e dados antigos

Uma máquina sem MeterUnit não aceita uma nova leitura operacional: a API retorna 422 com orientação para configurar HOURS ou KM. A leitura copia a unidade na criação e não passa a refletir alterações posteriores do cadastro da máquina. O mesmo snapshot é usado pelo OCR, inclusive no reprocessamento.

As leituras anteriores à migration permanecem com MeterUnit/autoria/referência/delta null. Não se copia a unidade atual da máquina retroativamente, pois ela não prova a unidade do evento passado. Esses registros continuam consultáveis e a interface informa a ausência de unidade histórica. Não são utilizados como referência de delta de uma unidade conhecida. Qualquer backfill futuro exige fonte confiável e revisão própria; esta migration não o executa.

## Idempotência e transação

Depois de autenticar e validar o UUID do evento, a API procura ClientEventId antes de consultar configuração mutável de máquina ou obra. Um evento já aceito retorna 200 e o registro original, sem recalcular delta, repetir anomalias ou criar auditoria adicional. O payload de um retry não substitui o evento aceito. Reutilizar o evento para outra máquina retorna 409.

O índice único `ux_hour_meter_readings_client_event_id` resolve a corrida entre requisições concorrentes. Em conflito de unicidade, as alterações locais são descartadas e o registro vencedor é consultado. Novos eventos retornam 201.

Leitura, anomalias e auditorias são adicionadas ao mesmo UnitOfWork e persistidas em um único SaveChanges transacional do EF Core. Uma falha no insert da auditoria não deixa leitura ou anomalia parcial. O resultado da comparação e o vínculo anterior ficam persistidos, sem reconstrução retroativa silenciosa.

## Referência anterior e ordenação

A referência é a leitura VALIDATED da mesma máquina e snapshot de unidade, com captura menor ou igual à captura atual, excluindo CORRECTION. Ordenação: CapturedAtDevice DESC, ReceivedAtServer DESC, Id DESC, com desempate executado pelo SQL Server. Registros SUSPECT, PENDING, REJECTED e originais legados CORRECTED não são escolhidos como referência válida.

O servidor registra exatamente a referência observada na consulta de criação. Eventos atrasados podem ser inseridos entre leituras existentes, mas não reescrevem vínculos ou deltas já gravados. Requisições de eventos distintos simultâneos podem observar a mesma referência; serialização/reconciliação histórica completa não faz parte desta fase.

Listagem e histórico da máquina usam a mesma ordem estável. A listagem mantém paginação no SQL; a tela Horímetros utiliza páginas de 20 registros e preserva os filtros na navegação.

## Regras de domínio

`HourMeterReadingPolicy` centraliza classificação/anomalias; `HourMeterReading.CreateOperational` valida os invariantes de criação. O construtor de compatibilidade histórica é interno, disponível para fixtures de teste. Não existe método público de alteração do valor/status no agregado imutável.

| Regra | Comportamento |
| --- | --- |
| READING_DECREASE | Valor menor que o anterior gera anomalia e SUSPECT, tanto em HOURS como KM |
| IMPOSSIBLE_HOUR_INCREASE | Somente HOURS; delta compara limites por tempo transcorrido e por dia, incluindo tolerância configurada |
| KM | Não recebe limite de horas nem velocidade presumida; redução/duplicação e regras gerais continuam válidas |
| DUPLICATE_READING | Mesmo valor e instante de captura na mesma máquina com outro ClientEventId; sinaliza sem rejeitar o registro |
| CLOCK_SKEW | Diferença absoluta entre captura e recebimento superior à tolerância configurada |
| LATE_SYNC | Atraso positivo entre captura e recebimento superior ao threshold, quando explicitamente configurado |
| DAY_TRANSITION_MISMATCH | Aberturas consecutivas sem encerramento conhecido; fechamento sem abertura quando exigido; diferença entre fechamento e abertura acima da tolerância de HOURS |
| WRONG_WORKSITE | Não é inventada: não há atribuição operacional determinística máquina/obra suficiente nesta fase |
| MISSING_READING | Modelo/timestamps permitem detecção posterior; dashboard existente mantém seu indicador, sem novo scheduler ou criação periódica de anomalias |

A seleção de abertura/fechamento considera a última fronteira VALIDATED da mesma máquina/unidade, mesmo que existam registros intermediários ou de manutenção entre elas. Fechamento sem abertura não é obrigatório por padrão; essa exigência depende da configuração operacional. Abertura após fechamento com diferença dentro da tolerância é aceita. As regras de sequência não presumem um turno obrigatório por data civil. As tolerâncias numéricas de horímetro não são aplicadas a KM.

Repetir o valor em outro instante pode ser legítimo e não gera DUPLICATE_READING automaticamente. Clock skew e atraso de sync podem coexistir e não provam qual relógio estava errado. Anomalias operacionais preservam a leitura suspeita. OCR continua auxiliar, sem alterar Reading.Value ou seu status.

## Configuração operacional

| Opção HourMeterRules | Padrão | Significado |
| --- | --- | --- |
| ClockSkewToleranceSeconds | 300 | Limite já existente para diferença de relógios |
| LateSyncThresholdSeconds | null | Desativado até a operação definir atraso aceitável; quando configurado, inteiro positivo |
| DetectConsecutiveOpenings | true | Detecta aberturas repetidas sem fechamento conhecido |
| RequireOpeningBeforeClosing | false | Ativa exigência explícita de abertura antes de fechamento |
| MaxPlausibleHoursPerElapsedHour | 1 | Somente HOURS |
| MaxPlausibleDailyHours | 24 | Somente HOURS |
| ElapsedTimeToleranceHours | 0.25 | Tolerância de incremento de HOURS |
| DayTransitionToleranceHours | 0.5 | Diferença entre fechamento e abertura de HOURS |

As opções são validadas na inicialização. Variáveis seguem o formato `HourMeterRules__Nome`; `.env.example` documenta a ativação opcional de late sync. Não foi inventado um limite padrão de atraso, velocidade ou obra.

## Status e correção imutável

`ReadingStatusTransitionPolicy` centraliza transições permitidas: PENDING → VALIDATED/SUSPECT/REJECTED; SUSPECT → VALIDATED/REJECTED; VALIDATED → CORRECTED. Outras transições, inclusive repetição do mesmo estado, são rejeitadas. Esta policy é uma regra para o fluxo de revisão futuro, não uma autorização para alterar linhas imutáveis pelo EF.

Leituras normais começam VALIDATED ou SUSPECT conforme avaliação. Uma CORRECTION é uma **proposta PENDING**, registrada apenas por SUPERVISOR, ADMINISTRATIVE, MANAGER ou ADMIN. Ela exige um original existente, da mesma máquina e snapshot de unidade. Não pode apontar para uma correção, outra máquina, unidade incompatível ou um registro original sem unidade histórica comprovada. Referências cíclicas são evitadas pela criação de um novo Id, validação do original e checks contra autorreferência.

O original não tem valor nem status alterados; propostas não viram o último valor operacional da máquina. O detalhe mostra os vínculos e a situação pendente. Aprovação, status efetivo CORRECTED, justificativas e eventos de revisão ficam para o workflow administrativo posterior, que deverá preservar o original. Esta fase não implementa edição disfarçada de correção.

## API e performance

POST `/api/v1/readings` mantém CanRecordReading para OPERATOR, SUPERVISOR, ADMINISTRATIVE, MANAGER e ADMIN. Propostas de correção exigem adicionalmente papel de supervisão na Application. Consultas preservam as policies atuais.

GET `/api/v1/readings/{id}` inclui campos operacionais, autoria/dispositivo, três timestamps, unidade histórica, referências/delta, HasPhotoEvidence derivado da relação, resumo do latest OCR, contagens de anomalias abertas/totais, detalhes das anomalias e correções vinculadas.

GET `/api/v1/readings` mantém paginação e aceita machineId, fleetNumber, workSiteId, meterUnit, readingType, status, from, to, hasPhotoEvidence e hasAnomaly. Todos os filtros são traduzidos para SQL. HasAnomaly considera qualquer anomalia; a contagem de abertas é apresentada separadamente.

Listagens carregam contagens e o resumo do latest OCR, sem arrays de anomalias/correções. `ReadingSummaryRepository` faz projeção SQL com agregações e último OCR; no detalhe, adiciona consultas em lote dos vínculos e anomalias, sem N+1. O histórico da máquina reutiliza os mesmos DTOs e mapeamentos. Propostas CORRECTION ficam no histórico, mas não substituem a leitura operacional mais recente.

GET nunca chama o provider OCR, lê o Blob ou recalcula o histórico. O endpoint de latest OCR agora consulta somente o primeiro resultado ordenado no SQL, em vez de carregar o histórico completo.

## Auditoria e frontend

CREATE_READING registra ReadingId, MachineId, Value, MeterUnit, ReadingType, WorkSiteId, ClientEventId, DeviceId, CreatedByUserId/Email, PreviousReadingId, DifferenceFromPrevious, CorrectionOfReadingId, Status e timestamps. correlationId e ator também são campos da auditoria existente. Tokens e secrets não entram no snapshot.

A lista e o histórico da máquina mostram valor, unidade, tipo, diferença, obra, captura, status, foto e contagem de alertas. Badges de status/unidade são compartilhados e seguem a paleta corporativa. Valores exibem até duas casas decimais para não ocultar a precisão aceita pelo servidor.

O detalhe organiza leitura, máquina/contexto operacional, linha do tempo, evidência/OCR, anomalias e vínculos de correção. “Valor informado” é distinto de “Valor detectado na imagem”. A supervisão vê um resumo de rastreabilidade; não existe endpoint novo para exportar payload completo de auditoria.

## Migration e implantação

`20261018155742_CompleteReadingModel` acrescenta somente os sete campos ausentes, índices e FKs/checks. A tabela não é recriada e os dados anteriores não são apagados. FKs de autor, referência anterior e correção usam Restrict. Há índices para referências e ordenação global/por máquina e unidade. As migrations anteriores permanecem intactas.

A migration foi **somente gerada**, sem aplicação no Azure ou database update de produção. Na revisão, verificar plano de execução, custo de índices, janela de migration e necessidade de confirmação de unidade das máquinas antes de liberar novas leituras. A configuração existente de startup pode aplicar migrations quando habilitada; não implantar esta branch antes da revisão.

O workflow existente `ocr-validation.yml` foi reutilizado e renomeado na interface para “Validate operational readings”. Continua sendo exclusivamente de PR, com SQL Server descartável e sem deploy. Fixtures de teste atribuem HOURS às máquinas sintéticas; o seed de produção permanece sem unidade presumida.

## Riscos e limites

Ainda dependem de validação operacional: unidades oficiais das máquinas, exigência de abertura/fechamento e limiar de late sync. Dados legados continuam incompletos quando não existe prova histórica. O offline completo, reconciliação concorrente, workflow de aprovação, autenticação forte de dispositivo, geofence e scheduler de ausência permanecem fora do escopo.

Resultados reais de build/testes, branch, commit e PR estão no [relatório da Fase 6](fase-6-relatorio-modelo-leitura.md).
