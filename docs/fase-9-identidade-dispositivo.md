# Fase 9 — Identidade confiável de dispositivo

## Objetivo e limites

A Fase 9 acrescenta uma identidade criptográfica ao navegador usado em campo sem substituir a identidade do usuário. O JWT continua provando **quem** está autenticado; a chave local e o desafio-resposta provam **qual dispositivo autorizado** está enviando uma nova leitura.

Esta entrega não implementa fingerprinting, GPS, biometria, push, aplicativo nativo, MDM ou atestado de hardware. Também não altera Azure, não aplica migrations remotamente e não inicia a Fase 10.

## Modelo de confiança

`RegisteredDevice` possui um ciclo de vida explícito e sem exclusão física:

| Estado | Coleta | Sincronização | Transição permitida |
| --- | --- | --- | --- |
| `PENDING` | bloqueada | aguarda autorização | `AUTHORIZED` ou `REVOKED` |
| `AUTHORIZED` | permitida, inclusive offline pelo último estado conhecido | exige prova online | `REVOKED` |
| `REVOKED` | bloqueada após o estado ser conhecido | rejeitada de forma permanente/revisável | nenhuma reautorização silenciosa |

Gerentes e administradores podem listar, filtrar, pesquisar, renomear, autorizar, revogar e consultar o histórico em `/dispositivos`. Operadores podem somente solicitar cadastro e consultar o estado do identificador local. A API, e não a interface, aplica as policies.

Pedidos repetidos com o mesmo `LocalDeviceId` e a mesma chave retornam o cadastro existente. O mesmo ID com outra chave ou a mesma chave com outro ID retorna conflito. Um dispositivo revogado nunca volta a autorizado por retry ou novo login. Limpar os dados do navegador remove a chave e gera uma nova identidade, que precisa de nova autorização.

## Chave local e formato interoperável

O navegador gera `ECDSA` na curva `P-256` com `crypto.subtle.generateKey`:

- chave privada com `extractable=false`, uso exclusivo `sign`;
- chave privada persistida como `CryptoKey` no IndexedDB por structured clone;
- chave pública exportada como DER `SubjectPublicKeyInfo` e transportada em Base64;
- algoritmo identificado por `ECDSA_P256_SHA256`;
- fingerprint administrativo: SHA-256 do SPKI normalizado, em hexadecimal minúsculo;
- assinatura: ECDSA/SHA-256 no formato fixo IEEE P1363 (`r || s`, 64 bytes), verificada no .NET com `DSASignatureFormat.IeeeP1363FixedFieldConcatenation`.

A chave privada não é exportada, serializada em JSON, enviada à API, gravada em `localStorage`, exibida na interface, auditada ou registrada em log. Logout preserva a identidade do dispositivo e remove somente a sessão do usuário.

