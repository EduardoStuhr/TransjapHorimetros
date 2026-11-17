# Fase 15 — otimização e preparação para produção

Estado em 17/11/2026: PENDENTE. Base 19d355a87417b8043dcf301d4e34c9bcc220b4f0, merge do PR #29. Os deploys da API e do frontend associados a esse commit terminaram com sucesso nas execuções 43461525265 e 43461525307. A disponibilidade e a versão em execução no Azure não foram verificadas nesta auditoria.

## Matriz inicial de achados

| Prioridade | Evidência | Impacto e probabilidade | Correção e risco |
|---|---|---|---|
| Alta | O transporte encerrava seu timeout logo após fetch retornar os cabeçalhos, antes de consumir response.text(). | Um corpo interrompido pode manter a fila travada indefinidamente. Probabilidade depende de falha da rede. | Manter o timeout e o cancelamento até terminar a leitura. A resposta continua sujeita às regras existentes de retry e idempotência. Risco de regressão no tratamento de HTTP validado por testes. |
| Alta | Os workflows de publicação executam deploy em push na main depois de seus próprios builds, sem dependência explícita da CI de PR. | Uma alteração na main pode ser publicada antes da validação completa. Probabilidade depende do processo de merge. | Revisar proteção da branch e aprovação de ambiente com responsáveis antes de modificar o fluxo. Sem alteração de workflow neste PR. |
| Média | Program.cs usa ApplyMigrationsOnStartup=true por padrão. | Configuração não informada pode aplicar migrations na inicialização. A configuração efetiva no Azure não foi verificada. | Confirmar configuração real e estratégia de migrations controladas antes de alterar o comportamento. |
| Média | A comparação de carga de 1, 5, 10 e 30 usuários ainda não foi executada nesta fase. | A capacidade operacional em Azure permanece desconhecida. | Executar testes isolados e registrar p50/p95, erros e recursos antes da aprovação. |

## Correção e verificações pendentes

O limite de 30 segundos passou a cobrir a leitura do corpo de resposta das operações de sincronização. Falhas HTTP preservam status e Retry-After. A fila e os identificadores de idempotência mantêm seu comportamento existente.

Permanecem pendentes a auditoria integral de segurança, o teste de carga solicitado para 1, 5, 10 e 30 usuários, planos SQL e baseline comparativa, simulações offline completas, revisão de OCR, fotos, custos e configuração efetiva do Azure, e um plano de rollback validado. Os testes físicos ficam para a Fase 16. Não há mudança em produção nem migration neste PR.

## Continuação — 17/11/2026

Os workflows do merge 8735270b4d568041b3f28f5bfb19933c356dce3e concluíram com sucesso para [API](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/44087440071) e [frontend](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/44087439889). A consulta remota de health não foi acessível e a navegação para readiness excedeu o tempo de espera. A versão efetivamente em execução, logs e configuração real de migrations não foram confirmados. Evidências disponíveis não permitem concluir se existe incidente.

| Área | Evidência de código | Avaliação |
|---|---|---|
| Saúde | Program.cs registra self em live/ready e SQL somente em ready. | Liveness não executa SQL; estado real dos endpoints pendente. |
| Offline | sync-engine.test.ts cobre resposta incompleta, cancelamento, retry com ClientEventId, falha de foto, troca de usuário, expiração e lease entre abas. indexed-db-store.test.ts cobre reabertura e retenção de pendentes. | Testes automatizados simulados; experiência física fica para Fase 16. |
| Segurança | Cookies usam HttpOnly, SameSite Strict e Secure em produção. Políticas do backend exigem papéis administrativos. | Revisão de endpoints e logs não foi concluída integralmente. |
| Persistência | Há índices de ordenação, idempotência e OCR nas configurações EF. UseSqlServer configura retries para falhas transitórias. | Planos SQL reais e custo dos índices não foram medidos. |
| Observabilidade | Console JSON, correlation ID, métricas HTTP e logs do worker OCR existem. | Validar logs efetivos, dados sensíveis e alertas no Azure. |

Os cenários de carga adicionados à integração usam dados sintéticos e SQL Server descartável. As medições são locais ao runner. Não representam o Azure nem incluem login, upload, sincronização ou exportação XLSX/PDF. O runbook proposto está em runbook-producao.md e requer ensaio de recuperação em ambiente isolado. Fase 15 permanece PENDENTE.

### Baseline concorrente em SQL Server descartável

A execução [44088973812](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/44088973812) usou 1.000 leituras sintéticas, ASP.NET TestServer e SQL Server 2022 em contêiner. Cada usuário executou cinco sequências de resumo, utilização paginada, histórico filtrado por máquina e consulta integral para exportação. Os números medem requisições e materialização da resposta em milissegundos. Não incluem rede real nem geração de XLSX/PDF.

