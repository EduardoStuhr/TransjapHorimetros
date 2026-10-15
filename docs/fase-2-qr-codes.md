# Fase 2 — QR Codes das máquinas

## Objetivo

Desacoplar a identificação física da máquina do cadastro principal, preservar histórico de troca de etiquetas e permitir que o futuro aplicativo resolva o conteúdo lido pela câmera para uma máquina cadastrada.

## Modelo

A entidade `MachineQrCode` armazena:

- `Id`
- `MachineId`
- `Token`
- `IsActive`
- `CreatedAt`
- `UpdatedAt`
- `DeactivatedAt`

O token é único globalmente e uma máquina pode possuir no máximo um QR ativo. A troca não apaga o QR anterior: ele é desativado e continua no histórico.

## Resolução

`GET /api/v1/qr-codes/resolve?value=...` resolve em duas etapas:

1. procura um `MachineQrCode` ativo com o conteúdo exato;
2. por compatibilidade temporária, aceita apenas os formatos legados seguros `68` e `FROTA 68`.

URLs e outros formatos físicos ainda não são presumidos. Quando o conteúdo real das etiquetas existentes for confirmado, o parser deve ser ampliado explicitamente e coberto por testes.

A resposta informa `resolutionSource`:

- `REGISTERED_QR`: QR formal cadastrado;
- `LEGACY_FLEET_NUMBER`: resolução temporária por número de frota.

## Operações

- `GET /api/v1/qr-codes/resolve`: resolver conteúdo lido.
- `GET /api/v1/machines/{machineId}/qr-codes`: histórico.
- `POST /api/v1/machines/{machineId}/qr-codes`: emitir/trocar QR. Se `token` for omitido, o servidor gera um token aleatório criptograficamente seguro.
- `DELETE /api/v1/machines/{machineId}/qr-codes/active`: desativar o QR ativo sem apagar histórico.

## Integridade

- QR anterior nunca é excluído durante troca.
- Tokens não podem ser reutilizados.
- Uma máquina tem no máximo um QR ativo.
- Emissão e desativação geram `AuditLog`.
- Nenhum QR é criado automaticamente para as 52 máquinas.
- A Fase 2 não implementa câmera, app mobile, autenticação, OCR ou armazenamento de fotos.

## Implantação

A migration `20261015103000_AddMachineQrCodes` é aditiva. Em produção, deve ser revisada e aplicada de forma controlada antes de depender dos novos endpoints, pois `ApplyMigrationsOnStartup=false`.
