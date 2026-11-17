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
