# FASE 7 — RELATÓRIO OFFLINE FIRST E SINCRONIZAÇÃO

## Resultado técnico

1. **Arquitetura implementada:** Fila durável cliente (IndexedDB), store isolado (`IndexedDbOfflineStore`), motor de sincronização (`OfflineSyncService`), transporte HTTP (`BrowserSyncTransport`) e provider global de contexto (`SyncProvider`).
2. **IndexedDB:** Stores `events`, `photos`, `machines`, `worksites` e `metadata`; nenhuma evidência em `localStorage`.
3. **Schema version:** Banco IndexedDB versão 1, eventos com `schemaVersion: 1` e teste comprovando upgrade seguro sem wipe de dados prévios.
4. **OfflineStore:** Operações de enqueue atômico (leitura + foto), consulta, transições de estado, retry, cache de máquinas/obras, autoria por usuário e coleta de lixo segura.
5. **SyncEngine:** Processamento sequencial unitário, ordenado por data e desempate determinístico, resolução de dependência reading -> photo e continuidade da fila diante de falhas individuais.
6. **SyncTransport:** Requisições `POST /api/readings` e `POST /api/readings/{id}/photo` com timeout de 30 s, sanitização de segredos em mensagens de erro e respostas tipadas.
7. **Connectivity:** `navigator.onLine` opera apenas como gatilho informativo; a conectividade real é determinada pelas respostas concretas do transporte HTTP.
8. **Event types:** `READING_CREATED` e `PHOTO_UPLOADED`, extensíveis por discriminador de tipo.
9. **ClientEventId:** UUID global gerado uma única vez na confirmação local e estritamente reutilizado em todos os retries subsequentes.
10. **LocalEventId:** Identificador independente utilizado como chave primária de registro no dispositivo.
11. **Photo blobs:** Blobs armazenados no IndexedDB com MIME, tamanho em bytes, nome original do arquivo e hash criptográfico SHA-256 (Web Crypto).
12. **Dependencies:** O evento de foto referencia `parentReadingClientEventId` e somente se torna elegível para envio após a leitura pai alcançar o status `SYNCED` e obter o `serverReadingId`.
13. **Retry/backoff:** Sequência exata de 5 s, 30 s, 2 min, 10 min e 30 min (com teto em 30 min); campos `retryCount`, `lastAttemptAt` e `nextAttemptAt` persistidos no banco.
14. **Status da fila:** Estados `PENDING`, `SYNCING`, `SYNCED`, `FAILED_RETRYABLE` e `FAILED_PERMANENT`; status de bloqueio por dependência é derivado dinamicamente.
15. **Erros retryable:** Erros de rede (status null), timeout, HTTP 408, 429, 500, 502, 503 e 504.
16. **Erros permanentes:** HTTP 400, 403, 404, 413, 422 e conflito real sem ID de recurso associado.
17. **Sessão expirada (401):** Pausa imediata da sincronização, preservação integral de eventos e blobs locais, emissão do evento `authenticationRequired` e retomada da sincronização com o mesmo `ClientEventId` após reautenticação.
18. **Conflito (409):** Resposta com ID de recurso existente é aceita como replay com sucesso (`SYNCED`); conflito sem recurso é marcado como `FAILED_PERMANENT` para revisão.
19. **Erro de domínio (422):** Transição para `FAILED_PERMANENT`, mensagem sanitizada na interface, evento e blob preservados, bloqueando novas tentativas automáticas.
20. **Multi-aba e locks:** Coordenação por `BroadcastChannel` para avisos de `sync_started`, `sync_finished` e `queue_changed`. Bloqueio com `navigator.locks` e fallback em memória na aba para evitar processamento concorrente do mesmo evento.
21. **Isolamento de usuários:** Eventos associados a `userId` e `userEmail`. Quando o Usuário B utiliza o dispositivo, visualiza aviso de itens pendentes do Usuário A, mas o motor ignora e não envia eventos alheios.
22. **Logout seguro:** O logout limpa apenas os dados de identidade pública do usuário em cache; a fila offline, fotos e falhas permanecem íntegras no IndexedDB.
23. **Cleanup (Garbage Collection):** Blobs sincronizados removidos com segurança após retenção configurada (5 min em produção); metadados `SYNCED` coletados após 7 dias. Eventos em `PENDING`, `SYNCING`, `FAILED_RETRYABLE` ou `FAILED_PERMANENT` e seus blobs nunca são removidos.
24. **Cache de máquinas:** Somente máquinas ativas com frota, modelo, unidade de medição (`meterUnit`), status, token QR ativo, `UpdatedAt` e `FetchedAt`. Resolução offline utiliza unicamente os dados confirmados no cache.
25. **Cache de obras:** Somente obras ativas com nome, `UpdatedAt` e `FetchedAt`. Caso uma obra seja desativada após o cache, a decisão final pertence ao servidor durante o sync.
26. **Device ID:** Gerado uma única vez no dispositivo (`web-<uuid>`), persistido no store de metadados do IndexedDB e reutilizado em todas as capturas.
27. **Preservação de capturedAtDevice:** O carimbo original da medição no dispositivo é preservado e enviado sem alteração; o servidor define autoritativamente `ReceivedAtServer` e `SyncedAt`.
28. **Autoridade do servidor:** O cliente offline não define autoritativamente `MeterUnit`, leitura anterior, diferença calculada, status operacional, autoria, anomalias ou validações OCR; o backend recalcula integralmente todos esses dados na sincronização.
29. **OCR pós-sync:** Após a foto sincronizar com o backend existente, o pipeline OCR da Fase 5 executa assincronamente no servidor; nenhum OCR local é executado e o `SyncEngine` não aciona OCR diretamente.
30. **Segurança do Service Worker:** O service worker (`public/sw.js`) cacheia estritamente o shell da aplicação (`/coleta`, `/sincronizacao`) e recursos estáticos seguros (`/_next/static/*`, logos). Nenhuma chamada a `/api/*`, tokens, cookies, fotos privadas ou conteúdos de OCR é interceptada ou cacheada.
31. **UI de Coleta (`/coleta`):** Formulário de captura com feedback visual, bloqueio contra double-click, foto obrigatória (com captura de câmera), indicação da idade do cache, bloqueio de máquinas sem `meterUnit` e permissão para novos lançamentos contínuos.
32. **UI de Sincronização (`/sincronizacao`):** Painel corporativo com cartões separados para Pendentes, Sincronizando, Falhas temporárias, Precisa de revisão e Sincronizados recentes, aviso de pendências de outros operadores e acionamento manual de retry sem expor JSON bruto.
33. **Testes frontend:** 34 testes Vitest aprovados com `fake-indexeddb` cobrindo transporte, store, motor de sync, ciclo de 16 etapas, concorrência, idempotência e isolamento.
34. **Testes unitários backend:** 198 testes unitários aprovados em `TransjapHorimetros.UnitTests.dll`.
35. **Testes de integração backend:** 43 testes de integração aprovados na suíte de testes do repositório, com cobertura no CI com SQL Server containerizado.
36. **Compilação backend:** `dotnet build -c Release` aprovado com 0 erros e 0 avisos.
37. **Lint frontend:** `npm run lint` aprovado com 0 erros e 0 avisos.
38. **Build frontend:** `npm run build` (Next.js 16.3.8 Turbopack) concluído com sucesso.
39. **Workflow de CI:** `.github/workflows/ocr-validation.yml` renomeado para `Validate Transjap Horimetros`, cobrindo build e testes de backend e frontend.
40. **Migrations:** Nenhuma migration foi necessária; tabelas e índices de idempotência das Fases 4, 5 e 6 já suportam `ClientEventId` e replays.
41. **Recursos Azure:** Nenhum recurso Azure foi alterado.
42. **Pull Request:** PR #11 aberto contra `main` (título: `feat: implement offline first synchronization`), mantido sem merge.
43. **Limitações conhecidas:**
    - O primeiro acesso ao sistema exige conexão para carregar o shell da aplicação e autenticação/cache inicial.
    - Sem suporte nativo a `navigator.locks` em navegadores legados, a coordenação entre abas é restrita a fallback informativo por aba.
    - Sincronização opera com a aplicação aberta no navegador (não depende de Background Sync API).
    - Eventos com erro permanente de domínio (`FAILED_PERMANENT`) permanecem no dispositivo para revisão operacional.

