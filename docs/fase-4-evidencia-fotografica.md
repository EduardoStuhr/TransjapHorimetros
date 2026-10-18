# Fase 4 — Evidência fotográfica

## Visão geral

A Fase 4 implementa a evidência fotográfica imutável das leituras. O princípio adotado é: **a foto é a prova, o texto é a leitura, o servidor é a verdade**. Não há OCR, interpretação do conteúdo, compressão nem alteração da imagem original.

A relação operacional é 1:1: uma `HourMeterReading` pode possuir uma `PhotoEvidence` principal. O SQL guarda somente metadados e a referência interna; os bytes permanecem no Azure Blob Storage privado.

## Modelo e migration

A migration `AddPhotoEvidence` cria `photo_evidence` com:

- `id`, `reading_id` e `client_event_id` opcional;
- provider, container e object key internos do storage;
- nome original sanitizado, MIME canônico, tamanho e SHA-256;
- instante opcional de captura no dispositivo e instante obrigatório de upload no servidor;
- largura, altura, status, dispositivo e autoria autenticada;
- `created_at` definido pelo servidor.

Índices e constraints:

- PK em `id`;
- unicidade em `reading_id`, garantindo uma evidência principal por leitura;
- unicidade filtrada em `client_event_id` quando informado;
- unicidade em `storage_object_key`;
- índices em `sha256` e `created_by_user_id`;
- FKs `Restrict` para `hour_meter_readings` e `users`;
- check constraint exigindo tamanho positivo.

A migration foi gerada, mas **não foi aplicada no Azure**.

## Upload e validação

Endpoint:

```text
POST /api/v1/readings/{readingId}/photo
Content-Type: multipart/form-data
```

Campos:

- `file` — obrigatório;
- `capturedAtDevice` — opcional;
- `deviceId` — opcional, até 100 caracteres;
- `clientEventId` — UUID opcional para reenvio mobile futuro.

Formatos aceitos: JPEG, PNG e WEBP. O limite padrão e máximo de segurança desta fase é 10 MB, configurado por `Storage__Photos__MaximumFileSizeBytes`.

O servidor não confia em filename, extensão ou `Content-Type`. O arquivo é transmitido para um arquivo temporário aleatório, com limite contado durante o streaming e SHA-256 incremental. Depois, são validados:

- JPEG: assinatura `FF D8 FF`, segmento de dimensões e marcador final;
- PNG: assinatura completa, `IHDR`, dimensões e `IEND`;
- WEBP: `RIFF`, tamanho declarado, `WEBP` e cabeçalho `VP8`, `VP8L` ou `VP8X`.

O MIME enviado precisa coincidir com o formato detectado. Arquivo vazio, truncado, MIME falso, PDF, ZIP, HTML, SVG e conteúdo desconhecido são rejeitados. O original não é reprocessado nem executado por ferramenta externa.

## Imutabilidade e idempotência

`PhotoEvidence` não pode sofrer update ou delete pelo `DbContext`, e não existe endpoint `DELETE`. O `ReadingId` não pode ser alterado e a API não substitui uma foto existente.

Comportamento idempotente:

- mesmo `ReadingId` + mesmo SHA-256 retorna a evidência existente (`200`);
- mesmo `ClientEventId` para a mesma leitura retorna a evidência existente (`200`);
- foto diferente para leitura já atendida retorna `409`;
- `ClientEventId` usado em outra leitura retorna `409`.

## Storage e consistência

`IPhotoStorage` mantém a Application desacoplada do SDK Azure. `AzureBlobPhotoStorage` grava objetos com chave gerada exclusivamente pelo servidor:

```text
readings/{readingId}/{photoEvidenceId}.{extensao-canonica}
```

O nome original é apenas metadado sanitizado e nunca participa do path. O container é criado com `PublicAccessType.None`; não há URL pública, SAS persistido ou acesso anônimo.

Ordem transacional compensatória:

1. validar leitura e arquivo;
2. gerar identificadores e object key;
3. enviar o blob sem permitir overwrite;
4. persistir `PhotoEvidence` e `PHOTO_EVIDENCE_CREATED` na mesma transação SQL;
5. se o SQL falhar, tentar excluir o blob recém-criado;
6. se o cleanup falhar, registrar erro estruturado sem conteúdo binário.

Esse desenho evita registro SQL sem blob. Uma falha rara entre commit SQL e confirmação ao cliente ainda exige reconciliação operacional futura; o SHA-256 e os logs tornam o caso rastreável.

Nos testes, `InMemoryPhotoStorage` substitui o Azure. Testes automatizados nunca acessam storage ou banco de produção.

## Consulta e visualização

```text
GET /api/v1/readings/{readingId}/photo
GET /api/v1/readings/{readingId}/photo/content
```

O primeiro endpoint entrega metadados sem container/object key. O segundo abre o blob privado e transmite o stream original. Ambos exigem `CanRecordReading`, que inclui `OPERATOR`, `SUPERVISOR`, `ADMINISTRATIVE`, `MANAGER` e `ADMIN`.

O frontend possui `/horimetros/{readingId}`, thumbnail, metadados e modal. A imagem passa pelos Route Handlers do BFF com o access token HttpOnly; a URL privada do Blob nunca chega ao HTML. O upload manual aparece somente para supervisão e perfis administrativos, embora o backend permaneça preparado para o futuro aplicativo do operador.

## Azure Blob Storage

Configuração sem secrets versionados:

```text
Storage__Photos__Provider=AzureBlob
Storage__Photos__ContainerName=photo-evidence
Storage__Photos__MaximumFileSizeBytes=10485760
Storage__Photos__AccountUri=https://<storage-account>.blob.core.windows.net
```

Produção recomendada:

1. criar o Storage Account na mesma região da API quando possível;
2. desabilitar acesso público anônimo no account;
3. criar o container `photo-evidence` com access level **Private**;
4. habilitar Managed Identity no App Service da API;
5. conceder à identidade o papel mínimo necessário, normalmente `Storage Blob Data Contributor`, no escopo do container/account;
6. configurar somente `Storage__Photos__AccountUri` e reiniciar a API.

Fallback por secret:

```text
Storage__Photos__ConnectionString=<secret-no-app-service-ou-key-vault>
```

A connection string nunca deve ser colocada em `appsettings.json`, `.env.example`, logs ou commits. Nenhum recurso Azure foi criado automaticamente por esta implementação.

## Segurança e riscos restantes

- O endpoint tem limite multipart de 11 MB para acomodar overhead, e o serviço impõe 10 MB sobre os bytes reais.
- O conteúdo é devolvido com autorização e `nosniff` no BFF.
- `CreatedByUserId` e `CreatedByEmail` vêm exclusivamente de `ICurrentUser`.
- `UploadedAtServer` e `CreatedAt` vêm exclusivamente do relógio do servidor.
- O hash protege integridade e auxilia deduplicação, mas não substitui antimalware. Varredura assíncrona poderá ser adicionada em fase futura.
- Revogação está prevista no status, mas não possui endpoint nesta fase.
- `PHOTO_EVIDENCE_VIEWED` e `PHOTO_EVIDENCE_REVOKED` estão reservados; somente criação é auditada agora.
- Não há offline sync completo, geofence, OCR, interpretação numérica ou detecção de fraude por imagem.
