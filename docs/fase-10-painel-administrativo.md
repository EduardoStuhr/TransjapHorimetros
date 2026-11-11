# Fase 10 — painel administrativo

## Diagnóstico antes da implementação

O backend possui paginação e filtros de leituras, resumo global do Dashboard, cadastro de máquinas/obras, anomalias somente para consulta, auditoria de escrita, OCR, evidências imutáveis e identidade de dispositivos. O frontend possui tabelas e detalhes, filtros de leituras, relatórios Excel/PDF gerados no servidor Next.js e painel de dispositivos.

As correções são novas leituras ligadas ao original e entram pendentes. Não existe decisão administrativa persistida. O DbContext impede alterações em leituras, fotos e auditoria. A revisão deve preservar essa garantia. Anomalias possuem status, mas ainda não possuem ação de revisão.

O Dashboard atual contém contagens globais sem filtros, usa lista de IDs ativos na aplicação e não oferece séries/agregações. Relatórios exportam todas as páginas e não propagam filtros. A exportação atual formata números como texto. A auditoria não possui consulta administrativa.

## Plano

1. Agregação operacional SQL com período, máquina/frota, obra, operador e status.
2. Decisões de revisão independentes da leitura original, únicas por leitura, com ator, justificativa e data. A unicidade no SQL deve arbitrar revisões concorrentes.
3. Relatórios com filtros propagados e valores numéricos.
4. Consulta paginada de auditoria e revisão de anomalias.
5. Regressões e CI em branch própria, com implantação e migration de produção sujeitas a autorização.

## Banco

Uma tabela incremental de decisões permite preservar leituras e auditoria imutáveis. A migration deve ser gerada com EF Core, testada em SQL Server descartável e aplicada somente após autorização de produção. Rollback de aplicação pode manter essa tabela. Recuperação de dados deve usar restauração point-in-time do Azure em banco separado, com validação antes de substituição.

## Semântica

Datas dos filtros correspondem à captura UTC, seguindo o contrato existente. Contagens de hoje usam o recebimento UTC. Horas e quilômetros têm unidades separadas e não são somados. Intervalos sem leitura seguem o parâmetro operacional provisório existente. Decisões de revisão não substituem o valor original nem recalculam retroativamente diferenças históricas.
