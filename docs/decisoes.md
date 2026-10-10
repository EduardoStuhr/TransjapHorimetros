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
   - Registros de leitura originais são estritamente **imutáveis**. Nenhuma leitura é atualizada via `UPDATE`. Um fluxo formal de correção deverá criar novo registro vinculado, com justificativa, responsável e timestamp; esse fluxo ainda não existe nesta fase.
   - Todo ato relevante é registrado em `AuditLog` com bloqueio contra exclusão ou alteração.

2. **Nomenclatura do Domínio Alinhada com as Skills**
   - As entidades centrais seguem o vocabulário oficial das skills: `PhotoEvidence` (em vez de `ReadingPhoto`) e `MachineQrCode` (para desacoplar o token de identificação física do cadastro da máquina).
   - A unidade é explícita (`MeterUnit.HOURS` ou `MeterUnit.KM`), mas a classificação de cada frota ainda depende de confirmação da Transjap; até lá, `MeterUnit` pode permanecer nulo.

3. **Política de Migrations e Proteção do Banco de Dados**
   - **Backup obrigatório antes de migrações:** Antes de aplicar qualquer migration futura em banco de dados existente, deve ser executado o script [scripts/backup-db.ps1](../scripts/backup-db.ps1), gerando e validando um backup SQL Server `.bak` com timestamp.
   - **Migrations estritamente aditivas:** Novas colunas, tabelas e constraints nunca devem excluir dados ou colunas existentes sem procedimento explícito de transição.
   - **Trava de segurança nos testes de integração:** A classe `TestDatabaseGuard` impede a execução do `ResetDatabaseAsync` caso a connection string aponte para o banco da aplicação ou para qualquer banco diferente de `transjap_horimetros_tests`.

4. **Bloqueio de Publicação e Exposição Externa**
   - **Segurança primeiro:** O sistema não será publicado no Azure nem exposto publicamente à internet antes da conclusão e validação completa da **Fase 3 (Segurança e Autenticação)**.
   - Arquivos `.env` e backups permanecem estritamente ignorados pelo Git (`.gitignore`).

---

## 3. Estado das Regras e Decisões Operacionais

As regras implementadas estão detalhadas em [regras-de-negocio.md](regras-de-negocio.md). Seus valores padrão são configuráveis em `appsettings.json` (`HourMeterRules`), mas a configuração técnica não equivale à aprovação da Transjap.

### Confirmado no código

- Leituras originais e logs de auditoria são append-only na aplicação; a API não oferece fluxo de correção formal nesta fase.
- O servidor define `ReceivedAtServer`; `ClientEventId` é único e repetição do mesmo evento retorna o registro existente.
- Anomalias deixam a leitura suspeita persistida e auditável; regressão, incremento implausível, relógio divergente, duplicidade provável e divergência de virada de dia têm regras implementadas.
- Máquinas sem unidade confirmada podem permanecer com `MeterUnit = null`; `HOURS` e `KM` são persistidos como strings e não são intercambiáveis.
- A métrica de ausência considera somente máquinas `ACTIVE` e o horário UTC de captura (`CapturedAtDevice`); o frontend apenas exibe o indicador retornado pela API.
- Valor negativo enviado pela API é rejeitado com erro de validação e não é persistido como leitura operacional. `REJECTED` existe como resultado da política de domínio, não como leitura gravada por este endpoint.

### Parâmetros provisórios configurados

| Parâmetro | Padrão provisório | Configuração | Observação |
| :--- | :--- | :--- | :--- |
| Tolerância de virada `CLOSING` → `OPENING` | `0.5` hora | `HourMeterRules:DayTransitionToleranceHours` | Fora da tolerância cria anomalia; não infere leitura nem evidência. |
| Limite plausível por dia | `24` horas | `HourMeterRules:MaxPlausibleDailyHours` | Ainda depende da operação real. |
| Limite por hora transcorrida | `1` hora | `HourMeterRules:MaxPlausibleHoursPerElapsedHour` | Ainda depende da operação real. |
| Tolerância adicional de incremento | `0.25` hora | `HourMeterRules:ElapsedTimeToleranceHours` | Ainda depende da operação real. |
| Ausência de leitura | `2` dias | `HourMeterRules:MissingReadingThresholdDays` | Provisório; conta leitura capturada no período para máquinas ativas. |
| Divergência de relógio | `300` segundos | `HourMeterRules:ClockSkewToleranceSeconds` | Provisório; divergência maior cria `CLOCK_SKEW`. |
| Raio padrão de geofence | `500` metros | `HourMeterRules:GeofenceDefaultRadiusMeters` | Provisório para fase futura; geofence não é declarado como implementado. |
| Confiança mínima de OCR | `0.85` | `HourMeterRules:OcrConfidenceThreshold` | Provisório para fase futura; OCR não é declarado como implementado. |

Não há limite ativo de atraso de sincronização (`LATE_SYNC`): falta validação oficial do atraso máximo tolerado. Não se deve deduzir nem ativar um limiar arbitrário.

### Pendente de validação com a Transjap

1. Após quantos dias sem leitura deve surgir o alerta?
2. Qual é o máximo realista de horas de operação por dia?
3. Há troca física de horímetro? Como é registrada?
4. O horímetro pode zerar?
5. Em quais situações uma leitura menor é legítima?
6. Quem pode solicitar/aprovar uma correção e qual justificativa deve ser registrada?
7. Abertura e fechamento são obrigatórios?
8. A obra é obrigatória em toda leitura?
9. Quais frotas usam `HOURS`?
10. Quais frotas usam `KM`?
11. Uma máquina pode mudar de obra no mesmo dia?
12. Qual atraso máximo de sincronização deve ser aceito antes de classificar `LATE_SYNC`?
13. Qual conteúdo os QR codes físicos existentes carregam: número, texto ou URL?
14. Qual provedor e fluxo de autenticação devem ser adotados na Fase 3?

Nenhuma resposta acima é presumida por este documento. QR, OCR, geofence, mobile e autenticação permanecem fora da Fase 1 e não são descritos como funcionalidades concluídas.
