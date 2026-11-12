# Fase 12 — diagnóstico de confiabilidade

Base c2c5353, merge do PR #21. Diagnóstico realizado antes das alterações em 12/11/2026.

| Mecanismo | Classificação | Risco, probabilidade e impacto |
| --- | --- | --- |
| ClientEventId com índice SQL único e captura de 2601/2627 | Implementado mas precisa de reforço | Alto, provável. Conteúdo divergente na mesma máquina é aceito como repetição. |
| Autoria e sessão de dispositivo | Implementado mas precisa de reforço | Alto, possível. O caminho de repetição antecede a comprovação de dispositivo. |
| SaveChanges com leitura, anomalias e auditoria | Já implementado e adequado | Transação SQL atômica, entidades históricas protegidas contra alteração/exclusão pelo contexto. |
| Duas leituras da mesma máquina | Ausente e necessário | Alto, possível. Avaliações concorrentes podem calcular o mesmo predecessor. |
| Fila IndexedDB com captura de leitura e foto atômicas | Já implementado e adequado | Sobrevive à reabertura, mantém ClientEventId e blob. |
| Recuperação de SYNCING e Web Locks | Implementado mas precisa de reforço | Alto, possível em navegadores sem Web Locks. Bloqueio fallback só protege uma aba. |
| Limpeza por retenção | Implementado mas precisa de reforço | Alto, possível. Leitura sincronizada pode ser removida enquanto uma foto ainda depende dela. |
| HTTP 409 com resourceId | Implementado mas precisa de reforço | Alto, possível. Identificador isolado não prova igualdade do evento. |
| Retentativas offline | Implementado mas precisa de reforço | Médio, provável. Falta jitter, Retry-After, cancelamento externo e limite por ciclo. |
| Upload, checksum e índice único por leitura | Implementado mas precisa de reforço | Alto, possível. Repetição por ClientEventId ignora conteúdo e compensação após commit incerto pode excluir arquivo confirmado. |
| OCR com claim SQL, timeout e finalização auditada | Já implementado e adequado | Processamento auxiliar não altera leitura. Reinício usa claim com recuperação de processamento expirado. |
| EF EnableRetryOnFailure | Já implementado e adequado | Repetição SQL limitada, exige execution strategy ao introduzir transação explícita. |
| Correlation ID, logs HTTP, rate limits, health SQL | Já implementado e adequado | Reutilizar mecanismos existentes sem contratar infraestrutura. |
| Decisões administrativas concorrentes | Já implementado e adequado | Leitura com chave única, alertas com token de concorrência e auditoria na mesma transação. |

As correções e evidências serão registradas no relatório final. Alterações serão verificadas no SQL Server descartável da CI. A Fase 16 concentra a validação física mobile.
