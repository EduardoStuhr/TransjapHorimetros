# Fase 7 — Offline First e sincronização confiável

## Princípios

A captura segue a regra: **a foto é a prova, o texto é a leitura, o servidor é a verdade**. O cliente pode validar formato e preservar eventos, mas não calcula nem envia como autoridade `MeterUnit`, leitura anterior, diferença, status, anomalias, autoria, `ReceivedAtServer` ou `SyncedAt`.

## Arquitetura

O frontend possui quatro camadas:

- `IndexedDbOfflineStore`: única implementação que acessa IndexedDB;
- `OfflineSyncService`: ordena e processa eventos individualmente, resolve dependências, aplica backoff e persiste transições;
- `BrowserSyncTransport`: envia os contratos explícitos para os endpoints BFF e interpreta HTTP;
- `SyncProvider`: integra usuário autenticado, conectividade, gatilhos automáticos, `BroadcastChannel`, service worker e UI.

As interfaces `OfflineStore` e `SyncTransport` permitem testar o motor sem React ou browser real. A API ganhou apenas `GET /api/v1/offline/reference-data`, que devolve máquinas e obras ativas com os campos mínimos. A fila continua pertencendo ao cliente; nenhuma tabela de sync foi criada.

## IndexedDB

Banco: `transjap-horimetros-offline`  
Versão: `1`

Object stores:

| Store | Chave | Conteúdo |
| --- | --- | --- |
| `events` | `localEventId` | Eventos, estado, dependência, autoria e retry |
| `photos` | `blobKey` | Blob da evidência e metadados de integridade |
| `machines` | `id` | Cache mínimo de máquinas ativas e QR ativo |
| `worksites` | `id` | Cache mínimo de obras ativas |
| `metadata` | `key` | `device-id`, data do cache e identidade pública do último usuário |

O upgrade cria stores e índices sem apagar dados. Leitura, blob e evento de foto são gravados em uma única transação `readwrite`, evitando foto órfã ou leitura sem sua dependência.

Cada evento inclui `schemaVersion: 1`, `localEventId`, `clientEventId`, tipo, payload JSON, `createdAtLocal`, `capturedAtDevice`, status, contador e datas de retry, erro sanitizado, data de sincronização, ID de leitura do servidor e proprietário (`userId`/`userEmail`). Blobs nunca são colocados em `localStorage`.

## IDs e idempotência

`localEventId` identifica o registro no dispositivo. `clientEventId` é o UUID global enviado ao servidor. Ambos são criados uma única vez na transação de confirmação e nunca são regenerados em retry.

Tipos atuais:

- `READING_CREATED`;
- `PHOTO_UPLOADED`.

A foto contém `parentReadingClientEventId` e só é enviada após o evento pai ficar `SYNCED` com `serverReadingId`. O backend já possui índices únicos para `ClientEventId` de leitura e foto e devolve `200` para replay ou `201` para criação. O upload também compara SHA-256 recalculado no servidor e não dispara novo OCR em replay idempotente.

## Captura offline

A tela `/coleta` permite resolver QR pelo cache, escolher máquina/obra ativas, informar leitura, tipo, horário real do dispositivo e tirar/selecionar foto. São validados localmente valor, unidade conhecida, obra, formato e limite de 10 MB. Ao confirmar:

1. obtém o `deviceId` persistente do IndexedDB;
2. calcula SHA-256 com Web Crypto;
3. cria os dois `ClientEventId`;
4. grava leitura, blob e foto dependente atomicamente;
5. informa que o conteúdo está salvo no dispositivo;
6. tenta sincronizar sem depender de `navigator.onLine`.

O QR offline só resolve token ativo ou formato de frota existente no último cache confirmado pelo servidor. Dados desconhecidos nunca são inventados.

## Sincronização

Gatilhos:

- montagem da aplicação autenticada;
- evento `online` do browser, apenas como sinal auxiliar;
- confirmação de nova captura;
- botão **Sincronizar agora**;
- fim do backoff persistido.

O resultado HTTP define conectividade e sucesso. Eventos são considerados em `createdAtLocal ASC`, com `localEventId` como desempate, e processados um a um. Dependências bloqueadas ficam intactas; uma leitura inválida não impede a próxima leitura independente.

Transições persistidas:

