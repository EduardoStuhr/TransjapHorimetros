# Relatório da Fase 9 — Identidade de dispositivo

## Escopo entregue

A entrega implementa identidade assimétrica local, cadastro pendente, administração restrita, desafio-resposta descartável, sessão curta vinculada a usuário e dispositivo, vínculo server-side em novas leituras e uploads de fotos de evidência, e preservação integral do fluxo offline. A Fase 10 não foi iniciada.

## Componentes alterados

- **Domain**: `RegisteredDevice`, `DeviceChallenge`, `RegisteredDeviceStatus`, FK opcional `RegisteredDeviceId` em `HourMeterReading`.
- **Application**: modelos e DTOs (incluindo `CurrentDeviceStatusResponse`), serviço de ciclo de vida, validação SPKI, canonicalização, validação de desafio/assinatura, validação de vínculo de dispositivo no upload de evidência fotográfica (`IPhotoEvidenceService` e `PhotoEvidenceService`), suporte a chave dedicada de assinatura (`DeviceIdentityOptions.SessionSigningKey`) e auditoria segura.
- **Infrastructure**: configurações EF, repositório, token curto HMAC (`DeviceSessionTokenService` com suporte a `SessionSigningKey` dedicada e fallback para JWT key) e migration `AddRegisteredDeviceIdentity`.
- **API**: endpoints `/api/v1/devices`, endpoint de status minimizado `GET /api/v1/devices/status`, proteção de foto com `X-Device-Session` em `POST /api/v1/readings/{readingId}/photo`, policy `CanManageDevices` e rate limit dedicado.
- **Web/PWA**: `DeviceIdentityProvider`, Web Crypto, IndexedDB v2, gate de coleta em `/coleta`, envio de `X-Device-Session` tanto no reading quanto no photo upload (`BrowserSyncTransport`), propagação no route handler Next.js, e tela administrativa `/dispositivos`.
- **Testes**: ciclo de vida, sessão/tamper/expiração, P-256, chave não exportável, upgrade v1→v2, transporte sem confiança no snapshot, integração completa do desafio e matriz completa de segurança de 20 cenários de backend e testes offline de revogação no frontend.

## Controles verificados

| Controle | Resultado |
| --- | --- |
| Chave privada não exportável | Coberto por teste Web Crypto (`extractable: false`) |
| Chave privada fora de API/log/localStorage | Confirmado por revisão dos contratos e DTOs |
| Cadastro idempotente | Mesmo ID/chave retorna o cadastro; conflitos de chave/ID são rejeitados com `409 Conflict` |
| Autorização administrativa | Somente `MANAGER`/`ADMIN` na policy `CanManageDevices` |
| Operador sem poder de autorização | Coberto por integração: `403 Forbidden` |
| Desafio aleatório, curto e one-time | Nonce 256 bits criptográfico, 120 s, consumo SQL atômico |
| Replay de desafio | Segundo consumo retorna `409 Conflict` |
| Assinatura inválida | Desafio consumido, registrado na auditoria e `403 Forbidden` |
| Sessão de dispositivo expirada | Rejeitada com `403 Forbidden` na criação de leitura e upload de foto |
| Sessão vinculada a usuário | Token emitido para User A rejeitado se usado por User B |
| Revogação contra sessão já emitida | Status do dispositivo é consultado no banco durante a validação de novas leituras/fotos (`403 Forbidden`) |
| Foto protegida por DeviceSession | `POST /api/v1/readings/{readingId}/photo` exige `X-Device-Session` válido |
| Foto vinculada ao dispositivo da leitura | Device da sessão deve ser idêntico ao `RegisteredDeviceId` da leitura; mismatch rejeitado com `403` |
| Compatibilidade de foto histórica | Leituras legadas (`RegisteredDeviceId == null`) aceitam upload sem quebrar histórico (sem backfill) |
| Identidade declarada pelo cliente | Snapshot local `deviceIdentity` é removido do POST; FK vem exclusivamente do servidor |
| Forjamento de RegisteredDeviceId | Rejeitado por `JsonUnmappedMemberHandling.Disallow` no backend |
| Status endpoint minimizado | `GET /api/v1/devices/status` retorna `CurrentDeviceStatusResponse` sem expor emails administrativos |
| Chave de assinatura dedicada | Suporte a `DeviceIdentity:SessionSigningKey` com fallback transparente para `Authentication:Jwt:SigningKey` |
| Fila/fotos no upgrade IndexedDB | Teste real de IndexedDB v1→v2 preservando todos os dados |
| Fila de pendente | Retryable, sem descarte de leituras ou fotos |
| Fila de revogado offline | Marcada como permanente/revisável (`FAILED_PERMANENT`), sem apagar evento local ou blob de foto |
| LastSeen | Throttle configurável de cinco minutos para evitar writes concorrentes excessivos |
| Auditoria sem material secreto | Snapshots excluem chave privada, assinaturas, tokens de sessão e JWTs |

