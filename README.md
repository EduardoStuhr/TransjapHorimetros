# Transjap Horímetros

Sistema de controle e integridade de registros de horímetros e odômetros de máquinas pesadas e veículos da **TRANSJAP Terraplenagem e Construções**.

> [!WARNING]
> A Fase 4 adiciona evidência fotográfica, mas a migration e o storage desta branch ainda não foram aplicados no Azure. Revise o PR e configure o Blob privado antes de qualquer implantação.

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
3. Gere uma chave JWT aleatória de pelo menos 32 bytes e configure `Authentication__Jwt__SigningKey`.
4. Informe `BootstrapAdmin__Email`, `BootstrapAdmin__Password` e `BootstrapAdmin__FullName` somente para provisionar o primeiro administrador.
5. Mantenha `SQLSERVER_USE_DOCKER=true` para usar o container ou altere para `false` quando houver um SQL Server local já configurado.
6. Para testar fotos, configure `Storage__Photos__AccountUri` com identidade Azure ou `Storage__Photos__ConnectionString` somente no `.env` local.

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

### Autenticação

O login corporativo está em `/login`. A API usa ASP.NET Core Identity, JWT Bearer e autorização por papéis. O frontend atua como BFF: access token e refresh token ficam em cookies `HttpOnly`, `SameSite=Strict` e `Secure` em produção, sem uso de `localStorage`.

Variáveis obrigatórias ou recomendadas:

```text
Authentication__Jwt__SigningKey=<segredo-aleatorio-com-no-minimo-32-bytes>
Authentication__Jwt__Issuer=TransjapHorimetros
Authentication__Jwt__Audience=TransjapHorimetrosApp
BootstrapAdmin__Email=<email-inicial>
BootstrapAdmin__Password=<senha-temporaria-forte>
BootstrapAdmin__FullName=<nome-completo>
API_URL=<url-interna-da-api-para-o-next>
```

O bootstrap não redefine senha e só provisiona um administrador quando ainda não existe usuário no papel `ADMIN`. Remova os três secrets `BootstrapAdmin__*` depois do primeiro provisionamento.

### Evidência fotográfica

Leituras podem receber uma foto JPEG, PNG ou WEBP de até 10 MB pelo endpoint autenticado `POST /api/v1/readings/{readingId}/photo`. A API valida magic bytes e dimensões, calcula SHA-256 e guarda somente metadados no Azure SQL. O original permanece em container Azure Blob privado e é visualizado exclusivamente pelo endpoint protegido/BFF.

Configuração recomendada com Managed Identity:

```text
Storage__Photos__Provider=AzureBlob
Storage__Photos__ContainerName=photo-evidence
Storage__Photos__MaximumFileSizeBytes=10485760
Storage__Photos__AccountUri=https://<storage-account>.blob.core.windows.net
```

Como fallback, `Storage__Photos__ConnectionString` pode ser definida por secret. Nunca versione a connection string, AccountKey ou SAS. Consulte [docs/fase-4-evidencia-fotografica.md](docs/fase-4-evidencia-fotografica.md).

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

Armazene o segredo em App Service Configuration e, futuramente, Key Vault. Os workflows atuais publicam API e frontend quando há push na `main`; por isso, a Fase 2 deve permanecer em branch e PR até a revisão da migration.

## Estrutura

- `apps/api/`: solução ASP.NET Core em Clean Architecture.
- `src/`: frontend Next.js 16.
- `scripts/`: automações de desenvolvimento, testes e backup.
- `docs/`: decisões arquiteturais e regras de engenharia.
- [Regras de negócio da Fase 1](docs/regras-de-negocio.md), [QR Codes da Fase 2](docs/fase-2-qr-codes.md), [Segurança da Fase 3](docs/fase-3-seguranca-autenticacao.md), [Evidência fotográfica da Fase 4](docs/fase-4-evidencia-fotografica.md) e [decisões arquiteturais](docs/decisoes.md).