- `PENDING` → `SYNCING` → `SYNCED`;
- `SYNCING` → `FAILED_RETRYABLE`;
- `SYNCING` → `FAILED_PERMANENT`.

Um `SYNCING` deixado por fechamento abrupto volta para `FAILED_RETRYABLE` na próxima execução. O backoff é 5 s, 30 s, 2 min, 10 min e 30 min, com teto de 30 min. Retry manual ignora apenas a espera; não altera `ClientEventId`.

## Classificação de erros

| Resultado | Tratamento |
| --- | --- |
| 200 / 201 | sucesso; persiste ID do servidor |
| rede / timeout / 408 / 429 / 500 / 502 / 503 / 504 | `FAILED_RETRYABLE` com backoff |
| 400 / 403 / 404 / 413 / 422 | `FAILED_PERMANENT`, preservado para revisão |
| 401 | preserva evento, pausa a execução e solicita novo login |
| 409 com recurso existente | sucesso idempotente |
| 409 sem recurso | conflito permanente visível |

O erro local é limitado a 300 caracteres, sem stack trace ou headers, e redige padrões de credencial, senha e connection string.

## Autoria, logout e múltiplas abas

Somente eventos cujo `userId` corresponde à sessão atual são sincronizados. Eventos de outro usuário são contados e exibidos, mas não enviados. Logout remove apenas a identidade pública em cache; fila e fotos permanecem.

`navigator.locks` fornece exclusão entre abas. Browsers sem essa API usam lock em memória na aba. `BroadcastChannel` comunica `sync_started`, `sync_finished` e `queue_changed`, atualizando as telas e evitando cliques duplicados óbvios.

## Cache, app shell e segurança

Máquinas e obras carregam `fetchedAt`, versão e `updatedAt` do servidor. A UI mostra a idade do cache. Um service worker progressivo guarda somente os shells explícitos `/coleta` e `/sincronizacao`, chunks versionados do Next e logos. Ele nunca intercepta `/api/*` e não armazena autenticação, respostas de foto privada ou tokens.

IndexedDB não protege dados contra alguém com acesso ao perfil do dispositivo. Por isso não são persistidos JWT, refresh token, senha, cookies, headers ou secrets. A identidade pública em cache permite abrir o formulário já carregado durante queda de rede, mas não concede permissão no servidor.

## Limpeza

Após sucesso confirmado, o blob pode ser removido depois de 5 minutos. Metadados `SYNCED` são mantidos por 7 dias para diagnóstico e depois coletados. Eventos pendentes, retryable ou permanentes nunca são removidos pelo garbage collector.

## Limitações conhecidas

- O service worker é melhoria progressiva: o dispositivo deve abrir a aplicação online ao menos uma vez para armazenar o shell e os chunks atuais.
- Sem `navigator.locks`, a coordenação entre abas é informativa, não distribuída perfeitamente; a idempotência do servidor continua sendo a última defesa.
- Leituras antigas que chegam fora de ordem não reescrevem histórico anterior. O backend aplica as regras da Fase 6 no momento do recebimento.
- Eventos `FAILED_PERMANENT` ainda não possuem editor administrativo; a Fase 7 apenas os preserva e sinaliza para revisão.
- Background Sync API não é requisito. A sincronização principal funciona com a aplicação aberta.

## Validação e Pull Request

- **Pull Request da Fase 7:** [PR #11 — feat: implement offline first synchronization](https://github.com/EduardoStuhr/TransjapHorimetros/pull/11), mergeado em `main`.
- **Correção de CI:** a execução pós-implementação falhou inicialmente no backend porque o banco `transjap_horimetros_tests` ainda não existia. O [PR #12 — fix: bootstrap integration test database in CI](https://github.com/EduardoStuhr/TransjapHorimetros/pull/12) adicionou o bootstrap seguro; sua execução #11 validou backend e frontend com sucesso.
- **Testes automatizados frontend:** 34 testes Vitest (`sync-transport.test.ts`, `indexed-db-store.test.ts`, `sync-engine.test.ts`).
- **Testes unitários backend:** 198 testes unitários aprovados em `TransjapHorimetros.UnitTests`.
- **Testes de integração backend:** 153 testes aprovados com SQL Server descartável no PR #12.
- **Workflow de CI:** `Validate Transjap Horimetros` (`.github/workflows/ocr-validation.yml`).