## Matriz de Cenários de Teste Backend (20 Cenários Obrigatórios)

1. `RegisterSameIdSameKey_IsIdempotent`: Retorna o dispositivo existente com status original.
2. `RegisterSameIdDifferentKey_Returns409`: Conflito ao tentar reutilizar LocalDeviceId com chave diferente.
3. `RegisterSameKeyDifferentId_Returns409`: Conflito ao tentar registrar mesma chave com outro LocalDeviceId.
4. `PendingDevice_CannotCreateChallenge`: Dispositivo pendente recebe 403 ao solicitar desafio.
5. `ChallengeExpires`: Desafio além da janela de 120s rejeitado com 409 Conflict.
6. `ChallengeReplay_Returns409`: Segundo verify com o mesmo nonce falha com 409.
7. `WrongSignature_Returns403`: Assinatura incorreta consome desafio e retorna 403.
8. `TamperedPayloadSignature_Fails`: Payload alterado rejeitado na verificação criptográfica.
9. `DeviceSessionExpires`: Sessão expirada rejeitada com 403 em leitura e foto.
10. `DeviceSessionBelongsToUser`: Token emitido para usuário A rejeitado para usuário B.
11. `RevokedDeviceRejectsOldSession`: Dispositivo revogado tem sessão antiga rejeitada na leitura.
12. `ReadingPersistsRegisteredDeviceId`: Leitura persiste FK `RegisteredDeviceId` resolvida server-side.
13. `ClientCannotSetRegisteredDeviceId`: Envio de `registeredDeviceId` no corpo falha na desserialização.
14. `PhotoRequiresDeviceSession`: Upload de foto para leitura associada a dispositivo exige `X-Device-Session`.
15. `PhotoDeviceMustMatchReadingDevice`: Device B tentando anexar foto a leitura do Device A recebe 403.
16. `RevokedDeviceCannotUploadPendingPhoto`: Dispositivo revogado após leitura não consegue enviar foto pendente.
17. `OperatorCannotManageDevices`: Operador recebe 403 ao tentar autorizar, revogar ou renomear dispositivos.
18. `ManagerCanManageDevices`: Gerente autoriza e gerencia dispositivos com sucesso.
19. `AdminCanManageDevices`: Administrador autoriza e gerencia dispositivos com sucesso.
20. `RevokedDeviceCannotBeReauthorized`: Dispositivo revogado não pode ser reautorizado.

## Migration

`20261022121009_AddRegisteredDeviceIdentity` contém a coluna anulável `registered_device_id` em `hour_meter_readings`, as tabelas `registered_devices` e `device_challenges`, índices únicos (`local_device_id`, `public_key_fingerprint`, `nonce`), e chave estrangeira `RESTRICT`. Não há alteração de migrations anteriores, delete, backfill, seed ou aplicação em Azure.

## Diagnóstico confirmado em 29/10/2026

