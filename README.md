# Transjap Horímetros

Sistema de controle e integridade de registros de horímetros e odômetros de máquinas pesadas e veículos da **TRANSJAP Terraplenagem e Construções**.

> [!IMPORTANT]
> O sistema não deve ser publicado no Azure nem exposto à internet antes da conclusão e validação da Fase 3 (Segurança e Autenticação).

## Arquitetura de dados

- Banco oficial de produção: **Azure SQL Database**.
- Banco de desenvolvimento: **SQL Server 2022** local ou em container.
- Provider: **Microsoft.EntityFrameworkCore.SqlServer**.
- Configuração: `ConnectionStrings__DefaultConnection`, sem credenciais versionadas.
- Resiliência: retry transiente do EF Core habilitado para Azure SQL.

## Pré-requisitos

- .NET SDK `10.0.401` (o repositório também inclui o runtime local em `.dotnet/`).
- Node.js `20.9` ou superior e npm.
- SQL Server 2022 ou Docker com Docker Compose.
- `sqlcmd` somente quando o backup for executado contra uma instalação local fora do Docker.

## Configuração inicial

1. Crie o arquivo local de ambiente:

   ```powershell
   Copy-Item .env.example .env
   ```

2. Substitua `your_secure_password_here` por uma senha forte compatível com a política do SQL Server.
3. Mantenha `SQLSERVER_USE_DOCKER=true` para usar o container ou altere para `false` quando houver um SQL Server local já configurado.

O `.env` não é versionado. Nunca inclua nele credenciais reais do Azure em commits.

## Execução local

Para validar o `.env`, iniciar o SQL Server quando necessário, subir a API, aguardar `/health/ready` e iniciar o frontend:

```powershell
.\scripts\dev.ps1
```

Para iniciar apenas banco e API:

```powershell
docker compose up -d sqlserver
.\scripts\start-api.ps1
```

A API aplica `InitialCreateSqlServer` e executa o seed idempotente das 52 máquinas na inicialização.

### URLs locais

- Painel: [http://localhost:3000](http://localhost:3000)
- API: [http://127.0.0.1:5080](http://127.0.0.1:5080)
- Readiness: [http://127.0.0.1:5080/health/ready](http://127.0.0.1:5080/health/ready)
- Liveness: [http://127.0.0.1:5080/health/live](http://127.0.0.1:5080/health/live)
- Swagger: [http://127.0.0.1:5080/swagger](http://127.0.0.1:5080/swagger)

## Testes

`TEST_SQLSERVER_CONNECTION_STRING` deve apontar exatamente para o banco `transjap_horimetros_tests` em uma instância local. O `TestDatabaseGuard` bloqueia outro nome, o banco principal e endpoints `*.database.windows.net`.

```powershell
.\scripts\test.ps1
```

## Migrations

A migration PostgreSQL de desenvolvimento foi substituída por uma nova baseline SQL Server:

```text
InitialCreateSqlServer
```

Para criar uma migration futura:

```powershell
.\.dotnet\dotnet.exe ef migrations add NomeDaMigration `
  --project apps/api/src/TransjapHorimetros.Infrastructure `
  --startup-project apps/api/src/TransjapHorimetros.Api
```

As datas são persistidas como `datetime2(7)` e convertidas para UTC; GUIDs usam `uniqueidentifier`, horímetros usam `decimal(12,2)`, enums usam strings e os snapshots JSON de auditoria usam `nvarchar(max)`.

## Backup local

O backup gera um `.bak` real com `BACKUP DATABASE` e valida o arquivo com `RESTORE VERIFYONLY`:

```powershell
.\scripts\backup-db.ps1
```

No Docker, o arquivo é criado no container e copiado para `.sqlserver-backups/`. Em uma instalação local, o script usa `sqlcmd`; a conta do serviço SQL Server precisa ter permissão de escrita no diretório informado.

### Dados PostgreSQL legados

Diretórios locais `.postgres/`, `.postgres-data/` e `.postgres-backups/` foram encontrados durante a migração e permaneceram intocados e ignorados pelo Git. A nova migration não importa esses dados automaticamente. Antes de remover esses diretórios manualmente, confira os backups existentes e exporte qualquer dado útil da instância antiga.

## Azure SQL Database

No Azure App Service, configure `ConnectionStrings__DefaultConnection` sem alterar o código. Formato esperado:

```text
Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=transjap_horimetros;Persist Security Info=False;User ID=<usuario>;Password=<segredo>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Armazene o segredo em App Service Configuration e, futuramente, Key Vault. Nenhum deploy é realizado por este repositório nesta etapa.

## Estrutura

- `apps/api/`: solução ASP.NET Core em Clean Architecture.
- `src/`: frontend Next.js 16.
- `scripts/`: automações de desenvolvimento, testes e backup.
- `docs/`: decisões arquiteturais e regras de engenharia.
- [Regras de negócio da Fase 1](docs/regras-de-negocio.md) e [decisões pendentes](docs/decisoes.md).
