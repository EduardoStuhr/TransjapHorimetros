# Relatório de implementação da Fase 10

## Entrega

Dashboard com agregações SQL, filtros de captura UTC, agrupamento diário e por obra e 20 máquinas ativas com recebimento mais antigo. Agrupamentos exibem até 100 obras e 366 dias e identificam esses limites. Os indicadores anteriores continuam globais, enquanto a seção Supervisão operacional recebe filtros.

Revisão de leituras pendentes/suspeitas e propostas de correção com confirmação, justificativa, usuário e data. Uma decisão imutável por leitura, chave primária no banco, gravação atômica com auditoria e recuperação de retentativa equivalente. Aprovação é uma decisão administrativa vinculada ao original. Valores operacionais e diferenças históricas não são recalculados.

Alertas paginados e filtrados por status com reconhecimento/resolução auditados. O status original esperado e o token de concorrência impedem decisões sobre versões desatualizadas.

Auditoria paginada com filtros por identificador. A resposta pública contém ação, recurso, ator e data, sem os payloads internos.

Exportações server-side reutilizam a API autenticada, recebem período, frota, máquina, obra, operador, status, tipo e anomalias. Excel inclui números e datas tipados. PDF apresenta os campos completos em registros detalhados com paginação. A saída inclui unidade, valor anterior derivado da diferença persistida, diferença, operador, OCR e dispositivo informado. Horas e quilômetros não são somados.

## Limitações de aceite

A autorização de revisão continua baseada na policy existente CanReviewAnomalies. Operadores não recebem permissão administrativa. Foto, OCR, QR, identidade de dispositivo e sincronização reutilizam os fluxos anteriores.

O relatório não introduz regras de fraude, tempo limite sem leitura, foto obrigatória ou produtividade. A regra provisória de ausência de leituras continua identificada no Dashboard anterior.

A aprovação de uma proposta não substitui automaticamente a leitura operacional. Essa distinção está descrita na interface e preserva o contrato imutável das Fases 6 e 9.

## Banco e implantação

Migration AddReadingReviews cria apenas reading_reviews com chave estrangeira restritiva para hour_meter_readings. A chave primária ReadingId limita a uma decisão por leitura. O token de concorrência em anomalias é metadado EF sobre a coluna existente.

Antes de produção, conferir o histórico até AddRegisteredDeviceIdentity, confirmar ponto de restauração disponível, gerar script EF incremental e validar o SQL. Aplicar a migration antes de publicar a API e o frontend desta branch. Após publicação, validar contagens, exportações, revisão, auditoria e coleta offline em aparelho real.

A implantação e a migration de produção aguardam autorização específica da Fase 10. O código está em branch própria.

## Validação

Frontend: npm ci, lint, testes automatizados e build executados localmente. Novas regressões validam preservação dos filtros, rejeição de intervalos/IDs inválidos, XLSX com filtros e números e PDF multipágina.

Backend: restore e build Release executados localmente. Testes de integração adicionados para autorização de operadores, tradução SQL das agregações, filtros e período inválido, decisões concorrentes, retentativa e preservação do registro original.

A CI do PR executa o conjunto completo com SQL Server descartável, incluindo migrations e regressões das Fases 1 a 9. O resultado da CI deve ser conferido no commit final antes do aceite.