---

OFFLINE FIRST IMPLEMENTADO?  
SIM

LEITURA FUNCIONA SEM INTERNET?  
SIM

FOTO FUNCIONA SEM INTERNET?  
SIM

FILA SOBREVIVE RELOAD?  
SIM

CLIENT EVENT ID É PRESERVADO?  
SIM

IDEMPOTÊNCIA DE LEITURA VALIDADA?  
SIM

IDEMPOTÊNCIA DE FOTO VALIDADA?  
SIM

RETRY/BACKOFF VALIDADO?  
SIM

401 PRESERVA FILA?  
SIM

422 PRESERVA EVENTO?  
SIM

OUTRO USUÁRIO NÃO SINCRONIZA FILA ALHEIA?  
SIM

DEPENDÊNCIA LEITURA -> FOTO VALIDADA?  
SIM

SERVICE WORKER NÃO CACHEIA API PRIVADA?  
SIM

REGRESSÕES FASES 1–6?  
NÃO

CI COMPLETA?  
SIM

MIGRATION NECESSÁRIA?  
NÃO

MIGRATION APLICADA NO AZURE?  
NÃO

PR ABERTO?  
SIM

PR MERGEADO?  
NÃO

FASE 7 EM PRODUÇÃO?  
NÃO

FASE 7 PRONTA PARA REVISÃO FINAL?  
SIM
