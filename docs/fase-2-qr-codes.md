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

Uma máquina pode não possuir QR cadastrado. A migration não cria tokens para as 52 máquinas existentes e o seed permanece inalterado.

## Integridade e ciclo de vida

- `ux_machine_qr_codes_token` reserva cada token globalmente, inclusive após sua desativação.
- `ux_machine_qr_codes_machine_active` é um índice único filtrado por `is_active = 1` e impede dois QRs ativos para a mesma máquina.
- `fk_machine_qr_codes_machines_machine_id` usa `RESTRICT`; o histórico nunca é removido por cascade.
- A substituição desativa o QR atual e cria o novo no mesmo `SaveChanges`, portanto na mesma transação do EF Core.
- `DELETE /active` é exclusivamente lógico. Não existe endpoint de exclusão física.
- Violações concorrentes dos índices únicos são convertidas em `409 Conflict`, sem expor erro SQL.

Tokens informados são normalizados apenas com `Trim` externo. Um token vazio é inválido e o limite é 256 caracteres. Quando o corpo omite o token (`null`), o servidor gera `TJ-` seguido por 128 bits aleatórios produzidos por `RandomNumberGenerator`. O token usa collation binária no SQL Server, preservando a resolução exata e sensível a maiúsculas/minúsculas.

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

O parser legado aceita exclusivamente:

- um número de frota positivo isolado, como `68`;
- o prefixo `FROTA` seguido por espaços ou dois-pontos e um número positivo, como `FROTA 68`, `frota 68` ou `FROTA: 68`.

Ele não extrai números de texto livre e não interpreta URLs. O conteúdo real das etiquetas físicas existentes ainda precisa ser confirmado pela Transjap. Até essa confirmação, nenhum formato adicional deve ser presumido.

## Auditoria

As ações `ISSUE_MACHINE_QR_CODE` e `DEACTIVATE_MACHINE_QR_CODE` registram entidade, ID, estado anterior, estado novo, instante do servidor e `correlationId`. Uma troca gera a auditoria da desativação anterior e da nova emissão.

## Migration

`AddMachineQrCodes` cria apenas a tabela `machine_qr_codes`, sua chave primária, a FK restritiva e os dois índices únicos. É uma migration aditiva: não altera nem preenche registros existentes. Ela deve ser revisada e aplicada de forma controlada; não foi aplicada automaticamente no Azure SQL.

## Limitações desta fase

Esta fase implementa somente identidade, histórico, emissão, desativação e resolução no backend. Não inclui imagem PNG/SVG, leitura por câmera, aplicativo mobile, OCR, fotos, Blob Storage, sincronização offline, geofence, autenticação, usuários, RBAC ou correção de leituras.
