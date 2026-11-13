# Fase 14 — relatórios e inteligência operacional

## Auditoria inicial

Base a767b16, merge da homologação da Fase 13. O serviço OperationalService já concentra resumo e decisões auditáveis. TransjapRepository possui filtros e paginação de leituras. A interface administrativa oferece detalhe da leitura, foto autorizada, OCR e histórico por frota. Os relatórios usam ExcelJS e pdf-lib com rotas autenticadas. A implementação acrescenta apuração de utilização a essa arquitetura.

## Regras implementadas

O endpoint administrativo GET /api/v1/operations/utilization usa a política CanViewDashboard. Considera captura em UTC e separa cada equipamento antes de ordenar os eventos. A chegada offline não governa o cálculo. Um intervalo exige dois registros aceitos, consecutivos e inteiramente dentro do período. A primeira captura no período fica sem cálculo, mesmo que haja histórico anterior. Não há interpolação, projeção nem estimativa para o fim do período. Ausência de evento não significa paralisação.

Validated sem alerta e sem decisão contrária é elegível. Uma aprovação explícita também é elegível. Rejeições, suspeitas sem aprovação e limites de correção impedem cálculo nos intervalos adjacentes. Capturas simultâneas são ambíguas. Valores decrescentes geram NEGATIVE_OR_RESET e não produzem horas. KM e unidade ausente não produzem HOURS. O valor é digitado pelo operador, nunca substituído por OCR. Duas capturas consecutivas na mesma obra permitem atribuição sob a hipótese de continuidade entre esses registros. Mudança ou ausência de obra deixa horas sem atribuição. Essa hipótese deve ser confirmada pelos responsáveis operacionais.

Os filtros de obra e operador são aplicados depois de identificar os vizinhos. No filtro de obra, intervalos não atribuíveis ficam sem cálculo para aquela obra. A soma usa decimal no servidor e fica indisponível quando não há nenhum intervalo elegível. Zero é reservado à diferença efetivamente nula. Motivos de indisponibilidade acompanham cada resultado.

Período padrão de 30 dias, máximo de 366 dias. A consulta projeta campos específicos, inclui revisões, correções e OCR por subconsultas, usa AsNoTracking e cancellation token. Limite de 10.000 leituras por período e frota, com rejeição explícita ao ultrapassar. O cálculo limitado é executado em memória após seleção SQL. A paginação é feita no servidor depois da apuração; os totais referem-se ao conjunto completo. Não há migrations novas.

## Interface e exportações

O dashboard apresenta horas calculáveis, intervalos indisponíveis, equipamentos com leituras, aprovadas e máquinas sem captura recente. O limiar de atenção é sete dias UTC e não representa inatividade. O histórico da apuração é paginado e leva ao detalhe autorizado. As barras por frota, obra e dia UTC e os motivos de indisponibilidade são agregados pelo servidor sobre o conjunto filtrado completo. A evolução e a tabela exibem somente a página indicada. A soma por obra acrescida das horas sem atribuição reconcilia com a soma por frota.

Filtros combináveis de período, máquina, frota, obra, operador, status, tipo, unidade, fotografia, anomalia, existência de revisão e último status OCR. O resumo, o histórico de leituras e a apuração compartilham os novos filtros. Datas da tela são dias UTC explícitos.

O novo relatório utilization reutiliza Excel e PDF. Exporta todos os resultados dentro do limite com leituras inicial e final, HOURS, obra e motivo. Mantém filtros, totais, emissão, valores numéricos, texto de célula sem fórmula e paginação PDF. O PDF segue o formato detalhado em campos já utilizado. Os gráficos não são exportados.

## Pontos ainda pendentes do escopo integral

O PR #27 foi integrado à main no merge a2ff8c235ef874c40c7271c18f478fb4f209468d. A CI 43454750104 aprovou 602 testes. A branch corretiva acrescenta um ensaio com SQL Server descartável para 100, 1.000, 5.000, 10.000 e 10.001 leituras. O teto é verificado por contagem limitada antes da projeção detalhada e novamente antes da apuração. O critério para a CI é p95 inferior a 10 segundos para navegação com três consultas simultâneas e nenhum erro inesperado em 30 consultas por volume. É um teto de regressão do runner compartilhado.

**Pendente de confirmação operacional.** Quando duas leituras consecutivas de uma máquina indicam a mesma obra, podemos considerar que todas as horas acumuladas entre elas pertencem a essa obra, mesmo sem registros intermediários?

Testes físicos reservados à Fase 16. Qualquer publicação ou aplicação de migration exige autorização posterior.

## Evidências de execução

A execução 43454178123 aprovou o backend Release, 265 testes unitários, 225 de integração com SQL Server 2022 descartável e 110 testes web. A revisão subsequente inclui filtro de captura recente e gráficos por dia e evolução, com mais dois testes SSR. Localmente, 112 testes web passaram, lint e build de produção concluíram. A CI final do PR #27 passou.

