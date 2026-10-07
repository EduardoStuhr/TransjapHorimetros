# Registro de Decisões de Arquitetura e Engenharia (ADR) — Transjap Horímetros

Este documento registra as decisões tomadas, convenções adotadas e itens com decisões pendentes pelo usuário para o sistema **Transjap Horímetros** (EduardoStuhr/TransjapHorimetros).

---

## 1. ADR — SQL Server e Azure SQL Database

**Data:** 2026-10-07
**Status:** Aceita

O banco oficial de produção passa a ser o **Azure SQL Database**. O ambiente de desenvolvimento usa **SQL Server 2022**, local ou via Docker, e a persistência usa `Microsoft.EntityFrameworkCore.SqlServer`.

A assinatura Azure for Students disponível para o projeto não permite provisionar Azure Database for PostgreSQL nas regiões disponíveis, enquanto Azure SQL Database está disponível. A mudança é uma decisão de compatibilidade com a infraestrutura da assinatura, não uma limitação técnica do PostgreSQL.

Consequências técnicas:

- a API recebe a conexão por `ConnectionStrings__DefaultConnection` e aceita uma connection string Azure SQL sem alteração de código;
- falhas transitórias usam `EnableRetryOnFailure` com até cinco tentativas;
- GUIDs usam `uniqueidentifier`, valores de horímetro usam `decimal(12,2)` e textos usam `nvarchar`;
- datas de domínio continuam como `DateTimeOffset`, são normalizadas para UTC e persistidas como `datetime2(7)`;
- enums continuam persistidos como strings, sem enum nativo de banco;
- snapshots JSON de auditoria usam `nvarchar(max)`;
- a baseline ativa é `InitialCreateSqlServer`; a migration PostgreSQL de desenvolvimento não é reaproveitada.

Os diretórios locais legados `.postgres/`, `.postgres-data/` e `.postgres-backups/` detectados na migração foram preservados. Nenhum dado legado é removido ou importado automaticamente.

## 2. Princípios e Regras Fundamentais Adotadas

1. **Prioridade Máxima à Integridade e Rastreabilidade ("A foto é a prova, o texto é a leitura, o servidor é a verdade")**
   - Registros de leitura originais são estritamente **imutáveis**. Nenhuma leitura é atualizada via `UPDATE`. Correções são efetuadas criando um novo registro vinculado com justificativa obrigatória, operador/usuário e timestamp.
   - Todo ato relevante é registrado em `AuditLog` com bloqueio contra exclusão ou alteração.

2. **Nomenclatura do Domínio Alinhada com as Skills**
   - As entidades centrais seguem o vocabulário oficial das skills: `PhotoEvidence` (em vez de `ReadingPhoto`) e `MachineQrCode` (para desacoplar o token de identificação física do cadastro da máquina).
   - Unidade de medição identificada explicitamente: máquinas pesadas em horas (`MeterUnit.HOURS`) e veículos leves da frota em quilômetros (`MeterUnit.KM`).

3. **Política de Migrations e Proteção do Banco de Dados**
   - **Backup obrigatório antes de migrações:** Antes de aplicar qualquer migration futura em banco de dados existente, deve ser executado o script [scripts/backup-db.ps1](../scripts/backup-db.ps1), gerando e validando um backup SQL Server `.bak` com timestamp.
   - **Migrations estritamente aditivas:** Novas colunas, tabelas e constraints nunca devem excluir dados ou colunas existentes sem procedimento explícito de transição.
   - **Trava de segurança nos testes de integração:** A classe `TestDatabaseGuard` impede a execução do `ResetDatabaseAsync` caso a connection string aponte para o banco da aplicação ou para qualquer banco diferente de `transjap_horimetros_tests`.

4. **Bloqueio de Publicação e Exposição Externa**
   - **Segurança primeiro:** O sistema não será publicado no Azure nem exposto publicamente à internet antes da conclusão e validação completa da **Fase 3 (Segurança e Autenticação)**.
   - Arquivos `.env` e backups permanecem estritamente ignorados pelo Git (`.gitignore`).

---

## 3. Parâmetros Configuráveis e Itens Pendentes de Decisão do Usuário

Enquanto as respostas aos itens de negócio não forem fornecidas, foram adotados padrões conservadores e configuráveis em `appsettings.json` (`HourMeterRules`):

| Decisão de Negócio | Status | Padrão Conservador Adotado | Onde Configurar |
| :--- | :--- | :--- | :--- |
| **Tolerância entre final de um dia e início do dia seguinte (F03)** | Pendente | `0.5` horas (30 minutos) de divergência permitida. Se operador registrar apenas abertura, o sistema gera registro final inferido (`INFERRED`) sem substituir evidência fotográfica. | `HourMeterRules:DayTransitionToleranceHours` |
| **Limite máximo diário de horas trabalhadas (F02)** | Pendente | Máximo de `24.0` horas por dia e `1.0` hora por hora transcorrida (com tolerância de `0.25h`). | `HourMeterRules:MaxPlausibleDailyHours` |
| **Janela de alerta para máquina sem leitura (F04)** | Pendente | `2` dias consecutivos sem registro para frota ativa. | `HourMeterRules:MissingReadingThresholdDays` |
| **Tolerância de divergência de relógio do dispositivo (F10)** | Pendente | `300` segundos (5 minutos) entre relógio do celular e relógio do servidor. | `HourMeterRules:ClockSkewToleranceSeconds` |
| **Raio padrão de Geofence para Obras (F09)** | Pendente | `500` metros a partir do centro da obra, caso não haja polígono detalhado. | `HourMeterRules:GeofenceDefaultRadiusMeters` |
| **Limiar de confiança mínima do OCR (F08)** | Pendente | `0.85` (85%). Abaixo disso gera anomalia de baixa confiança (`LOW_OCR_CONFIDENCE`). | `HourMeterRules:OcrConfidenceThreshold` |
| **Método de Autenticação Primário (Fase 3)** | Pendente | Microsoft Entra ID (OIDC) como primário corporativo com fallback para credenciais locais com hash Argon2/BCrypt e papéis (`Admin`, `Diretoria`, `Escritório`). | `Authentication` |
| **Plataforma Mobile Alvo** | Informativo | Arquitetura de API desenhada para ser agnóstica (JSON / HTTPS com autenticação de dispositivo por HMAC), compatível com Android e iOS (Expo/React Native). | Contratos `/api/v1/sync` |
| **Conteúdo físico dos QR Codes colados** | Pendente | Confirmar se o QR code já existente nas máquinas traz a string `"FROTA 68"`, URL ou número simples. A API aceita resolução por `FleetNumber`. | Cadastro e leitura do QR |
