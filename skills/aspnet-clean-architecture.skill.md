# Skill: ASP.NET Core Clean Architecture — Transjap Horímetros

## Objetivo
Orientar o desenvolvimento da API e backend do projeto **Transjap Horímetros** utilizando ASP.NET Core, com separação de responsabilidades, regras de negócio centralizadas, segurança, auditabilidade e integração com PostgreSQL e Azure.

## Contexto
O backend será a autoridade do sistema.

Clientes previstos:
- painel web Next.js;
- aplicativo mobile Flutter;
- integrações futuras.

Nenhum cliente acessará o banco diretamente.

Fluxo obrigatório:

```text
Web / Mobile
     ↓ HTTPS
ASP.NET Core API
     ↓
Application / Domain
     ↓
Infrastructure
     ↓
PostgreSQL / Storage / OCR / Logs
```

## Stack Recomendada
- .NET
- ASP.NET Core Web API
- C#
- PostgreSQL
- Entity Framework Core
- FluentValidation ou validação equivalente
- OpenAPI / Swagger
- JWT ou solução de autenticação adequada
- Serilog ou logging estruturado
- Azure Blob Storage
- Application Insights
- Docker para ambiente de desenvolvimento e deploy quando conveniente

## Arquitetura

Estrutura sugerida:

```text
src/
├── TransjapHorimetros.Domain/
├── TransjapHorimetros.Application/
├── TransjapHorimetros.Infrastructure/
└── TransjapHorimetros.Api/

tests/
├── TransjapHorimetros.Domain.Tests/
├── TransjapHorimetros.Application.Tests/
└── TransjapHorimetros.IntegrationTests/
```

## Domain

Responsável por:
- entidades;
- value objects;
- enums;
- regras fundamentais;
- invariantes do negócio.

Entidades previstas:

```text
User
Role
Machine
MachineQrCode
WorkSite
HourMeterReading
PhotoEvidence
OcrResult
Anomaly
AuditLog
```

O domínio não deve depender de:
- ASP.NET;
- Entity Framework;
- Azure;
- PostgreSQL;
- controllers;
- UI.

## Application

Responsável por:
- casos de uso;
- commands;
- queries;
- DTOs;
- validações de aplicação;
- interfaces para infraestrutura;
- autorização de operações.

Exemplos:

```text
CreateMachineCommand
GetMachineByFleetQuery
CreateHourMeterReadingCommand
GetMachineReadingsQuery
ReviewSuspiciousReadingCommand
ExportHourMeterReportQuery
```

## Infrastructure

Responsável por:
- PostgreSQL;
- EF Core;
- Azure Blob;
- OCR;
- e-mail;
- logging externo;
- implementação de repositories quando necessário;
- clock;
- geração de arquivos;
- serviços externos.

Interfaces devem ser definidas na camada Application quando fizer sentido.

Exemplo:

```csharp
public interface IImageStorage
{
    Task<string> StoreAsync(...);
}
```

Implementação:

```text
AzureBlobImageStorage
```

Assim o domínio não depende do Azure.

## API

Responsável por:
- autenticação;
- autorização;
- controllers/endpoints;
- request/response;
- status HTTP;
- versionamento;
- OpenAPI;
- middleware;
- correlation id;
- rate limiting quando necessário.

## Módulos do Sistema

Separar por domínio:

```text
Auth
Users
Machines
QrCodes
WorkSites
HourMeterReadings
Photos
Ocr
Anomalies
Reports
Audit
Sync
```

## Rotas Iniciais

```http
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
```

## Regras de Horímetro

A API é responsável por validar:
- máquina existente;
- usuário autorizado;
- QR válido;
- obra existente;
- leitura válida;
- sequência histórica;
- duplicidade;
- horários;
- idempotência;
- integridade da evidência.

Nunca confiar em valores calculados pelo app.

## Idempotência

Registros do app devem possuir identificador único gerado antes da sincronização.

Exemplo:

```text
clientEventId: UUID
```

Criar constraint única.

Se o app reenviar a mesma requisição por falha de conexão:
- não duplicar;
- retornar o registro existente quando possível.

## Data e Hora

Armazenar separadamente:

```text
CapturedAtDevice
ReceivedAtServer
SyncedAt
```

Não confiar somente no relógio do celular.

Usar UTC internamente.

Converter para horário local apenas na apresentação.

## Histórico

Não apagar ou sobrescrever silenciosamente leituras relevantes.

Correções devem ser rastreáveis.

Modelo preferencial:

