# Transjap Horímetros

Sistema de controle e integridade de registros de horímetros e odômetros de máquinas pesadas e veículos da **TRANSJAP Terraplenagem e Construções**.

> [!IMPORTANT]
> **Aviso de Segurança e Publicação:**
> Nada será publicado no Azure nem exposto à internet antes da conclusão e validação completa da **Fase 3 (Segurança e Autenticação)**. O ambiente atual opera estritamente em rede local/desenvolvimento.

---

## 1. Pré-requisitos

- **.NET SDK:** Versão `10.0.401` (fixada em [global.json](file:///c:/Users/fabio/Downloads/TransjapHorimetros/TransjapHorimetros/global.json); o repositório inclui runtime local em `.dotnet/` para conveniência).
- **Node.js:** Versão `20.9` ou superior e `npm`.
- **PostgreSQL:** Versão `16` ou superior (executável local em `.postgres/`, serviço do sistema ou via Docker).
- **Docker e Docker Compose:** Opcional (caso opte por subir o PostgreSQL em container).

---

## 2. Configuração Inicial

1. Copie o arquivo de exemplo de variáveis de ambiente:
   ```powershell
   Copy-Item .env.example .env
   ```
2. Caso necessário, ajuste credenciais no `.env` (a API e os scripts utilizam `127.0.0.1` para conexões locais).

---

## 3. Como Executar Localmente

### Opção 1: Inicialização Unificada (Recomendado)
Para subir o PostgreSQL, a API ASP.NET Core e preparar o Frontend em um único comando:
```powershell
.\scripts\dev.ps1
```

### Opção 2: Inicialização Passo a Passo

1. **Subir o Banco de Dados (se optar por Docker):**
   ```bash
   docker compose up -d postgres
   ```
2. **Iniciar a API ASP.NET Core:**
   ```powershell
   .\scripts\start-api.ps1
   ```
   *O script valida o `.env`, checa se o PostgreSQL está ativo na porta 5432, sobe a API em segundo plano e aguarda `/health/ready` responder 200 OK.*
3. **Iniciar o Frontend Web:**
   ```bash
   npm run dev
   ```

### URLs de Acesso Local:
- **Painel Administrativo:** [http://localhost:3000](http://localhost:3000)
- **API ASP.NET Core:** [http://127.0.0.1:5080](http://127.0.0.1:5080)
- **Health Check:** [http://127.0.0.1:5080/health/ready](http://127.0.0.1:5080/health/ready)
- **Swagger / OpenAPI:** [http://127.0.0.1:5080/swagger](http://127.0.0.1:5080/swagger)

---

## 4. Testes Automatizados

O repositório possui suíte de testes unitários e de integração protegidos contra exclusão acidental do banco de aplicação (`TestDatabaseGuard`):
```powershell
.\scripts\test.ps1
```
*O script carrega o `.env` (incluindo `TEST_POSTGRES_CONNECTION_STRING`) e roda todos os testes.*

---

## 5. Rotinas de Banco de Dados

### Backup Preventivo (pg_dump)
Antes de qualquer alteração estrutural ou aplicação de migrations futuras, gere um backup completo:
```powershell
.\scripts\backup-db.ps1
```
Os backups são gravados na pasta `.postgres-backups/` (ignorada pelo Git).

### Reset e Reaplicação do Banco de Desenvolvimento
Para reinicializar o banco de dados e reaplicar as migrations e os seeds das 52 máquinas:
1. Pare a API.
2. No PostgreSQL, recrie o banco `transjap_horimetros`.
3. Inicie a API com `.\scripts\start-api.ps1` (as migrations e o seed serão aplicados automaticamente na inicialização).

---

## 6. Estrutura do Projeto

- `apps/api/`: Solução ASP.NET Core Clean Architecture (`Domain`, `Application`, `Infrastructure`, `Api`, `UnitTests`, `IntegrationTests`).
- `src/`: Aplicação Web Next.js 16 (App Router, Tailwind CSS, shadcn/ui).
- `scripts/`: Scripts PowerShell de automação (`dev.ps1`, `start-api.ps1`, `test.ps1`, `backup-db.ps1`).
- `docs/`: Documentação de arquitetura, decisões e regras (`docs/decisoes.md`).
