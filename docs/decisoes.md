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

4. **Publicação atual e risco de exposição externa**
   - A autenticação foi implementada na branch da Fase 3, mas ainda depende de revisão, merge, configuração de secrets e nova implantação.
   - A publicação existente não equivale à aprovação operacional dos endpoints protegidos.
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
| Confiança mínima de OCR | `0.75` | `Ocr:MinimumConfidence` | Ativo na Fase 5; a opção antiga em HourMeterRules permanece sem uso. |

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
13. Há exigência futura de MFA ou integração com um provedor corporativo de identidade?

Nenhuma resposta acima é presumida por este documento. A identidade e a resolução de QR foram implementadas na Fase 2 e estão documentadas em [fase-2-qr-codes.md](fase-2-qr-codes.md). A implementação OCR da Fase 5 é descrita abaixo, com validação no relatório próprio; geofence e mobile permanecem fora do escopo.

## 4. ADR — QR físico desacoplado da máquina

**Data:** 2026-10-15
**Status:** Aceita

O QR físico é modelado por `MachineQrCode`, em relação N:1 com `Machine`. Tokens antigos permanecem reservados e auditáveis após desativação; não existe exclusão física. O banco garante token globalmente único e no máximo um QR ativo por máquina. A resolução aceita número de frota isolado, o formato conservador `FROTA <número>` e o padrão físico oficial de quatro dígitos com zeros à esquerda, sem interpretar URLs ou números em texto livre.

## 5. ADR — Associação inicial dos QR codes físicos oficiais

**Data:** 2026-10-15
**Status:** Aceita

O PDF oficial confirmou que os adesivos já existem e usam quatro dígitos (`0068` → frota 68). Uma migration de dados separada associa os 51 códigos pertencentes às máquinas atuais por `fleet_number`. A frota 296 permanece sem QR porque não aparece no PDF. Códigos de frotas antigas não criam nem reativam máquinas. O patch é idempotente, valida conflitos antes de inserir e não é executado como importação oculta em cada startup. Tokens `TJ-*` permanecem disponíveis somente para futuras emissões ou substituições.

## 6. ADR — Identity, JWT e sessão BFF

**Data:** 2026-10-15
**Status:** Aceita para revisão

A API é a autoridade de autenticação e autorização. ASP.NET Core Identity mantém usuários, papéis, lockout e credenciais; JWT Bearer protege a API com validações de issuer, audience, chave, assinatura e lifetime. A fallback policy exige autenticação em todos os endpoints, exceto login, refresh e health checks explicitamente anônimos.

O frontend Next.js usa o padrão BFF. Tokens não são entregues ao JavaScript da aplicação: access e refresh tokens são gravados no servidor em cookies `HttpOnly`, `SameSite=Strict` e `Secure` em produção. A rotação acontece no backend; o token anterior é revogado e não pode ser reutilizado. O Proxy faz somente a renovação antecipada e o redirecionamento otimista, enquanto a API continua executando a autorização real por policy.

O bootstrap inicial depende exclusivamente de `BootstrapAdmin__Email`, `BootstrapAdmin__Password` e `BootstrapAdmin__FullName`. Ele não altera senhas existentes e deixa de criar usuário assim que existe um membro do papel `ADMIN`. Secrets reais devem permanecer no App Service Configuration ou Key Vault, nunca no repositório.

## 7. ADR — Evidência fotográfica em Blob privado

**Data:** 2026-10-16
**Status:** Aceita para revisão

Cada leitura pode possuir uma única evidência principal imutável. O Azure SQL armazena metadados, SHA-256, autoria e referência interna; os bytes originais ficam no Azure Blob Storage em container privado. A API transmite o conteúdo após autenticação, sem expor URL pública permanente ou SAS.

O upload é separado da criação da leitura para preservar o fluxo mobile/offline futuro. Reenvios com o mesmo `ClientEventId` ou com o mesmo hash na mesma leitura são idempotentes. Uma imagem diferente nunca substitui silenciosamente a evidência existente. Não existe delete físico nem update de arquivo.

O acesso ao SDK Azure permanece na Infrastructure por meio de `IPhotoStorage`. Produção usa Managed Identity preferencialmente, com connection string somente como fallback por secret. Testes usam storage em memória e são proibidos de acessar recursos Azure reais. Em falha SQL após o upload, o serviço executa remoção compensatória do blob e registra falhas de cleanup.

Detalhes operacionais e configuração estão em [fase-4-evidencia-fotografica.md](fase-4-evidencia-fotografica.md).

## 8. ADR — OCR auxiliar, persistente e limitado

**Data:** 2026-10-18
**Status:** Implementada em branch, aguardando validação e revisão

O valor operacional pertence ao operador; OCR nunca altera leitura ou evidência. A foto é persistida antes da análise síncrona com timeout. Cada análise tem um PhotoOcrResult, com histórico 1:N, estado e comparação persistidos. Resultados concluídos são imutáveis. Falha externa não invalida upload.

A Application usa IPhotoOcrProvider e IMeterReadingExtractor. A Infrastructure implementa Azure AI Vision por REST autenticado com bytes privados, preferindo Managed Identity. Um fake local retorna ausência de detecção. Texto persistido é limitado a 4000 caracteres e não se inventa confiança. Unidade desconhecida não é inferida.

Divergência usa limite absoluto OU percentual; operador zero torna o percentual indisponível. Baixa confiança e divergência geram anomalias auxiliares, únicas por resultado/tipo. A reserva SQL usa lock na evidência e unicidade da tentativa inicial. Reprocessamento manual preserva histórico, exige supervisão e aplica cooldown compartilhado entre instâncias. GET nunca chama OCR.

Não há worker de recuperação nesta fase: queda do processo pode deixar PROCESSING no histórico; uma nova tentativa manual é permitida depois do prazo documentado. O Image Analysis 4.0 tem retirada anunciada para 25/09/2028; a interface permite substituição de provider. Configuração real Azure e calibração com fotos operacionais permanecem tarefas de implantação posteriores à revisão.

A migration AddPhotoOcrResults foi gerada sem aplicação no Azure; as anteriores permanecem intactas. CI de PR usa SQL Server descartável e não executa deploy. Detalhes e resultados em [fase-5-ocr.md](fase-5-ocr.md) e [fase-5-relatorio-ocr.md](fase-5-relatorio-ocr.md).
