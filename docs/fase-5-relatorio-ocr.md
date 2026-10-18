# FASE 5 — RELATÓRIO OCR

Data: 18/10/2026. Implementação em `feat/phase-5-ocr`, sem implantação ou merge. Detalhes técnicos em [fase-5-ocr.md](fase-5-ocr.md).

| Item | Entrega / resultado |
| --- | --- |
| 1. Arquitetura | Foto commitada primeiro; OCR síncrono limitado, com reserva persistente e finalização transacional. |
| 2. Entidade/modelo | PhotoOcrResult, relação 1:N, resultado mais recente, estados, diferenças e histórico imutável após conclusão. |
| 3. Migration | `20261018135847_AddPhotoOcrResults`, gerada; não aplicada no Azure. Migrations anteriores preservadas. |
| 4. Provider | AzureAiVisionOcrProvider por REST Image Analysis 2024-02-01 e fake local/testes. |
| 5. Abstração | IPhotoOcrProvider e IMeterReadingExtractor na Application, sem SDK Azure. |
| 6. Configuração | Ocr: Provider, MinimumConfidence, MaximumDifferenceAbsolute/Percentage, TimeoutSeconds, MaximumRetries, ReprocessCooldownSeconds e Azure. |
| 7. Azure | Bytes privados, HTTPS, DefaultAzureCredential/Managed Identity; key opcional por secret. Nenhuma chamada real nesta validação. |
| 8. Extração | Ponto/vírgula, labels de horímetro e odômetro; valor nullable. |
| 9. Heurísticas | Rejeição conservadora de datas, horários, séries e ambiguidade; respeita HOURS/KM/null. RawText limitado a 4000 caracteres. |
| 10. Confidence | Confiança normalizada da palavra numérica; null quando indisponível. |
| 11. Low confidence | LOW_CONFIDENCE e anomalia LOW_OCR_CONFIDENCE abaixo do limiar. |
| 12. Comparação | Diferenças absoluta/percentual; limites em OU; percentual null com operador zero. Reading.Value preservado. |
| 13. Anomalias | LOW_OCR_CONFIDENCE e OCR_OPERATOR_DIVERGENCE, WARNING, vínculo ao resultado. |
| 14. Idempotência | Lock SQL na foto, índice único da tentativa inicial, índice único de anomalia por resultado/tipo. |
| 15. Reprocessamento | POST explícito, cooldown entre instâncias, nova tentativa preservando anteriores. |
| 16. Histórico | GET protegido; latest sem novo processamento. |
| 17. Autorização | Latest para operação autenticada; histórico/reprocessamento para SUPERVISOR, ADMINISTRATIVE, MANAGER e ADMIN. |
| 18. Auditoria | PHOTO_OCR_PROCESSED, PHOTO_OCR_FAILED, PHOTO_OCR_REPROCESSED; identificadores, comparação, confiança, correlação e ator manual. |
| 19. Frontend | Seção de OCR, estados, diferenças, aviso de divergência, confirmação de reprocessamento e proteção de clique. |
| 20. Testes unitários | 163 aprovados, zero falhas: extração, comparação, provider HTTP simulado, serviço, retry, timeout, falhas, imutabilidade, modelo EF e isolamento do upload quando a persistência OCR falha. |
| 21. Integração | Primeira execução local: 43 aprovados, 73 falharam na inicialização por TEST_SQLSERVER_CONNECTION_STRING ausente. SQL Server/Docker e .env não disponíveis. A primeira CI identificou configuração tardia na factory do host mínimo; corrigida via ConfigureHostConfiguration. Nova rodada remota pendente, incluindo rollback de commit e perda da confirmação de commit da reserva. |
| 22. Build backend | `dotnet restore` e `dotnet build ... --configuration Release` concluídos, zero erros/avisos na última compilação completa. |
| 23. Lint frontend | `npm ci` e `npm run lint` concluídos com sucesso. Dependências transitivas emitiram avisos de depreciação. |
| 24. Build frontend | `npm run build` concluído; TypeScript e rotas geradas. Aviso de lockfile no diretório pai, fora do Git, sem impedir build. |
| 25. Secrets | Ocr__Azure__Key apenas se não usar identidade; nenhum secret real adicionado. |
| 26. Configuração Azure | Endpoint do recurso, Managed Identity + Cognitive Services User, provider AzureAiVision, revisão dos limiares e migration. Não executada nesta fase. |
| 27. Branch | feat/phase-5-ocr. |
| 28. Commit | Principal: `1ce724f` — `feat: implement OCR analysis for photo evidence`; correções de validação posteriores no histórico da branch. |
| 29. PR | [#9](https://github.com/EduardoStuhr/TransjapHorimetros/pull/9), para main, em rascunho e sem merge; validação remota pendente. |
| 30. Riscos restantes | Executar integração SQL, validar fotos reais e identidade Azure, calibrar limiares, revisar migration; queda do processo exige reprocessamento manual. Image Analysis 4.0 tem retirada anunciada em 25/09/2028. |

O comando completo `dotnet test apps/api/TransjapHorimetros.sln --configuration Release` foi executado e seu resultado local não foi considerado aprovado. Nenhum banco alternativo ou Azure foi usado para contornar a proteção dos testes.

OCR IMPLEMENTADO? **SIM**

OCR ALTERA LEITURA? **NÃO**

LOW OCR CONFIDENCE? **SIM**

OCR OPERATOR DIVERGENCE? **SIM**

HISTÓRICO OCR? **SIM**

REPROCESSAMENTO? **SIM**

MIGRATION APLICADA NO AZURE? **NÃO**

PR MERGEADO? **NÃO**

FASE 5 EM PRODUÇÃO? **NÃO**

FASE 5 PRONTA PARA REVISÃO? **NÃO — validação SQL completa pendente.**