- `main` local e remota estavam limpas e em `79c0c3bbae1df4d0e22927d67c547437c20fffd0`; nenhuma divergência. A correção parte dessa main em `fix/phase-9-final-ci`.
- [PR #16](https://github.com/EduardoStuhr/TransjapHorimetros/pull/16) incorporado; [PR #17](https://github.com/EduardoStuhr/TransjapHorimetros/pull/17) fechado sem merge e sem reaproveitamento direto de branch.
- [CI anterior](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/39832164288): frontend aprovado, backend com 214 unitários aprovados e integração com 150 aprovados / 26 falhas / zero ignorados. Os deploys verdes de main não foram considerados prova de integração.
- Reprodução local do código base: **214 unitários aprovados; integração SQL com 151 aprovados e 25 falhas em 176 testes**. A diferença de uma falha é o teste de QR: timestamps congelados empatam e o desempate por GUID pode passar ou falhar.
- O log detalhado de cadastro revelou `InvalidOperationException` em `ModelMetadata.ThrowIfRecordTypeHasValidationOnProperties`: DataAnnotations estavam direcionadas a propriedades de records posicionais. O erro ocorria antes da action; **CreatedAtAction e sua rota não eram a causa**. O mesmo defeito afetava cadastro, desafio, verificação e renomeação.
- O relógio congelado fora da suíte de dispositivos impedia a passagem do cooldown mesmo após `Task.Delay(1100)`, afetando as quatro variações de reprocessamento, a concorrência de OCR e a unidade histórica; também empatava a emissão dos QR codes.
- A nova prova de concorrência inicialmente passou e depois reproduziu uma falha (191/192 integrações aprovadas): o commit de outro cadastro entre a consulta por ID e a consulta por fingerprint levava ao conflito indevido para a mesma identidade. Corrigido com retorno idempotente também nesse intervalo; duas variações unitárias determinísticas preservam o 409 para outro ID.
- A revisão encontrou exposição de metadados administrativos no retry de cadastro e na resposta de verificação; status já usava DTO mínimo. Encontrou ainda classificação permanente do bloqueio por cadastro ausente/pendente e ausência de controle de concorrência na transição de status do dispositivo.

A localização correta dos atributos é descrita na [documentação oficial de model binding de records](https://learn.microsoft.com/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0#record-types-validation-and-binding-metadata).

## Correções e arquivos desta finalização

| Arquivos | Correção / justificativa |
| --- | --- |
| `RegisteredDeviceModels.cs`, `RegisteredDevicesController.cs` | DataAnnotations nos parâmetros dos records; DTO mínimo para cadastro e sessão. Mantidos 201/200/409 e CreatedAtAction. |
| `RegisteredDeviceService.cs` | Idempotência também quando um cadastro concorrente confirma entre as duas consultas de identidade; mesmo DTO mínimo de status em todas as respostas acessíveis ao operador; nova consulta sem tracking após persistência, antes de emitir sessão. |
| `RegisteredDeviceConfiguration.cs`, `TransjapRepository.cs` | Status existente como token de concorrência otimista; uma autorização obsoleta não sobrescreve revogação já confirmada. Conflito traduzido para 409, sem alterar schema. |
| `RegisteredDeviceRepository.cs` | Desafio expira também na fronteira exata: `ExpiresAt > now`. |
| `DeviceIdentityOptions.cs` | Inicialização rejeita desafio fora de 1–300 segundos, sessão fora de 1–15 minutos e throttle fora de 1–60 minutos. Mantidas validações de enforcement e chave. |
| `TransjapApiFactory.cs`, novo `DeviceIdentityApiFactory.cs` | Relógio real para fixtures anteriores; relógio congelado exclusivo para identidade, resetado entre testes. |
| `RegisteredDeviceIntegrationTests.cs`, `RegisteredDeviceTests.cs` | Contratos HTTP e Location real; entradas inválidas; privacidade; idempotência concorrente; consumo concorrente; expiração exata; revogação durante verificação; autorização obsoleta; fotos históricas. |
| `device-identity-error.ts`, `device-identity-provider.tsx`, `types.ts` | Bloqueio temporário gera erro retryable; revogação/identidade quebrada continuam permanentes. Tipos frontend acompanham DTO mínimo. |
| `sync-engine.test.ts`, `sync-transport.test.ts` | Fila e blob preservados para dispositivo pendente/sem cadastro; retomada após autorização com o mesmo ClientEventId. |
| `package.json`, `package-lock.json` | Next.js e eslint-config-next 16.4.3; Zod 4.6.7. Correções dos alertas críticos/altos encontrados na auditoria. |
| Documentação da Fase 9 e este relatório | Diagnóstico, evidências, comandos, contagens e limites reais da validação. |

Não foram alterados workflows de deploy, migrations, schema SQL, implementação/versão do IndexedDB ou regras operacionais de OCR/QR. O teste com duas instâncias de DbContext confirma que não há mudanças pendentes no modelo de migrations após configurar concorrência otimista.

## Validação local desta finalização

Ambiente: .NET SDK 10.0.401, Node 24.21.0, Next.js 16.4.3; CI oficial usa Node 26. SQL Server 2022 em container Docker dedicado, porta publicada somente em loopback e banco estritamente `transjap_horimetros_tests`, conforme o workflow oficial. Não houve acesso a banco Azure.

Comandos executados:

```bash
dotnet restore apps/api/TransjapHorimetros.sln
dotnet build apps/api/TransjapHorimetros.sln -c Release --no-restore
# TEST_SQLSERVER_CONNECTION_STRING configurada somente para o container descartável local.
dotnet test apps/api/TransjapHorimetros.sln -c Release --no-build --logger trx
npm ci
npm run lint
npm test
npm run build
npm audit --omit=dev --json
git diff --check
git status
git diff --stat
```

| Verificação | Resultado |
| --- | --- |
| Restore / build .NET Release | Aprovados; zero warnings e zero erros |
| Unitários .NET | 223 aprovados, zero falhas, zero ignorados |
| Integração SQL Server | 192 aprovados, zero falhas, zero ignorados |
| npm ci | Aprovado com lockfile atualizado |
| Lint | Aprovado |
| Frontend | 92 aprovados em 9 arquivos, zero falhas |
| Build frontend | Aprovado em Next.js 16.4.3 |
| Auditoria npm de produção | Zero alertas críticos/altos; dois moderados transitivos, descritos abaixo |
| Integridade do diff | Sem erros de whitespace; sem alterações em Azure, migrations ou IndexedDB |

A integração cobre a matriz original de segurança e as novas regressões. OCR preserva cooldown, uma única reserva concorrente, histórico, valor e unidade históricos. QR preserva os dois registros e a desativação do anterior. Os testes offline cobrem revogação antes/depois da leitura, blobs preservados, idempotência, retentativas, isolamento por usuário, Web Crypto não exportável e upgrade real de IndexedDB v1→v2 via fake-indexeddb.

Todos os testes automatizados do repositório foram executados, sem exclusão/filtro na validação final. Não foram executados ensaios manuais em celular físico, OCR Azure real ou testes de atestado de hardware; esses resultados não são inferidos dos testes Node/SQL. Nenhum deploy ou migration remota foi realizado.

## GitHub Actions e veredito

A integração obrigatória é `.github/workflows/ocr-validation.yml`, **Validate Transjap Horimetros**, com jobs `backend` (restore/build/unitários/SQL Server) e `frontend` (npm ci/lint/test/build). Os workflows de deploy de main não devem rodar nesta branch.

**FASE 9 PRONTA PARA APROVAÇÃO**, com evidência de CI completa no [PR #18](https://github.com/EduardoStuhr/TransjapHorimetros/pull/18), sem merge ou deploy.

A [execução 40936258717](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/40936258717) aprovou o commit de implementação/documentação inicial `82f22bc9b49802c730af678c90229f0d55b6152a`:

| Job | Resultado e evidência |
| --- | --- |
| [backend](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/40936258717/job/123667475908) | SUCCESS: restore, build Release, 223 unitários e 192 integrações SQL Server; zero falhas e zero ignorados; artefatos TRX publicados |
| [frontend](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/40936258717/job/123667475294) | SUCCESS: npm ci, lint, 92 testes e build Next.js 16.4.3 em Node 26 |

Esta atualização registra o resultado e altera somente este relatório. A aprovação do commit que efetivamente será incorporado exige também a execução verde correspondente ao HEAD final; ela deve ser conferida na [aba Checks do PR](https://github.com/EduardoStuhr/TransjapHorimetros/pull/18/checks). O SHA final e o link da execução desse HEAD são entregues no encerramento da tarefa, após verificação individual dos jobs.

São **507 testes aprovados** (223 + 192 + 92), sem falhas ou testes automatizados ignorados. Os workflows de deploy não foram acionados e main permanece em `79c0c3bbae1df4d0e22927d67c547437c20fffd0`.

## Riscos residuais e operação

- **Retenção de Desafios**: Desafios expirados e consumidos permanecem na tabela `device_challenges` para proteção contra replay; política de expurgo / retenção periódica será tratada em fase futura.
- **Conectividade para Prova**: Primeiro cadastro e emissão de `DeviceSession` exigem conectividade de rede; durante operação offline a coleta é permitida se o dispositivo já estava previamente autorizado.
- **Revogação Offline**: Dispositivo revogado no servidor enquanto offline apenas terá as requisições bloqueadas ao retornar a conectividade, momento no qual a fila local é protegida e marcada para revisão manual sem exclusão de dados ou blobs.
- **Limpeza do Navegador**: Limpar o armazenamento do navegador (IndexedDB) exclui o par de chaves Web Crypto gerado localmente, exigindo nova solicitação de registro pelo operador e autorização pelo gestor.
- **Atestado de Hardware**: A identidade comprova a posse da chave privada assimétrica não exportável gerada no Web Crypto do navegador, mas não constitui atestado de hardware (TPM/Secure Enclave).


## Auditoria de dependências e riscos residuais adicionais

O `npm audit` original encontrou alertas em Next.js e Zod. Foram atualizados para versões corrigidas conforme os avisos [Next.js / otimização de imagem](https://github.com/advisories/GHSA-2464-fhr4-w9xw), [Next.js / Server Actions](https://github.com/advisories/GHSA-p3m5-59mp-jv5r), [Next.js / use cache](https://github.com/advisories/GHSA-673q-mjm6-r5xr) e [Zod](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5).

Restam dois pacotes classificados como moderados pelo npm, `exceljs` e seu `uuid` transitivo, derivados do mesmo [aviso sobre buffers em UUID v3/v5/v6](https://github.com/advisories/GHSA-w5hq-g745-h8pq). A importação encontrada em `exceljs/lib/xlsx/xform/sheet/cf-ext/cf-rule-ext-xform.js` usa somente `v4`, fora dos métodos descritos no aviso. A identidade de dispositivo usa `crypto.randomUUID`/Web Crypto, sem esse pacote. Não foi imposto downgrade ou override de versão principal em exportação de planilhas; a dependência transitiva permanece uma pendência de manutenção registrada, sem falha observada no fluxo da Fase 9.

Uma revogação concorrente após a última consulta da verificação pode fazer uma resposta de sessão chegar já inválida. As novas operações online consultam o estado atual e rejeitam esse token; a consulta não deve ser descrita como uma transação única envolvendo toda a requisição. Dados capturados offline continuam preservados para revisão. Limpar os dados do navegador continua removendo a chave; não há promessa de recuperação dessa chave ou atestado físico do dispositivo.
