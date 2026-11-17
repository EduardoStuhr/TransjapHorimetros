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