Referências técnicas: [Web Cryptography Level 2](https://www.w3.org/TR/WebCryptoAPI/) e [DSASignatureFormat no .NET](https://learn.microsoft.com/dotnet/api/system.security.cryptography.dsasignatureformat).

## IndexedDB e upgrade

O banco `transjap-horimetros-offline` passa da versão 1 para a versão 2. O upgrade cria somente o object store `device_identity`, com a entrada `current`. Os stores anteriores — `events`, `photos`, `machines`, `worksites` e `metadata` — não são limpos, renomeados ou recriados.

A entrada de identidade contém versão de schema, IDs local/registrado, nome, plataforma ampla (`WEB_PWA`), algoritmo, SPKI público, `CryptoKey` privada, fingerprint, último status conhecido e datas de validação. Eventos novos guardam um snapshot local do dispositivo autorizado para diagnóstico; esse snapshot nunca é enviado como identidade confiável à API.

O teste de upgrade abre um banco v1 real, grava evento, foto e metadado, reabre pela implementação v2 e confirma a preservação integral antes de criar a identidade.

## Cadastro e administração

Endpoints autenticados:

- `POST /api/v1/devices/registration`: cadastro idempotente, sempre inicialmente `PENDING`; novo cadastro retorna 201 com `Location` consultável, retry retorna 200 e conflitos retornam 409;
- `GET /api/v1/devices/status`: consulta minimizada por `LocalDeviceId` retornando `CurrentDeviceStatusResponse` (sem expor e-mails ou dados administrativos ao operador);
- `POST /api/v1/devices/challenge`: emite desafio para dispositivo autorizado;
- `POST /api/v1/devices/challenge/verify`: consome desafio, verifica assinatura e emite sessão curta;
- `GET /api/v1/devices`: lista administrativa paginada com status e busca;
- `GET /api/v1/devices/{id}/history`: histórico administrativo derivado da auditoria;
- `POST /api/v1/devices/{id}/authorize`: autorização;
- `POST /api/v1/devices/{id}/revoke`: revogação irreversível no escopo atual;
- `PATCH /api/v1/devices/{id}`: renomeação.

Solicitante, e-mail e timestamps vêm do usuário e relógio do servidor. A API valida que o SPKI recebido é realmente ECDSA P-256 e calcula o fingerprint após normalizá-lo. Chave pública completa não é devolvida nos DTOs administrativos. Cadastro (inclusive retry autorizado/revogado), status e a propriedade `device` da verificação de desafio retornam o DTO estrito `CurrentDeviceStatusResponse`, expondo apenas Id, LocalDeviceId, DisplayName, Platform, Status, KeyAlgorithm, PublicKeyFingerprint, LastSeenAt e RequestedAt, protegendo metadados de auditoria e e-mails de outros usuários contra vazamento no perfil de operador.

## Desafio-resposta e sessão curta

Cada desafio tem nonce aleatório de 256 bits, validade padrão de 120 segundos e consumo SQL atômico único. Na fronteira exata de expiração ele já é rejeitado (`ExpiresAt > now` é obrigatório). O payload UTF-8 é retornado pronto e assinado sem reconstrução pelo cliente:

```text
TRANSJAP-DEVICE-CHALLENGE
v1
<challenge-id-guid>
<registered-device-id-guid>
<nonce-base64url>
<expires-at-ISO-8601-O>
```

O desafio é consumido antes da verificação, portanto uma assinatura inválida também inutiliza o nonce. Replay, expiração, assinatura inválida, dispositivo pendente e dispositivo revogado são rejeitados. Sucesso emite uma credencial HMAC de sessão de dispositivo, vinculada ao `RegisteredDeviceId` e ao `UserId`, com validade padrão de cinco minutos e armazenamento somente em memória no frontend. A assinatura do token de sessão utiliza preferencialmente `DeviceIdentity:SessionSigningKey`; caso não esteja configurada, recorre de forma segura a `Authentication:Jwt:SigningKey`.

Ao criar uma leitura e ao enviar uma foto, a API valida simultaneamente JWT, sessão curta e status atual do dispositivo. Uma sessão emitida antes da revogação deixa de funcionar imediatamente porque o status é relido no banco. A verificação também relê o estado sem tracking após persistir a auditoria, antes de emitir a sessão. O status é token de concorrência otimista do EF: uma autorização carregada antes de uma revogação não consegue sobrescrever o estado revogado, retornando conflito. Isso usa a coluna existente, sem migration ou alteração de schema. Se uma revogação concorrer após a última consulta da verificação, uma credencial eventualmente retornada já estará inutilizável nas novas operações online. Sessões são estritamente vinculadas ao usuário autenticado: um token de sessão emitido para o Usuário A não pode ser utilizado pelo Usuário B. `LastSeenAt` é atualizado no máximo uma vez a cada cinco minutos. Sucessos são auditados uma vez por desafio; tentativas inválidas são auditadas pelo desafio consumido, evitando flood por retry do mesmo nonce.

Cadastro, emissão e verificação usam rate limit específico, além do limitador global.

## Leituras, fotos e compatibilidade histórica

`hour_meter_readings.registered_device_id` é uma FK anulável com `ON DELETE RESTRICT`:

- linhas históricas continuam nulas e não recebem backfill;
- o corpo de `CreateReadingRequest` não aceita `RegisteredDeviceId` por causa de `JsonUnmappedMemberHandling.Disallow`;
- o servidor resolve o dispositivo a partir da sessão assinada e grava a FK;
- o `DeviceId` textual anterior permanece como snapshot legado;
- retries de um `ClientEventId` já aceito continuam devolvendo o evento imutável existente.

### Proteção de fotos por DeviceSession e vínculo com a leitura

O upload de foto em `POST /api/v1/readings/{readingId}/photo` exige o cabeçalho `X-Device-Session`:
1. Valida o JWT do usuário autenticado.
2. Carrega a leitura associada.
3. Se a leitura possui `RegisteredDeviceId`, valida a sessão do dispositivo e verifica se o `RegisteredDeviceId` comprovado na sessão é idêntico ao `RegisteredDeviceId` da leitura (`Reading.RegisteredDeviceId == Session.RegisteredDeviceId`). Caso contrário, retorna `403 Forbidden` e nenhum blob ou registro é gravado.
4. Confirma que o dispositivo permanece no status `AUTHORIZED`; dispositivos revogados após a criação da leitura têm o upload da foto rejeitado com `403 Forbidden`.
5. Leituras históricas (`RegisteredDeviceId == null`) mantêm compatibilidade sem quebra, permitindo anexar fotos sem forçar vínculo inexistente.

A migration `AddRegisteredDeviceIdentity` cria apenas as novas tabelas, índices, FKs e a coluna anulável. Não contém `UPDATE`, seed ou inferência de identidade passada.

## Offline, revogação e preservação da fila

Um dispositivo com último estado local `AUTHORIZED` pode continuar capturando leitura e foto offline. O evento e o blob são gravados atomicamente como antes. Ao reconectar, cada leitura e upload de foto obtém uma sessão de dispositivo em memória antes do envio.

- cadastro ausente ou pendente gera falha retryable: a fila permanece e pode sincronizar depois da autorização;
- revogação conhecida ou recebida durante o sync (403 Forbidden) move o evento para falha permanente/revisável (`FAILED_PERMANENT`), com mensagem clara;
- quando o dispositivo é revogado enquanto offline, a leitura vira `FAILED_PERMANENT`, a foto dependente permanece `PENDING` (bloqueada pela dependência), o blob local permanece intacto e nenhum dado local é apagado;
- quando a revogação ocorre entre a leitura e a foto, a leitura é sincronizada, mas o upload da foto recebe 403 e a foto vira `FAILED_PERMANENT`; o blob da foto continua rigorosamente preservado no IndexedDB;
- fotos dependentes permanecem bloqueadas e preservadas quando a leitura é recusada;
- cleanup continua removendo somente dados já confirmados (`SYNCED`);
- eventos de outro usuário continuam isolados pelo `userId` da fila;
- logout não apaga chave, fila, fotos ou caches.

## Auditoria e dados sensíveis

São registrados `DEVICE_REGISTRATION_REQUESTED`, `DEVICE_AUTHORIZED`, `DEVICE_REVOKED`, `DEVICE_RENAMED`, `DEVICE_CHALLENGE_VERIFIED` e `DEVICE_CHALLENGE_FAILED`. Snapshots de auditoria incluem IDs, estado, atores, datas, algoritmo e fingerprint, mas nunca a chave pública completa, chave privada, assinatura ou token de sessão.

## Configuração

Seção `DeviceIdentity`:

```json
{
  "EnforceForReadings": true,
  "ChallengeLifetimeSeconds": 120,
  "SessionLifetimeMinutes": 5,
  "LastSeenThrottleMinutes": 5,
  "SessionSigningKey": ""
}
```

A inicialização rejeita janelas fora dos limites: desafio de 1–300 segundos, sessão de 1–15 minutos e throttle de 1–60 minutos. Uma chave dedicada configurada precisa ter ao menos 32 bytes UTF-8; na ausência dela, a chave JWT também é validada na inicialização.

`EnforceForReadings=false` existe exclusivamente para fixtures legadas de integração que testam regras anteriores isoladamente. Produção usa `true` por padrão. `SessionSigningKey` permite segregar a chave de assinatura de sessões de dispositivo do secret do JWT de autenticação; quando vazio, o sistema utiliza com segurança a chave JWT configurada.

## Riscos residuais e operação futura

- **Retenção de desafios**: O histórico de `device_challenges` expirados/consumidos cresce cumulativamente. A retenção/cleanup periódica dessa tabela será tratada em fase posterior para evitar acúmulo desnecessário de linhas sem comprometer a integridade de auditoria.
- **Segregação de secret no Azure**: A chave dedicada `DeviceIdentity:SessionSigningKey` está implementada e pronta; a configuração do segredo dedicado no Azure Key Vault/App Service será realizada quando as variáveis de ambiente de produção forem ajustadas na esteira adequada, sem alteração de Azure nesta fase.
- O primeiro cadastro e toda comprovação para sincronização exigem internet.
- Um dispositivo autorizado offline só descobre revogação quando reconecta; sua fila continua íntegra.
- Limpar o armazenamento do site perde a chave e exige nova solicitação.
- A credencial curta fica somente em memória e é renovada por desafio conforme necessário.
- A identidade comprova posse da chave do perfil do navegador; não promete atestado de hardware.


## Regressões e validação final de 29/10/2026

Os atributos DataAnnotations dos records de entrada ficam nos parâmetros do construtor primário, conforme o model binding do ASP.NET Core. O uso anterior de `[property: ...]` lançava `InvalidOperationException` antes da action e provocava HTTP 500 em cadastro, desafio, verificação e renomeação; `CreatedAtAction` não era a causa. O teste do contrato segue o `Location` real retornado no cadastro.

As fixtures legadas usam `TimeProvider.System`. Somente `DeviceIdentityApiFactory` congela o relógio para testes de expiração e o reseta entre testes. O congelamento global anterior impedia o cooldown de OCR de avançar e empatava timestamps de emissões de QR, tornando a ordem dependente de IDs aleatórios. As regras de OCR, QR, captura, foto e IndexedDB permanecem preservadas.

O frontend distingue bloqueio temporário (sem cadastro/pendente) de revogação/identidade inválida. O primeiro mantém a fila em `FAILED_RETRYABLE`, com foto dependente e blob intactos; a mesma fila retoma após autorização, preservando `ClientEventId`. Consulte os comandos, contagens, limites da validação e referências de CI no [relatório final](fase-9-relatorio-identidade-dispositivo.md).