Os testes específicos incluem ordem de captura versus chegada, precisão decimal, duplicidade de horário, rejeição ou suspeita não aprovada, correção, reinício, unidade KM, mudança de obra, falta de leitura inicial, período cruzado e dados vazios. A integração verifica acesso anônimo/operador, validação do período, soma de três leituras aprovadas, paginação e exportação completa. Excel é reaberto para validar números, filtros, total e texto que começa com igual. PDF é reaberto para validar a estrutura. SSR verifica estado vazio, links autorizados, filtros e navegação.

Atenção para a hipótese de continuidade da obra e para os motivos em que não existe leitura inicial. Esses pontos precisam ser confirmados na homologação gerencial. A recuperação de falhas OCR e os testes das fases anteriores continuam na suíte de regressão.

## Homologação em ambiente isolado · 13 de novembro de 2026

O PR corretivo #28 reúne os testes e os ajustes. A API envia os totais decimais para a exportação e a página de relatórios apresenta a orientação de reduzir o período ou selecionar a frota quando a API responde 400. A listagem paginada e a exportação passam pelo mesmo serviço. O resumo envia à API também os filtros de unidade e evidência fotográfica.

No [ensaio de integração](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/43457151833), a API ASP.NET Core em TestServer consultou SQL Server 2022 em contêiner. Cada volume recebeu 30 consultas, em dez ondas de três, mais uma exportação integral. A medição da API inclui materialização e leitura do JSON pelo cliente de teste. A memória é o conjunto de trabalho do processo de testes, incluindo a API. Os números descrevem o runner Linux daquela execução.

| Leituras | p50 API | p95 API | Memória do processo | Erros inesperados |
|---:|---:|---:|---:|---:|
| 100 | 7 ms | 125 ms | 232 MiB | 0 |
| 1.000 | 33 ms | 54 ms | 242 MiB | 0 |
| 5.000 | 157 ms | 307 ms | 278 MiB | 0 |
| 10.000 | 303 ms | 376 ms | 323 MiB | 0 |
| 10.001 | HTTP 400 na página e na exportação | | | 0 |

A [inspeção Chrome](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/43457498687) exercitou o build de produção do Next.js com uma API simulada em loopback. As capturas de 1920 × 1080, 1366 × 768, 768 × 1024 e 390 × 844 estão no artefato fase14-inspecao. A largura do documento coincidiu com a viewport nas quatro resoluções. A frota sintética apresentou 5 HOURS no período, 4,75 na obra São João e 0,25 sem atribuição. O histórico abriu por equipamento e o total permaneceu em 5 HOURS na segunda página. Também foram exercitados o estado vazio e o aviso do limite.

Foram reabertos XLSX e PDF de 21 e 10.000 registros. Na primeira execução com Chrome, os arquivos menores levaram 0,186 e 0,045 s, respectivamente. Com 10.000 registros foram 0,658 e 3,932 s. A verificação percorreu as 2.353 páginas do PDF maior e conferiu texto e limites físicos das páginas. Sem dados, as duas rotas responderam 404. A suíte verifica que texto iniciado por igual permanece uma string no Excel. O PDF detalhado desse volume é extenso para consulta manual.

A política CanViewDashboard e a rota de exportação restringem acesso a supervisor, administrativo, gerente e administrador. A integração cobre 401 anônimo e 403 operador na API. A rota Next recusa exportação a operador mesmo com acesso direto. O novo contrato expõe agregações, contagens e identificadores das leituras paginadas. Fotografias, credenciais e e-mails de operadores não são necessários nesses dados. O ensaio usa base descartável protegida pelo TestDatabaseGuard.

O aceite da hipótese de continuidade depende do responsável operacional. Testes físicos pertencem à Fase 16. Os resultados acima são de desenvolvimento e CI.

### Medição com instrumentação

A [execução 43458122793](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/43458122793) passou com instrumentação por etapa. A coluna de consulta mede a leitura via EF, incluindo transporte e materialização. A coluna de apuração inclui agrupamento, filtros, agregação e paginação em memória. Os percentis são independentes. O serviço respeitou o token já cancelado e a consulta subsequente retornou 200.

| Leituras | p50 API | p95 API | p95 consulta EF | p95 apuração | Memória do processo |
|---:|---:|---:|---:|---:|---:|
| 100 | 6 ms | 62 ms | 50,8 ms | 0,5 ms | 371 MiB |
| 1.000 | 33 ms | 52 ms | 41,7 ms | 17,5 ms | 377 MiB |
| 5.000 | 137 ms | 189 ms | 159,9 ms | 33,3 ms | 429 MiB |
| 10.000 | 303 ms | 348 ms | 335,1 ms | 55,7 ms | 447 MiB |

No mesmo runner de frontend, a exportação de 10.000 registros via Next levou 0,552 s para Excel e 3,090 s para PDF sobre a API simulada. A CI testa também a orientação de limite na página de relatórios e o HTTP 400 nas duas rotas de download. A medição de memória inclui o processo de testes e varia com a ordem dos testes. Cancelamento de comando SQL por falha de rede em produção e carga do Azure ficam fora da evidência deste ensaio.
