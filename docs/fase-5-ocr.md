# Fase 5 — OCR de evidência fotográfica

A foto é a prova, o texto é a leitura, o servidor é a verdade. OCR é evidência auxiliar sujeita a erro. Nenhum caminho desta implementação altera `HourMeterReading.Value`, status da leitura ou conteúdo/status da foto. A revisão e eventual correção continuam humanas.

## Arquitetura e fluxo

1. O upload existente valida e persiste o original no Blob privado, os metadados e sua auditoria.
2. Somente depois do commit da foto, `PhotoEvidenceService` chama `IPhotoOcrService`.
3. O serviço cria um `PhotoOcrResult` em `PROCESSING`, com uma reserva persistente antes de chamar o provider.
4. `IPhotoStorage` abre os bytes privados; `IPhotoOcrProvider` extrai texto e confiança por palavra.
5. `IMeterReadingExtractor` tenta identificar um valor conservador. `OcrComparison` centraliza a comparação.
6. Resultado, anomalias e auditoria são finalizados em um único `SaveChanges` transacional do EF Core.

Foi escolhido processamento síncrono limitado nesta fase, sem fila em memória nem tarefas desacopladas do escopo DI. O prazo de análise, incluindo leitura do storage, autenticação do provider, retries e backoff, é `Ocr:TimeoutSeconds` (15 segundos por padrão). Cada tentativa tem no máximo 5 segundos e respeita o prazo total. A gravação final possui um prazo próprio de 30 segundos. A reserva SQL e o upload têm seus próprios limites de infraestrutura.

Após a reserva, o processamento usa seu próprio cancellation token para poder registrar a conclusão mesmo se o cliente desconectar. Uma falha OCR nunca executa compensação do Blob, apaga a foto ou invalida a leitura. Se o banco ficar indisponível depois do commit da foto, o upload continua válido e um log estruturado identifica a evidência pendente. A conclusão OCR só pode ser persistida quando o banco voltar.

Uma interrupção do processo pode deixar uma tentativa em `PROCESSING`. Não há worker de recuperação automática nesta fase: depois do prazo total mais 30 segundos e do cooldown, um supervisor pode executar nova tentativa, preservando a anterior. A interface permite solicitar reprocessamento; a API decide se a tentativa anterior ainda está dentro do prazo.

## Persistência e migration

`PhotoOcrResult` possui Id, PhotoEvidenceId, Provider, ProviderModel, RawText, DetectedValue, Confidence, Status, ProcessedAtServer, CreatedAt, ErrorCode, ErrorMessage, ProcessingDurationMs e CorrelationId. Também registra ActorUserId, IsInitial, diferenças absoluta/percentual e a conclusão da comparação no momento da análise.

A relação é 1:N com a evidência. O latest é ordenado por `CreatedAt DESC, Id DESC`; uma execução posterior não apaga nem substitui resultados anteriores. Estados expostos: `PENDING`, `PROCESSING`, `SUCCEEDED`, `FAILED`, `NO_VALUE_DETECTED`, `LOW_CONFIDENCE`. Esta implementação inicia diretamente em `PROCESSING`.

`AddPhotoOcrResults` cria `photo_ocr_results`, adiciona `anomalies.ocr_result_id` e atualiza o snapshot. Não modifica migrations anteriores. Há índices para evidência/data de criação, data de processamento e status, índice único filtrado para a execução inicial e índice único de anomalia por resultado/tipo. Todas as novas FKs usam `Restrict`. Confiança possui check entre 0 e 1 ou null.

O domínio impede finalizar duas vezes a mesma tentativa. O DbContext bloqueia exclusão e alterações de resultados já finalizados, e protege os identificadores da tentativa mesmo durante processamento. A migration foi apenas gerada: **não foi aplicada ao Azure**. Revisar o SQL antes de qualquer implantação; a configuração existente de startup pode aplicar migrations se habilitada.

## Providers e Azure

A Application depende de `IPhotoOcrProvider` e `OcrProviderResult`, sem referências ao SDK Azure. `AzureAiVisionOcrProvider` fica na Infrastructure e usa a API REST Image Analysis `2024-02-01`, recurso `read`, por POST com `application/octet-stream`. Somente bytes da imagem são enviados, sem URL pública/SAS, nomes de operador ou identificadores de negócio no corpo.

O transporte limita a resposta a 1 MiB antes de interpretar JSON. Não persiste o payload completo. Desativa redirecionamentos HTTP para não encaminhar credenciais a outro host. Endpoint exige HTTPS, sem credenciais, query string ou fragmento. JPEG, PNG e WEBP permanecem aceitos pelo upload original.

