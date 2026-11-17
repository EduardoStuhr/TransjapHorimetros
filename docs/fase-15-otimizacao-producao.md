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