| Concorrência | Amostras por rota | Resumo p50/p95 | Utilização p50/p95 | Histórico p50/p95 | Dados para exportação p50/p95 | Erros | Memória processo |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 5 | 12,8 / 137,1 | 14,2 / 17,4 | 4,1 / 61,5 | 15,3 / 15,5 | 0 | 326 MiB |
| 5 | 25 | 36,7 / 56,2 | 28,8 / 55,8 | 8,6 / 18,4 | 39,0 / 56,6 | 0 | 336 MiB |
| 10 | 50 | 80,4 / 128,3 | 56,8 / 108,5 | 18,0 / 41,7 | 63,0 / 95,5 | 0 | 344 MiB |
| 30 | 150 | 248,1 / 463,7 | 111,4 / 225,1 | 55,2 / 207,2 | 124,5 / 237,5 | 0 | 394 MiB |

A primeira amostra inclui aquecimento, especialmente perceptível na concorrência 1. Tempo de CPU agregado do processo por cenário foi 322, 815, 1.768 e 5.223 ms, respectivamente. Não foi medida CPU do contêiner SQL. A instrumentação existente registra tempos de consulta da utilização na suíte de volume, mas não foram coletados por concorrência neste experimento. A comparação antes/depois e os cenários de autenticação, sincronização e geração de relatórios continuam pendentes.

## Etapa final de 17/11/2026

Às 13h37 UTC, a API respondeu HTTP 200 em /health/live e HTTP 503 em /health e /health/ready. A resposta de readiness indicou self HEALTHY e sqlserver UNHEALTHY, com a descrição “SQL Server indisponível.” e o traceId 6564c6a6de5e43d7a04740dad5f20f90. O frontend respondeu HTTP 307 na raiz. A causa da falha, a duração, as versões efetivamente implantadas e as configurações do App Service exigem evidência do Azure. Consultar o runbook antes de qualquer recuperação.

A revisão identificou que appsettings.Production.json já desabilitava migrations, mas uma sobrescrita de configuração podia habilitá-las. A branch restringe a chamada conjunta de migration e seed a Development e Testing. Uma base nova de produção deve ser provisionada e populada mediante procedimento separado.

O build local apontou uma vulnerabilidade na versão 16.4.3 do Next.js. A revisão atualiza o runtime para o patch 16.4.6. A carga de exportações usa arquivos gerados pela rota real do Next, uma fixture HTTP local de 21 intervalos e cinco solicitações por usuário. O teste de operadores usa login por senha, replay do mesmo ClientEventId e upload multipart de JPEG sintético com armazenamento em memória e SQL Server descartável. O cenário não mede a latência do Blob Storage, do Azure AI nem o tempo em rede móvel.

A visão geral do banco no Portal Azure confirmou Status Paused, limites da oferta gratuita atingidos e 0 segundos restantes de vCore no mês. O esgotamento de cota é consistente com o 503 da API. Não há evidência de que o merge tenha causado o consumo. O portal oferece continuidade com encargos adicionais, que exige decisão de orçamento. A versão implantada, os logs do processo e a configuração efetiva continuam sem confirmação.

No build 16.4.6, a carga local da rota de relatórios gerou 230 XLSX e 230 PDF medidos, além do aquecimento. Cada XLSX foi reaberto como ZIP com verificação de CRC e planilha presente. Cada PDF foi aberto e teve o filtro de frota conferido. Tempos de resposta em ms, p50/p95, por 1/5/10/30 usuários:

    xlsx users=1 n=5 p50=28.3 p95=45.0ms
    xlsx users=5 n=25 p50=70.6 p95=107.6ms
    xlsx users=10 n=50 p50=75.5 p95=95.2ms
    xlsx users=30 n=150 p50=236.8 p95=324.5ms
    pdf users=1 n=5 p50=15.2 p95=20.4ms
    pdf users=5 n=25 p50=55.0 p95=93.5ms
    pdf users=10 n=50 p50=86.3 p95=108.5ms
    pdf users=30 n=150 p50=325.9 p95=352.0ms

A primeira execução de carga reproduziu erro 500 no POST da leitura a 30 operadores. O TRX da execução 44095429390 registrou deadlock SQL 1205 e esgotamento de cinco retries em ReadingSummaryRepository.GetAsync durante a transação de escrita. Com 10 operadores, o p95 de sync chegou a 17.219,5 ms. A correção move a consulta de apresentação de anomalias, OCR e correções para depois do commit. A decisão, a leitura e a auditoria continuam na transação protegida por máquina. Se a resposta se perder após o commit, o cliente pode reenviar o mesmo ClientEventId.

### Revisão de segurança e operação

