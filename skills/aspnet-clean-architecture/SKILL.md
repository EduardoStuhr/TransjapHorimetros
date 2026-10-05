---
name: aspnet-clean-architecture
description: Projetar, implementar e revisar o backend ASP.NET Core do Transjap Horímetros com Clean Architecture, PostgreSQL, EF Core, autenticação, auditoria, idempotência, evidências fotográficas, OCR e observabilidade. Usar em tarefas de API, entidades, casos de uso, persistência, migrations, endpoints, segurança, testes e integrações Azure deste projeto.
---

# ASP.NET Core Clean Architecture

Construir o backend como autoridade do sistema. Fazer clientes web e mobile consumirem exclusivamente a API; nunca permitir acesso direto ao PostgreSQL.

## Aplicar a arquitetura

Organizar a solução assim:

~~~text
src/
├── TransjapHorimetros.Domain/
├── TransjapHorimetros.Application/
├── TransjapHorimetros.Infrastructure/
└── TransjapHorimetros.Api/
tests/
├── TransjapHorimetros.Domain.Tests/
├── TransjapHorimetros.Application.Tests/
└── TransjapHorimetros.IntegrationTests/
~~~

- Manter Domain independente de ASP.NET Core, EF Core, PostgreSQL, Azure, controllers e UI.
- Colocar entidades, value objects, enums, invariantes e regras fundamentais em Domain.
- Colocar casos de uso, commands, queries, DTOs, validações, autorização e interfaces de infraestrutura em Application.
- Implementar PostgreSQL, EF Core, Azure Blob, OCR, e-mail, clock, arquivos e serviços externos em Infrastructure.
- Limitar Api a autenticação, autorização, contratos HTTP, endpoints, middleware, versionamento e OpenAPI.
- Separar funcionalidades pelos módulos Auth, Users, Machines, QrCodes, WorkSites, HourMeterReadings, Photos, Ocr, Anomalies, Reports, Audit e Sync.

## Modelar o domínio

Considerar inicialmente User, Role, Machine, MachineQrCode, WorkSite, HourMeterReading, PhotoEvidence, OcrResult, Anomaly e AuditLog.

Centralizar no servidor as validações de máquina, usuário, permissão, QR, obra, valor, sequência histórica, duplicidade, horário, idempotência e integridade da evidência. Nunca confiar em valores calculados pelo cliente.

Definir interfaces externas na camada Application quando isso preservar a inversão de dependência:

~~~csharp
public interface IImageStorage
{
    Task<string> StoreAsync(...);
}
~~~

Implementar a interface em Infrastructure, por exemplo como AzureBlobImageStorage.

## Garantir idempotência e tempo

- Exigir ClientEventId UUID criado antes da sincronização.
- Criar constraint única para ClientEventId.
- Retornar o registro existente quando o cliente repetir uma requisição já processada.
- Armazenar CapturedAtDevice, ReceivedAtServer e SyncedAt separadamente.
- Usar UTC internamente e converter fuso apenas na apresentação.
- Tratar o relógio do dispositivo como informação não confiável.

## Preservar histórico e auditoria

Nunca apagar ou sobrescrever silenciosamente leituras relevantes. Representar correções com leitura original, nova leitura, justificativa, autor e data.

Registrar ações críticas como LOGIN, CREATE, UPDATE, CORRECT, REVIEW, REJECT, EXPORT e PERMISSION_CHANGE. Incluir identidade, entidade, valores anterior e novo, IP, user agent, data e correlation ID quando aplicável. Nunca registrar senhas, tokens ou secrets.

## Proteger a API

- Exigir HTTPS, autenticação e autorização por roles ou policies.
- Aplicar menor privilégio, CORS restritivo, validação de entrada, headers adequados e rate limiting em endpoints sensíveis.
- Proteger refresh tokens e secrets.
- Usar inicialmente os perfis OPERATOR, SUPERVISOR, ADMINISTRATIVE, MANAGER e ADMIN.
- Nunca enviar stack trace ao cliente em produção.
- Usar respostas consistentes e preferir Problem Details.

Rotas iniciais esperadas:

~~~http
POST /api/auth/login
GET  /api/machines
GET  /api/machines/{id}
GET  /api/machines/by-fleet/{fleetNumber}
POST /api/machines
GET  /api/worksites
POST /api/worksites
GET  /api/readings
GET  /api/readings/{id}
POST /api/readings
GET  /api/anomalies
GET  /api/reports/hourmeters
~~~

## Persistir com PostgreSQL

- Usar migrations versionadas do EF Core.
- Manter configurações de entidades preferencialmente em Infrastructure/Persistence/Configurations/.
- Criar foreign keys, status válidos e unicidade para frota, UUID, QR ativo e client event ID.
- Evitar alterações manuais em produção sem procedimento controlado.
- Não armazenar imagens grandes no PostgreSQL.

Enviar fotos para storage e persistir identificador, SHA-256, content type, tamanho, data, usuário e leitura associada.

## Tratar OCR e anomalias

Tratar OCR como auxílio, nunca como verdade absoluta. Persistir texto bruto, valor detectado, confiança, engine, versão e data.

Implementar regras de anomalia desacopladas para, no mínimo:

~~~text
READING_DECREASE
IMPOSSIBLE_HOUR_INCREASE
DUPLICATE_READING
MISSING_READING
LOW_OCR_CONFIDENCE
OCR_OPERATOR_DIVERGENCE
LATE_SYNC
~~~

Registrar tipo, severidade, descrição, status, leitura relacionada, data e revisor.

## Observar e testar

- Disponibilizar Swagger/OpenAPI em desenvolvimento e manter contratos atualizados.
- Registrar correlation ID, erros, duração, falhas de banco, storage, OCR e sincronização sem expor conteúdo sensível.
- Usar logging estruturado e Application Insights quando configurado.
- Testar regras de leitura, anomalias, permissões e cálculos em testes unitários.
- Testar API, PostgreSQL, autenticação, idempotência, migrations e upload em testes de integração.

## Concluir a funcionalidade

Só considerar uma funcionalidade pronta quando possuir regra de domínio, validação server-side, autorização, persistência consistente, tratamento de erro, logs, OpenAPI atualizado e testes relevantes. Preservar histórico e compatibilidade, salvo decisão explícita em contrário.
