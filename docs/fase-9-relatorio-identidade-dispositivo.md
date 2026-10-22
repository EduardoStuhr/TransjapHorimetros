# Relatório da Fase 9 — Identidade de dispositivo

## Escopo entregue

A entrega implementa identidade assimétrica local, cadastro pendente, administração restrita, desafio-resposta descartável, sessão curta vinculada a usuário e dispositivo, vínculo server-side em novas leituras e preservação do fluxo offline. A Fase 10 não foi iniciada.

## Componentes alterados

- Domain: `RegisteredDevice`, `DeviceChallenge`, `RegisteredDeviceStatus` e FK opcional em `HourMeterReading`.
- Application: modelos, serviço de ciclo de vida, validação SPKI, canonicalização, desafio e auditoria.
- Infrastructure: configurações EF, repositório, token curto HMAC e migration `AddRegisteredDeviceIdentity`.
- API: endpoints `/api/v1/devices`, policy `CanManageDevices` e rate limit dedicado.
- Web/PWA: `DeviceIdentityProvider`, Web Crypto, IndexedDB v2, gate de coleta, prova antes do sync e `/dispositivos`.
- Testes: ciclo de vida, sessão/tamper/expiração, P-256, chave não exportável, upgrade v1→v2, transporte sem confiança no snapshot e integração completa do desafio.

## Controles verificados

| Controle | Resultado |
| --- | --- |
| Chave privada não exportável | Coberto por teste Web Crypto |
| Chave privada fora de API/log/localStorage | Confirmado por revisão dos contratos |
| Cadastro idempotente | Mesmo ID/chave retorna o cadastro; conflitos são rejeitados |
| Autorização administrativa | Somente `MANAGER`/`ADMIN` na policy da API |
| Operador sem poder de autorização | Coberto por integração |
| Desafio aleatório, curto e one-time | Nonce 256 bits, 120 s, consumo SQL atômico |
| Replay | Retorna conflito |
| Assinatura inválida | Desafio consumido, auditoria e `403` |
| Revogação contra sessão já emitida | Status é relido antes de criar leitura |
| Identidade declarada pelo cliente | Snapshot local é removido do POST; FK vem do servidor |
| Histórico existente | FK anulável e sem backfill |
| Fila/fotos no upgrade | Teste real de IndexedDB v1→v2 |
| Fila de pendente | Retryable, sem descarte |
| Fila de revogado | Permanente/revisável, sem apagar evento ou blob |
| LastSeen | Throttle configurável de cinco minutos |
| Auditoria sem material secreto | Snapshots excluem chave, assinatura e token |

## Migration

`AddRegisteredDeviceIdentity` contém a coluna anulável `registered_device_id`, as tabelas `registered_devices` e `device_challenges`, índices e FKs `RESTRICT`. Não há alteração de migrations anteriores, delete, backfill, seed ou aplicação em Azure.

## Validação executada

- `npm run lint`: aprovado;
- `npm test`: aprovado;
- `npm run build`: aprovado com Next.js 16.3.8;
- `dotnet build ... -c Release`: aprovado, zero warnings/erros;
- unitários .NET em Release: aprovados, 202 testes;
- integração SQL local: indisponível porque o ambiente não possui `TEST_SQLSERVER_CONNECTION_STRING` nem Docker; o workflow de PR provisiona SQL Server descartável e executa a solução completa.

O resultado final do workflow de PR deve ser registrado no fechamento da entrega. Nenhum workflow de deploy foi disparado porque a branch não é `main`.

## Riscos residuais e operação

- O primeiro cadastro e toda comprovação para sincronização exigem internet.
- Um dispositivo autorizado offline só descobre revogação quando reconecta; sua fila continua íntegra.
- Limpar o armazenamento do site perde a chave e exige nova solicitação.
- A credencial curta fica somente em memória e é renovada por desafio conforme necessário.
- A identidade comprova posse da chave do perfil do navegador; não promete atestado de hardware.
