# Fase 2 — QR Codes das Máquinas

## Objetivo e modelo

A identificação física foi separada do cadastro permanente da máquina. `Machine` continua representando o equipamento; `MachineQrCode` representa uma etiqueta substituível e auditável. Essa separação permite trocar, revogar ou reimprimir uma etiqueta sem apagar sua história e sem alterar a identidade da máquina.

`MachineQrCode` possui:

- `Id` (`Guid`);
- `MachineId` (`Guid`);
- `Token` (`string`, máximo de 256 caracteres);
- `IsActive` (`bool`);
- `CreatedAt` e `UpdatedAt` (`DateTimeOffset`, normalizados para UTC);
- `DeactivatedAt` (`DateTimeOffset?`).

Uma máquina pode não possuir QR cadastrado. A migration estrutural `AddMachineQrCodes` não cria tokens e permanece inalterada. Após a confirmação do PDF oficial, a migration de dados separada `SeedExistingMachineQrCodes` associa os 51 adesivos físicos conhecidos; o seed de máquinas permanece inalterado.

## Integridade e ciclo de vida

- `ux_machine_qr_codes_token` reserva cada token globalmente, inclusive após sua desativação.
- `ux_machine_qr_codes_machine_active` é um índice único filtrado por `is_active = 1` e impede dois QRs ativos para a mesma máquina.
- `fk_machine_qr_codes_machines_machine_id` usa `RESTRICT`; o histórico nunca é removido por cascade.
- A substituição desativa o QR atual e cria o novo no mesmo `SaveChanges`, portanto na mesma transação do EF Core.
- `DELETE /active` é exclusivamente lógico. Não existe endpoint de exclusão física.
- Violações concorrentes dos índices únicos são convertidas em `409 Conflict`, sem expor erro SQL.

Tokens informados são normalizados apenas com `Trim` externo. Um token vazio é inválido e o limite é 256 caracteres. Os adesivos oficiais usam exatamente quatro dígitos, como `0068`, `0112` e `0294`. Quando uma emissão futura omitir o token (`null`), o servidor gera `TJ-` seguido por 128 bits aleatórios produzidos por `RandomNumberGenerator`. Tokens `TJ-*` são reservados a futuras emissões ou substituições; não substituem os adesivos atuais. O token usa collation binária no SQL Server, preservando a resolução exata e sensível a maiúsculas/minúsculas.

## Endpoints

### `GET /api/v1/qr-codes/resolve?value={conteudo}`

Normaliza espaços externos, procura primeiro um QR ativo pelo token exato e, somente se não encontrar, tenta o parser legado. Retorna `QrResolutionResponse` com máquina, status, unidade, origem e o ID do QR formal quando aplicável.

As origens possíveis são:

- `REGISTERED_QR`;
- `LEGACY_FLEET_NUMBER`.

Uma máquina `INACTIVE` continua sendo retornada. O endpoint não cria máquinas nem cadastra tokens implicitamente.

### `GET /api/v1/machines/{machineId}/qr-codes`

Retorna o histórico completo, incluindo ativos e desativados, em ordem de criação decrescente.

### `POST /api/v1/machines/{machineId}/qr-codes`

Emite um token informado ou gerado pelo servidor. Se já existir um QR ativo, ele é desativado antes da criação do novo. A emissão e eventual desativação são auditadas e persistidas atomicamente.

### `DELETE /api/v1/machines/{machineId}/qr-codes/active`

Desativa o QR ativo e retorna `204 No Content`. Se não houver QR ativo, também retorna `204`, de forma idempotente.

Os endpoints administrativos `POST` e `DELETE` não devem ser usados na operação real antes da autenticação e autorização previstas para a Fase 3.

## Compatibilidade legada

O PDF oficial da Transjap confirmou que os QR codes já estão impressos e colados nas máquinas. Seu conteúdo é o número da frota com exatamente quatro dígitos e zeros à esquerda, por exemplo `0068` para a frota 68.

O parser conservador aceita exclusivamente:

- um número de frota positivo isolado, como `68`;
- um código físico com exatamente quatro algarismos ASCII e resultado positivo, como `0068`, `0112` ou `0294`;
- o prefixo `FROTA` seguido por espaços ou dois-pontos e um número positivo, como `FROTA 68`, `frota 68` ou `FROTA: 68`.

`0000`, caracteres alfanuméricos, códigos com cinco dígitos iniciados por zero, texto livre e URLs são rejeitados. O parser nunca extrai uma sequência numérica de outro conteúdo.

## Associação inicial dos adesivos oficiais

Das 52 máquinas atuais, 51 aparecem no PDF e recebem seu token real de quatro dígitos. A frota 296 não aparece no PDF e permanece sem `MachineQrCode`; nenhum token é inventado para ela.

O PDF também contém frotas antigas que não pertencem ao cadastro atual: 16, 76, 80, 86, 92, 94, 108, 114, 166, 184, 186, 188, 190, 196, 202, 206, 220, 222, 224, 226, 234, 252, 272, 276, 286 e 288. A migration não cria essas máquinas nem importa seus tokens.

O data patch resolve cada destino por `machines.fleet_number`, gera somente o `Id` técnico com `NEWID()` e valida todo o conjunto antes de inserir. Uma execução repetida aceita apenas a associação exata já ativa e não duplica registros. Máquina ausente, token associado a outra máquina, token desativado ou QR ativo diferente provocam falha explícita antes de qualquer inserção.

## Auditoria

As ações `ISSUE_MACHINE_QR_CODE` e `DEACTIVATE_MACHINE_QR_CODE` registram entidade, ID, estado anterior, estado novo, instante do servidor e `correlationId`. Uma troca gera a auditoria da desativação anterior e da nova emissão. A associação inicial é uma migration de dados baseada no PDF oficial e não inventa um ator humano em `AuditLog`.

## Migration

`AddMachineQrCodes` cria apenas a tabela `machine_qr_codes`, sua chave primária, a FK restritiva e os dois índices únicos; essa migration anterior não foi reescrita. `SeedExistingMachineQrCodes` é uma migration de dados separada, explícita e idempotente que associa os 51 tokens confirmados. Nenhuma das duas migrations foi aplicada automaticamente no Azure SQL. A nova migration deve ser revisada e aplicada de forma controlada.

## Limitações desta fase

Esta fase implementa somente identidade, histórico, emissão, desativação e resolução no backend. Não inclui imagem PNG/SVG, leitura por câmera, aplicativo mobile, OCR, fotos, Blob Storage, sincronização offline, geofence, autenticação, usuários, RBAC ou correção de leituras.