Referências oficiais: [contrato REST](https://learn.microsoft.com/en-us/rest/api/computervision/analyze/image?view=rest-computervision-v4.0+%282024-02-01%29), [chamada de análise](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/how-to/call-analyze-image-40) e [formatos suportados](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/overview-image-analysis).

Para Managed Identity, habilitar a identidade do App Service, atribuir `Cognitive Services User` no recurso Azure AI e configurar seu endpoint com subdomínio personalizado. `DefaultAzureCredential` obtém o token com escopo `https://cognitiveservices.azure.com/.default`. Consulte [autenticação oficial](https://learn.microsoft.com/en-us/azure/ai-services/authentication). Não é necessário adicionar key neste modo.

Se Managed Identity não estiver disponível, configurar `Ocr__Azure__Key` somente em App Service Configuration/Key Vault. A key nunca é retornada, persistida no resultado ou registrada nos logs.

O Image Analysis 4.0 está descontinuado, com retirada anunciada para 25/09/2028. A abstração permite trocar o provider antes desse prazo; acompanhar a [documentação de ciclo de vida](https://learn.microsoft.com/azure/ai-services/computer-vision/quickstarts-sdk/image-analysis-client-library-40?pivots=programming-language-python). Não houve provisionamento nem chamada a Azure real durante os testes.

`FakePhotoOcrProvider` local retorna texto vazio/sem valor, para não fabricar leituras. Os fakes de testes permitem sucesso, timeout, 429, 500, baixa confiança, ausência de valor e resposta inválida. Para OCR real é obrigatório selecionar e configurar AzureAiVision.

## Configuração

| Variável | Padrão | Regra |
| --- | --- | --- |
| `Ocr__Provider` | `Fake` | `Fake` ou `AzureAiVision` |
| `Ocr__MinimumConfidence` | `0.75` | 0 a 1 |
| `Ocr__MaximumDifferenceAbsolute` | `0.5` | maior ou igual a zero |
| `Ocr__MaximumDifferencePercentage` | `5` | maior ou igual a zero |
| `Ocr__TimeoutSeconds` | `15` | 1 a 120 |
| `Ocr__MaximumRetries` | `2` | 0 a 3 retries além da primeira tentativa |
| `Ocr__ReprocessCooldownSeconds` | `60` | 1 a 86400 |
| `Ocr__Azure__Endpoint` | ausente | endpoint HTTPS do recurso |
| `Ocr__Azure__Key` | ausente | secret opcional; ausência seleciona identidade |

As opções são validadas na inicialização. Valores locais estão em `.env.example`, apenas placeholders. O limiar OCR antigo reservado em `HourMeterRules:OcrConfidenceThreshold` não é usado; a autoridade desta fase é `Ocr:MinimumConfidence`.

## Extração e confiança

São aceitos números não negativos de 1 a 9 dígitos, com até duas casas decimais, usando ponto ou vírgula. Não são interpretados separadores de milhar. Labels próximos na mesma linha: HOUR, HOURS, HORIMETRO, HORÍMETRO, H, HR, ODO, ODOMETER, ODOMETRO, ODÔMETRO, KM, KMS.

`HOURS` elimina candidatos explicitamente marcados como quilômetros; `KM` elimina candidatos marcados como horas. Unidade null não recebe unidade presumida: labels de ambas as famílias podem ser considerados, mas valores conflitantes resultam em null.

Datas e horários com delimitadores não são números candidatos. Números alfanuméricos, códigos longos e números imediatamente precedidos por SERIAL, SN, S/N, SERIE, SÉRIE, MODEL ou MODELO são descartados. Sem label, só é aceito um display estritamente numérico isolado. Vários valores distintos igualmente elegíveis produzem `DetectedValue = null` e `NO_VALUE_DETECTED`.

RawText tem no máximo 4000 caracteres. O limite utilizado na extração evita cortar um token e transformar seu prefixo em leitura válida. O banco mantém a proteção de tamanho. Confidence vem somente da palavra numérica identificada, entre 0 e 1; na ausência de correspondência confiável é null. Labels ou palavras irrelevantes não criam uma confiança artificial. Se há ocorrências repetidas do mesmo token, utiliza-se a menor confiança disponível; qualquer ocorrência sem confiança torna o resultado conservadoramente null.

## Comparação e anomalias

`absoluteDifference = abs(operador - detectado)`.

`percentageDifference = absoluteDifference / abs(operador) * 100`. Se operador é zero, o percentual é null e somente o limite absoluto é usado. Sem valor detectado, ambas as diferenças e `matchesOperatorValue` são null.

Há divergência se `absoluteDifference > MaximumDifferenceAbsolute` **OU** `percentageDifference > MaximumDifferencePercentage`. Igualdade com o limite não gera divergência. `matchesOperatorValue` representa correspondência dentro dessas tolerâncias, sem afirmar que o OCR é verdadeiro.

Confiança existente menor que `MinimumConfidence` gera `LOW_OCR_CONFIDENCE` e estado `LOW_CONFIDENCE`. Confiança null não é tratada como baixa nem substituída por uma estimativa. Divergência gera `OCR_OPERATOR_DIVERGENCE`; baixa confiança e divergência podem coexistir. Severidade: WARNING. Anomalias referenciam o resultado, que identifica a foto e a leitura. A unicidade resultado/tipo impede duplicação na mesma execução; reprocessamentos intencionais podem gerar novas anomalias vinculadas ao novo resultado.

## Idempotência, retry, custo e recuperação

A reserva usa lock SQL `UPDLOCK, HOLDLOCK` na evidência, dentro de transação com execution strategy do EF. Assim, instâncias concorrentes não fazem duas análises iniciais. O índice único filtrado também garante no máximo uma tentativa inicial por foto. Reenvio idempotente do upload retorna a evidência existente sem disparar OCR novamente. O reprocessamento é sempre uma nova execução intencional.

O mesmo lock serializa tentativas manuais e aplica cooldown por foto, compartilhado entre usuários/instâncias, incluindo a execução inicial. Tentativas dentro do cooldown ou com processamento ainda no prazo recebem HTTP 409, sem chamar o provider. O rate limit global existente também continua ativo. Não há fila de requisições manuais.

Somente 429, 5xx, erro de rede e timeout de tentativa são candidatos a retry, limitados pelo número configurado e pelo prazo total. Backoff: 250 ms, 500 ms e, se configurado, 1000 ms. 400/401 e respostas inválidas não são repetidos. O término do prazo total finaliza como `FAILED`, com código `OCR_TIMEOUT`. Cada tentativa reabre o stream privado.

GET de resultado/histórico, listagens, dashboard e abertura de tela **nunca** chamam OCR. O botão de atualizar resultado apenas consulta dados persistidos. Retries podem ter custo adicional; não existe garantia de cobrança única no provider após uma falha de rede ambígua. Monitorar volume, custo e limites no recurso Azure.

## API, autorização e auditoria

| Método/rota relativa à leitura | Acesso | Resultado |
| --- | --- | --- |
| `GET /photo/ocr` | OPERATOR, SUPERVISOR, ADMINISTRATIVE, MANAGER, ADMIN | latest; 404 sem foto/resultado |
| `GET /photo/ocr/history` | SUPERVISOR, ADMINISTRATIVE, MANAGER, ADMIN | histórico preservado |
| `POST /photo/ocr/reprocess` | SUPERVISOR, ADMINISTRATIVE, MANAGER, ADMIN | nova análise; 409 durante cooldown |

Prefixo: `/api/v1/readings/{readingId}`. Sem token: 401; perfil insuficiente: 403. A Application também verifica os papéis no processamento manual. Falhas do provider retornam resultado com estado `FAILED`, sem transformar a falha externa em rejeição da evidência.

DTOs expõem Id, PhotoEvidenceId, estado, valor, confiança, provider/modelo, datas, diferenças e códigos/mensagens sanitizados. Não expõem RawText, payload externo, Blob key, SAS ou credenciais.

Auditorias: `PHOTO_OCR_PROCESSED`, `PHOTO_OCR_FAILED` e adicionalmente `PHOTO_OCR_REPROCESSED` para execução manual. Incluem ReadingId, PhotoEvidenceId, OcrResultId, provider, estado, valor, confiança, correlationId e ator manual. Logs estruturados incluem os identificadores, duração, confiança e estado, sem imagem, RawText ou mensagem interna de exceção do provider.

## Frontend

Em `/horimetros/{readingId}`, a seção “Leitura automática da imagem” mostra estado, valor informado, valor detectado na imagem, confiança, diferenças, provider e data. Exibe estados de processamento, baixa confiança, falta de valor, falha e divergência. A frase de divergência é: “Valor detectado na imagem difere da leitura informada.”

Perfis de supervisão recebem botão “Reprocessar OCR”, confirmação explícita, bloqueio durante a chamada e atualização do resultado ao concluir. O BFF usa cookie HttpOnly e verifica Origin na mutação. O cooldown real é imposto pela API. Erro de consulta OCR é apresentado sem esconder a foto; redirecionamentos internos de autenticação Next.js são preservados.

## Segurança, privacidade, riscos e limitações

O original permanece privado e imutável. O único terceiro destinatário novo é o recurso Azure AI configurado, que recebe os bytes originais para extração textual. RawText pode incluir informação incidental da imagem e permanece limitado ao banco protegido; definir retenção e permissões junto à política existente de evidências/auditoria. Não registrar nem compartilhar credenciais de storage ou Azure AI.

Heurísticas conservadoras podem não identificar displays com reflexo, unidades separadas em outra linha, muitos números, separadores de milhar ou caracteres confundidos pelo OCR. Valores sem confiança ainda podem gerar divergência auxiliar. É necessária validação com fotos reais e calibração dos limiares pela operação, antes de produção. Não foram implementados correção automática, treinamento, reconhecimento da máquina, geofence, detecção avançada de fraude, edição de imagem, offline completo ou app mobile.

Consulte [o relatório de validação](fase-5-relatorio-ocr.md) para resultados reais e pendências. O workflow de PR valida em SQL Server descartável e não possui etapa de deploy. Não fazer merge nem aplicar a migration ao Azure sem revisão.
