# Fase 14 — relatórios e inteligência operacional

## Auditoria inicial

Base a767b16, merge da homologação da Fase 13. O serviço OperationalService já concentra resumo e decisões auditáveis. TransjapRepository possui filtros e paginação de leituras. A interface administrativa oferece detalhe da leitura, foto autorizada, OCR e histórico por frota. Os relatórios usam ExcelJS e pdf-lib com rotas autenticadas. A implementação acrescenta apuração de utilização a essa arquitetura. Produção permanece na versão previamente homologada.

## Regras implementadas

O endpoint administrativo GET /api/v1/operations/utilization usa a política CanViewDashboard. Considera captura em UTC e separa cada equipamento antes de ordenar os eventos. A chegada offline não governa o cálculo. Um intervalo exige dois registros aceitos, consecutivos e inteiramente dentro do período. A primeira captura no período fica sem cálculo, mesmo que haja histórico anterior. Não há interpolação, projeção nem estimativa para o fim do período. Ausência de evento não significa paralisação.

Validated sem alerta e sem decisão contrária é elegível. Uma aprovação explícita também é elegível. Rejeições, suspeitas sem aprovação e limites de correção impedem cálculo nos intervalos adjacentes. Capturas simultâneas são ambíguas. Valores decrescentes geram NEGATIVE_OR_RESET e não produzem horas. KM e unidade ausente não produzem HOURS. O valor é digitado pelo operador, nunca substituído por OCR. Duas capturas consecutivas na mesma obra permitem atribuição sob a hipótese de continuidade entre esses registros. Mudança ou ausência de obra deixa horas sem atribuição. Essa hipótese deve ser confirmada pelos responsáveis operacionais.

Os filtros de obra e operador são aplicados depois de identificar os vizinhos. No filtro de obra, intervalos não atribuíveis ficam sem cálculo para aquela obra. A soma usa decimal no servidor e fica indisponível quando não há nenhum intervalo elegível. Zero é reservado à diferença efetivamente nula. Motivos de indisponibilidade acompanham cada resultado.

Período padrão de 30 dias, máximo de 366 dias. A consulta projeta campos específicos, inclui revisões, correções e OCR por subconsultas, usa AsNoTracking e cancellation token. Limite de 10.000 leituras por período e frota, com rejeição explícita ao ultrapassar. O cálculo limitado é executado em memória após seleção SQL. A paginação é feita no servidor depois da apuração; os totais referem-se ao conjunto completo. Não há migrations novas.

## Interface e exportações

O dashboard apresenta horas calculáveis, intervalos indisponíveis, equipamentos com leituras, aprovadas e máquinas sem captura recente. O limiar de atenção é sete dias UTC e não representa inatividade. O histórico da apuração é paginado e leva ao detalhe autorizado. Os gráficos de barras por frota e obra descrevem a página atual.

Filtros combináveis de período, máquina, frota, obra, operador, status, tipo, unidade, fotografia, anomalia, existência de revisão e último status OCR. O resumo, o histórico de leituras e a apuração compartilham os novos filtros. Datas da tela são dias UTC explícitos.

O novo relatório utilization reutiliza Excel e PDF. Exporta todos os resultados dentro do limite com leituras inicial e final, HOURS, obra e motivo. Mantém filtros, totais, emissão, valores numéricos, texto de célula sem fórmula e paginação PDF. O PDF segue o formato detalhado em campos já utilizado. Os gráficos não são exportados.

## Pontos ainda pendentes do escopo integral

O PR continua em rascunho. Faltam filtro específico de equipamentos sem leitura recente, evolução em gráfico de linha, gráfico de horas por dia/semana e visualização dedicada de inconsistências. Falta ensaio de carga e inspeção visual do novo painel em navegador. A paginação calcula até 10.000 registros em memória; períodos de grande volume exigem frota ou janela menor. Não há teste de desempenho para crescimento além desse limite. Esses itens impedem declarar a Fase 14 integralmente pronta.

Testes físicos reservados à Fase 16. Qualquer publicação ou aplicação de migration exige autorização posterior.
