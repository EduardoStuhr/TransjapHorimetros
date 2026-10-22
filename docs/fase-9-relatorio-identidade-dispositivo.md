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
| Revogação contra sessão já emitida | Status do dispositivo é relido atonicamente antes de aceitar leitura ou foto (`403 Forbidden`) |
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

## Validação executada

- `npm run lint`: aprovado com 0 erros;
- `npm test`: aprovado com 82 testes passando em 9 arquivos;
- `npm run build`: aprovado com Next.js 16.3.8 (todas as 14 páginas estáticas/dinâmicas otimizadas);
- `dotnet build TransjapHorimetros.sln -c Release`: validado com zero warnings e zero erros;
- Unitários .NET em Release: aprovados com 206 testes unitários;
- Integração SQL local: marcada como `NÃO VALIDADO LOCALMENTE — CI EXECUTARÁ` (executada pelo workflow de PR com container descartável SQL Server 2022).

## Riscos residuais e operação

- **Retenção de Desafios**: Desafios expirados e consumidos permanecem na tabela `device_challenges` para proteção contra replay; política de expurgo / retenção periódica será tratada em fase futura.
- **Conectividade para Prova**: Primeiro cadastro e emissão de `DeviceSession` exigem conectividade de rede; durante operação offline a coleta é permitida se o dispositivo já estava previamente autorizado.
- **Revogação Offline**: Dispositivo revogado no servidor enquanto offline apenas terá as requisições bloqueadas ao retornar a conectividade, momento no qual a fila local é protegida e marcada para revisão manual sem exclusão de dados ou blobs.
- **Limpeza do Navegador**: Limpar o armazenamento do navegador (IndexedDB) exclui o par de chaves Web Crypto gerado localmente, exigindo nova solicitação de registro pelo operador e autorização pelo gestor.
- **Atestado de Hardware**: A identidade comprova a posse da chave privada assimétrica não exportável gerada no Web Crypto do navegador, mas não constitui atestado de hardware (TPM/Secure Enclave).
