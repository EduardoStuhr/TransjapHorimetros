# Homologação da Fase 13 — qualidade do OCR

12/11/2026, a partir das 15h43 (Brasília). Escopo de produção: consultas de disponibilidade, logs e metadados. Escopo funcional: dados sintéticos e SQL Server descartável da CI. Os testes físicos ficaram para a Fase 16.

**Parecer: FASE 13 APROVADA COM RESSALVAS. A homologação de produção continua aberta até comprovar o schema e a operação do worker.**

| Verificação | Evidência | Resultado |
|---|---|---|
| Código integrado | [PR #23](https://github.com/EduardoStuhr/TransjapHorimetros/pull/23) integrado à main em d169ff3e58a2f05ea0c62e10f4cfb4c4440023d1. A árvore Git do merge e a da revisão testada 84fb693 são idênticas, 3b336afc22ed5a69a2355806c8daddc306fd19fb. | Confirmado |
| Deploy da API | [Build e deploy](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/43280923278) aprovados no merge. O job de deploy confirmou a publicação pelo App Service às 15h33min56s. Portal mostrou Em execução e runtime .NET 10 íntegro. | Confirmado |
| API e SQL | GET /health e /health/ready retornaram HTTP 200, HEALTHY para self e sqlserver. Trace IDs c9208c1335a147ea91fa146fd5140b1a e 475c0554471f43abac38974dd27fc84c. A verificação de integridade gerenciada do App Service da API aparecia como não configurada. | Disponibilidade e conectividade confirmadas |
| Deploy do frontend | [Build e deploy](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/43280923321) aprovados no mesmo merge. Publicação confirmada às 15h35min03s. Portal mostrou Em execução e Node 26 íntegro. GET / seguiu para /login?returnTo=%2F com HTTP 200. | Confirmado |
| Logs do frontend | O fluxo de logs em janela de 60 minutos exibiu 119 entradas e a mensagem de inicialização “Ready in 0ms”. Houve avisos de certificados duplicados durante rehash. A página de login respondeu. | Inicialização confirmada |
| Azure SQL | Editor aberto para pg-transjap-horimetros / transjap_horimetros. O servidor recusou autenticação Microsoft Entra. O editor solicitou autenticação SQL. | Histórico, seis colunas e índice ainda sem resultado de SELECT |
| Integração SQL | [CI aprovada](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/43280223532). O job backend registrou 258 testes unitários e 222 de integração aprovados, zero falhas e zero ignorados, com SQL Server 2022 descartável. | Confirmado em isolamento |
| Frontend e regressão | CI com 108 testes de frontend, lint e build. Reexecução local sobre árvore idêntica ao merge: 258 unitários e 108 de frontend aprovados, build Release sem erros ou avisos, build de produção do Next.js concluído. Os 222 testes de integração são resultado da CI. | 588 aprovados na CI, 0 falhas. A reexecução local de 366 não aumenta esse total. |

## OCR, painel e segurança

O MeterReadingExtractor distingue ponto e vírgula, unidades e candidatos concorrentes. Formatos ambíguos e confiança ausente exigem revisão. PhotoOcrService e PhotoOcrResult gravam o resultado separado da leitura digitada. AzureAiVisionOcrProvider limita o conteúdo enviado e recebido. OcrProcessingGate limita paralelismo, PhotoOcrWorker recupera processamento interrompido e PhotoOcrRepository serializa tentativas e impõe idempotência por chave.

Os testes OcrQualityIntegrationTests verificam preservação da evidência e da leitura, acesso de operador restrito à própria foto, proteção do histórico e do reprocessamento, recuperação auditada, alerta único em falhas repetidas, preservação de alerta resolvido e conflito em reutilização concorrente de chave para fotos diferentes. OcrWorkerIntegrationTests exercita upload e finalização assíncrona. Foram usadas imagens sintéticas. O processamento no provedor Azure AI Vision em produção e a acurácia com máquinas reais permanecem sem medição nesta homologação.

O detalhe de leitura e o painel OCR apresentam valor digitado, valor extraído, estado, divergência e confiança quando disponível. O acesso à foto é verificado no servidor. Histórico, texto bruto e candidatos são restritos à supervisão. A CI cobre autorização, apresentação e regras de alertas. O log de resultado registra identificadores, estado, duração e confiança. Não foi feita auditoria exaustiva de segredos em toda a plataforma. A suíte de regressão da árvore integrada inclui as funcionalidades da Fase 12.

## Conferência do schema em produção

A migration 20261112180316_AddOcrQualityMetadata sucede 20261111170305_AddReadingReviews. O script incremental do EF prevê seis colunas anuláveis em photo_ocr_results e um índice único filtrado. Não cria tabela e não atualiza leituras existentes. O resultado esperado abaixo vem do código. Execute a consulta no banco de destino para obter o resultado observado.

```sql
SELECT @@SERVERNAME AS servidor, DB_NAME() AS banco;
SELECT SCHEMA_NAME(schema_id) AS esquema, name FROM sys.tables
WHERE name IN ('__EFMigrationsHistory','photo_ocr_results');

-- Usar o esquema retornado pelo catálogo, se diferente de dbo.
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory
WHERE MigrationId IN ('20261111170305_AddReadingReviews','20261112180316_AddOcrQualityMetadata')
ORDER BY MigrationId;

SELECT c.name, TYPE_NAME(c.user_type_id) AS tipo, c.max_length AS bytes,
       c.precision, c.scale, c.is_nullable
FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.photo_ocr_results')
AND c.name IN ('AlgorithmVersion','ReviewReason','CandidatesJson','PreviousReadingValue','BelowPreviousReading','RequestId');

SELECT i.name, c.name AS coluna, ic.key_ordinal, i.is_unique, i.is_disabled, i.filter_definition
FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID(N'dbo.photo_ocr_results') AND i.name='ux_photo_ocr_request';
```

Esperado: AlgorithmVersion e ReviewReason nvarchar(64), CandidatesJson nvarchar(4000), PreviousReadingValue decimal(12,2), BelowPreviousReading bit e RequestId uniqueidentifier. Índice habilitado e único sobre RequestId, filtrado por RequestId IS NOT NULL.

Se a migration estiver ausente, verificar o histórico completo, o destino da API e a recuperação point-in-time. Se houver aplicação parcial, interromper e reconciliar o catálogo antes de propor DDL. Caso falte somente essa migration, submeter o incremental revisado para autorização. A criação do índice e os bloqueios de schema podem afetar o worker e consultas concorrentes. Aplicar em janela combinada e repetir as consultas, o health e a leitura dos logs. Evitar reiniciar a API para tentar migrar: a rotina de inicialização pode aplicar outras migrations e executar seed, dependendo da configuração.

## Ocorrência na API e critério de encerramento

O App Service exibiu 482 entradas na janela de 60 minutos consultada. Às 14h51min36s de Brasília, antes do deploy, /api/v1/auth/refresh registrou timeout do SQL na fase posterior ao login. A nova API iniciou às 15h34min25s. Às 15h35min05s o EF registrou o mesmo tipo de timeout e o PhotoOcrWorker registrou falha de lote. O log não identifica a causa de infraestrutura do timeout. Não apareceu mensagem de coluna inválida na amostra exibida.

Às 15h50min29s o EF registrou as consultas de recuperação de análises interrompidas e de busca de fotos pendentes, ambas concluídas em aproximadamente 2 ms. A varredura do worker retomou. Essas consultas usam campos antigos, portanto não demonstram a presença das seis colunas nem o processamento completo de uma imagem na instância publicada.

Para encerrar: conferir histórico, colunas e índice no editor SQL autenticado, verificar se os timeouts voltaram a ocorrer e confrontar os horários com conexões e métricas do Azure SQL. Se persistirem, investigar demora de conexão e disponibilidade do banco antes de ajustar parâmetros. Para comprovar o ciclo completo com o provedor efetivo, usar ambiente isolado equivalente com evidência sintética ou observar passivamente um processamento autorizado. Nenhuma alteração de banco, configuração, plano, merge ou deploy foi realizada na homologação.
