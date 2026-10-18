# Fase 4 — revisão final

Data: 2026-10-18. Branch: `feat/phase-4-photo-evidence`. Base revisada: `origin/main` (`33ff999`).

## Resultado da validação

| Verificação | Resultado |
| --- | --- |
| Base atualizada | A branch contém a main atual; rebase desnecessário |
| `dotnet restore apps/api/TransjapHorimetros.sln` | Passou |
| `dotnet build apps/api/TransjapHorimetros.sln --configuration Release` | Passou, zero erros e zero avisos |
| `dotnet test apps/api/TransjapHorimetros.sln --configuration Release` | 96 unitários passaram; integração: 43 passaram e 58 falharam na inicialização da fixture por falta de SQL local/configuração |
| `npm ci` | Passou |
| `npm run lint` | Passou |
| `npm run build` | Passou, incluindo `/horimetros/[readingId]` e as rotas BFF de foto |
| Migration `20261016174544_AddPhotoEvidence` | Revisada; não aplicada |
| Secrets reais nas alterações | Nenhum encontrado; `.env.example` contém placeholders |

O build Next.js informou que ignorou um `package-lock.json` no diretório pai, fora do repositório; a compilação concluiu normalmente. Nenhuma alteração cosmética de configuração foi feita.

## Correções desta revisão

- `PhotoEvidenceService.cs`: após colisão de unicidade, a comparação de dois `ClientEventId` nulos podia retornar sucesso para fotos com hashes diferentes. A equivalência por evento agora exige um evento informado. Testes cobrem a corrida sem evento com hash igual e diferente e a remoção compensatória do novo blob.
- `PhotoFileValidator.cs`: um segmento JPEG de dimensões menor que o necessário podia causar leitura além do fim e erro interno. O validador agora rejeita esse cabeçalho como arquivo inválido. Teste de regressão incluído.

## Revisão funcional e de segurança

- Entidade, configuração EF, snapshot, migration, repositório, DbContext e DI revisados. A tabela `photo_evidence` tem PK `id`, FKs `Restrict` para leituras e usuários, índices em SHA-256 e autor, e unicidade em leitura, object key e evento opcional. `Down` remove somente a tabela criada.
- Upload exige autenticação e a policy `CanRecordReading`; verifica leitura existente, arquivo vazio, limite de bytes, assinatura, cabeçalhos, dimensões e MIME de JPEG/PNG/WEBP. Tipos não reconhecidos, incluindo PDF, ZIP, SVG, HTML e executáveis, são rejeitados. A inspeção estrutural não equivale à decodificação completa nem a antimalware.
- SHA-256 é calculado sobre os bytes originais. A chave do objeto é gerada no servidor; filename é somente metadado sanitizado. O banco recebe metadados, sem o binário.
- Container novo usa `PublicAccessType.None`; upload usa `IfNoneMatch = ETag.All`. Nenhuma URL pública permanente ou SAS é retornado/persistido. A configuração efetiva de um container Azure já existente não foi consultada; o provisionamento deverá garantir acesso privado e bloquear acesso público no account.
- `PhotoEvidence` e `ReadingId` são imutáveis no fluxo normal; o DbContext bloqueia update/delete. Não há endpoint DELETE ou substituição. Existe exclusão interna compensatória apenas para o blob recém-enviado quando a persistência falha; não é um recurso público de exclusão de evidências.
- `PHOTO_EVIDENCE_CREATED` é adicionado junto aos metadados na mesma chamada de persistência: usuário autenticado, leitura, evidência, hash, timestamp do servidor e correlation ID; sem binário no log.
- A página de detalhes contém estado sem evidência, thumbnail, data, tipo, tamanho e modal de visualização. As imagens passam pelo BFF autenticado, com `private, no-store` e `nosniff`; o componente usa imagem sem otimização intermediária. Revisão do código e build concluídos; não houve sessão autenticada de navegador para um teste ponta a ponta.

## Limitações e pendências

Os 58 testes que dependem de `TransjapApiFactory` não executaram seus cenários: o construtor abortou por ausência de `TEST_SQLSERVER_CONNECTION_STRING`. Causa em `apps/api/tests/TransjapHorimetros.IntegrationTests/TransjapApiFactory.cs`; não foi identificado erro funcional nesses testes. Para completar a validação, disponibilizar SQL Server local com banco exclusivo `transjap_horimetros_tests`, configurar a variável e repetir a suíte. Nunca apontar para Azure. Os 43 testes de imutabilidade, definição do patch e proteção do banco dispensam conexão e passaram.

Provisionamento Azure pendente: Storage Account, container privado `photo-evidence`, Managed Identity, papel `Storage Blob Data Contributor`, `Storage__Photos__AccountUri` e aplicação controlada da migration após revisão.

OCR, offline sync completo, mobile, geofence e análise antifraude por imagem não foram implementados nesta fase. Nenhum recurso Azure foi criado, nenhuma migration foi aplicada no Azure, nenhum deploy foi executado e nenhum merge foi realizado nesta revisão.

Pronta para revisão manual do PR, com integração SQL e validação ponta a ponta pendentes antes de produção.