```text
OriginalReading
CorrectionReading
CorrectionReason
CorrectedBy
CorrectedAt
```

ou outra estrutura equivalente que mantenha histórico completo.

## Audit Log

Registrar operações críticas:

```text
LOGIN
CREATE
UPDATE
CORRECT
REVIEW
REJECT
EXPORT
PERMISSION_CHANGE
```

Campos recomendados:

```text
Id
UserId
Action
Entity
EntityId
OldValue
NewValue
IpAddress
UserAgent
CreatedAt
CorrelationId
```

Evitar guardar senhas, tokens e secrets no log.

## Segurança

Implementar:
- HTTPS obrigatório;
- autenticação;
- autorização por roles/policies;
- refresh token seguro;
- proteção contra brute force;
- validação de input;
- logs estruturados;
- secrets em ambiente seguro;
- menor privilégio;
- CORS restritivo;
- headers adequados;
- rate limiting para endpoints sensíveis.

Nunca enviar stack trace para o usuário em produção.

## RBAC

Perfis iniciais:

```text
OPERATOR
SUPERVISOR
ADMINISTRATIVE
MANAGER
ADMIN
```

Exemplo:
- operador não exclui leitura;
- supervisor pode revisar;
- administrativo consulta e exporta;
- gestor consulta indicadores;
- admin gerencia usuários e configurações.

## Banco de Dados

PostgreSQL.

Tabelas previstas:

```text
users
roles
user_roles
machines
machine_qr_codes
work_sites
hour_meter_readings
photo_evidences
ocr_results
anomalies
audit_logs
```

Criar constraints para:
- frota única;
- QR ativo válido;
- UUID único;
- client event id único;
- foreign keys;
- status válidos.

## EF Core

Usar migrations versionadas.

Não editar banco manualmente em produção sem procedimento controlado.

Configurações das entidades preferencialmente separadas:

```text
Infrastructure/Persistence/Configurations/
```

## Upload de Foto

A API não deve armazenar imagens grandes diretamente no PostgreSQL.

Fluxo:

```text
API
 ↓
Storage
 ↓
URL/identifier
 ↓
Banco
```

Armazenar metadados:
- hash;
- content type;
- tamanho;
- data;
- usuário;
- leitura associada.

## Hash

Gerar SHA-256 ou equivalente para evidências recebidas.

Objetivo:
- detectar alteração posterior;
- garantir integridade do arquivo armazenado.

## OCR

OCR deve ser considerado processamento auxiliar.

Nunca tratar OCR como verdade absoluta.

Armazenar:
- texto bruto;
- valor detectado;
- confiança;
- engine/version;
- data de processamento.

## Anomalias

Criar motor de regras desacoplado.

Exemplos:

```text
READING_DECREASE
IMPOSSIBLE_HOUR_INCREASE
DUPLICATE_READING
MISSING_READING
LOW_OCR_CONFIDENCE
MANUAL_OCR_DIVERGENCE
UNEXPECTED_SYNC_DELAY
```

Cada anomalia deve ter:
- tipo;
- severidade;
- descrição;
- status;
- leitura relacionada;
- data;
- responsável pela revisão.

## API Responses

Erros devem ter formato consistente.

Exemplo:

```json
{
  "type": "validation_error",
  "title": "Dados inválidos",
  "status": 400,
  "errors": {
    "hourMeter": ["Valor inválido."]
  }
}
```

Preferir Problem Details quando adequado.

## Swagger

Swagger/OpenAPI deve estar disponível em desenvolvimento.

Usar para:
- testar endpoints;
- validar contratos;
- ajudar frontend/mobile;
- documentar requests/responses.

## Observabilidade

Registrar:
- correlation id;
- request;
- erro;
- tempo de resposta;
- falhas de banco;
- falhas de storage;
- falhas OCR;
- sincronizações problemáticas.

Não registrar conteúdo sensível desnecessariamente.

## Testes

### Unitários
Focar em:
- regras de leitura;
- anomalias;
- permissões;
- cálculos.

### Integração
Testar:
- API;
- PostgreSQL;
- autenticação;
- idempotência;
- migrations;
- upload.

## Critério de Conclusão

Uma funcionalidade de backend só está pronta quando:
1. possui regra de domínio definida;
2. possui validação server-side;
3. possui autorização;
4. possui persistência consistente;
5. possui tratamento de erro;
6. possui logs apropriados;
7. possui OpenAPI atualizado;
8. possui testes relevantes;
9. mantém histórico quando aplicável;
10. não quebra compatibilidade sem decisão explícita.