| Área | Evidência de implementação ou teste | Limite |
|---|---|---|
| Autenticação | JWT valida assinatura, emissor, audiência e expiração. Login e refresh admitem acesso anônimo. Logout e me exigem autenticação. O rate limiter de login usa IP. | A factory de carga eleva o limite para medir o caminho de sucesso. |
| Permissões | A política padrão exige usuário autenticado. Gestão de usuários requer Admin. Dispositivos exigem Manager/Admin. Dashboard, anomalias e histórico OCR exigem perfil de supervisão. | A leitura efetiva de configurações e logs do App Service continua pendente. |
| Fotos | EvidenceAccess verifica autoria para operador e retorna 404 quando fora do escopo. O serviço valida o arquivo e confere autoria e leitura no replay. Os testes cobrem tamanho, tipo, ausência de token, papel e conteúdo imutável. | A carga substitui o storage por memória, com JPEG sintético mínimo. |
| OCR | Histórico e reprocessamento usam política administrativa. O serviço verifica ator no replay manual e persiste tentativas e auditoria. | A integração usa provedor isolado. |
| Dados e logs | Middleware registra rota, status, duração e correlação. Metadados de storage ficam fora da resposta de evidência. Problemas inesperados recebem mensagem pública genérica. | Logs efetivos no Azure e retenção externa exigem conferência. |
| Frontend | A CI testa filtros, estado vazio, erro de volume, paginação e larguras de 390 a 1920 px. O painel tem tabela com legenda e alternativa textual ao gráfico. O fallback de carregamento usa status acessível e a falha global anuncia o erro. | Testes físicos ficam na Fase 16. |

O npm audit de dependências de produção registrou duas entradas moderadas relacionadas ao mesmo aviso no uuid transitivo de exceljs (GHSA-w5hq-g745-h8pq), sem altas ou críticas. O código instalado do ExcelJS importa v4, enquanto o aviso envolve v3/v5/v6 com buffer fornecido. A dependência permanece sinalizada.

O ensaio local de rollback do frontend serviu primeiro o standalone 16.4.6 e depois o artefato anterior 16.4.3 no mesmo endereço, com fixture isolada. Ambos carregaram o login e entregaram PDF legível de cinco páginas para a frota 84. Este teste cobre compatibilidade da rota. A versão anterior tem alerta de segurança e exige avaliação antes de eventual uso real.

O handler passa a responder 503 com Retry-After quando a estratégia EF esgota as tentativas transitórias. O middleware de métricas envolve o handler para registrar o status final e usa o template da rota no log, evitando identificadores no caminho.

### Parecer técnico de 17/11/2026

**Fase 15 PENDENTE em produção.** A verificação posterior no Portal encontrou o App Service da API em execução, verificação de integridade do App Service não configurada, log do PhotoOcrWorker com erro SQL 42119 e referência à cota gratuita. As configurações individuais exibidas foram ASPNETCORE_ENVIRONMENT=Production e Database__ApplyMigrationsOnStartup=false. O frontend redirecionou a raiz para o login e respondeu HTTP 200 nessa página. Os workflows da API (44092836786) e web (44092836544) concluíram com sucesso para 0b202132. O SHA carregado pelos processos permanece sem comprovação independente.

A [CI 44096801024](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/44096801024) passou no commit 3118a0d. Foram 265 testes unitários do backend, 230 de integração e 113 do frontend, total de 608, zero falhas. Inclui build Release, lint, build Next.js, inspeção Chrome, carga dos operadores e ensaio da base restaurada. No ensaio, a guarda em Production impediu a criação de banco mesmo com a flag ligada. A API candidata e a anterior passaram no ready e na rejeição de leitura anônima contra a cópia, com igualdade das tabelas após cada inicialização.

| Operadores | Login p50/p95 ms | Sync p50/p95 ms | Replay p50/p95 ms | Foto p50/p95 ms |
|---:|---:|---:|---:|---:|
| 1 | 62,7 / 67,7 | 11,1 / 37,5 | 6,1 / 6,6 | 13,8 / 14,9 |
| 5 | 117,7 / 187,7 | 31,3 / 55,6 | 16,8 / 25,0 | 40,3 / 74,8 |
| 10 | 186,2 / 293,1 | 50,7 / 111,3 | 28,8 / 58,4 | 73,2 / 113,0 |
| 30 | 398,7 / 850,2 | 205,0 / 536,9 | 120,1 / 313,3 | 276,7 / 632,6 |

Cada operador fez cinco sequências. Os 230 replays retornaram os IDs originais e a contagem final por cenário correspondeu aos eventos distintos. A verificação automática de startup, backup e restore rodou somente na instância SQL descartável.

No artefato fase15-query-plans da mesma CI, o plano real do lookup por ClientEventId usou Index Seek em ux_hour_meter_readings_client_event_id, lendo uma linha. Para TOP 20 por máquina e HOURS, o SQL usou ix_readings_capture_order e 20 buscas no índice clusterizado. O scan leu 20 linhas para retornar 20 nesse conjunto concentrado em uma máquina. Esse plano não fundamenta um índice adicional para a distribuição de produção.

Para encerrar, confirmar a recuperação da cota e do ready, funcionamento de consulta autenticada e do worker, obter os SHAs reais dos serviços e validar a política externa de aprovação e a disponibilidade de artefatos de recuperação. A aprovação de publicação e qualquer mudança de plano ou infraestrutura são decisões separadas. A Fase 16 mantém os testes de campo.
