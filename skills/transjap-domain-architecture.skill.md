# Skill: Transjap Horímetros — Domain Architecture

## Objetivo
Definir as regras arquiteturais e de negócio específicas do sistema **Transjap Horímetros**.

Esta skill deve ser usada em todas as decisões de backend, frontend, mobile, banco de dados, sincronização, OCR, relatórios e segurança.

## Missão do Sistema
Criar uma plataforma confiável para registrar, sincronizar, validar, analisar e auditar horímetros de máquinas e equipamentos da Transjap.

O sistema deve reduzir:
- registros incorretos;
- falta de leitura;
- leitura atrasada;
- erros de transcrição;
- duplicidades;
- ausência de evidência;
- perda de informação em áreas sem internet.

## Princípios Obrigatórios

```text
Offline First
Evidence First
Audit Everything
API First
Server Validates
Never Trust Client
Never Delete History
Human in the Loop
```

## Atores

### Operador
Responsável por:
- abrir o app;
- autenticar-se;
- ler QR;
- fotografar horímetro;
- confirmar valor;
- informar obra quando necessário;
- enviar/sincronizar registro.

### Supervisor
Responsável por:
- revisar inconsistências;
- validar correções;
- acompanhar pendências.

### Administrativo
Responsável por:
- consultas;
- filtros;
- relatórios;
- conferência;
- acompanhamento de registros.

### Gestor/Diretoria
Responsável por:
- indicadores;
- relatórios;
- informações consolidadas;
- acompanhamento de confiabilidade.

### Administrador
Responsável por:
- usuários;
- permissões;
- máquinas;
- QR Codes;
- configurações.

## Máquina

Toda máquina deve possuir:

```text
Id
FleetNumber
Model
Manufacturer
Status
CreatedAt
UpdatedAt
```

`FleetNumber` representa o número da frota.

Exemplos do contexto inicial:

```text
68  - Pipa Ford
70  - Moto Niveladora 120H
74  - Escavadeira Hidráulica 312 DL
84  - Retro-Escavadeira 416-E 4x4
244 - Escavadeira Hidráulica 345 GC
290 - Moto Niveladora 140
292 - Fiat Fiorino Endurance
294 - CHEV ONIX
296 - Moto Bomba
```

O sistema deve ser a fonte oficial do cadastro de frota.

## QR Code

O QR identifica a máquina.

Não permitir que a leitura do QR seja substituída silenciosamente por outra máquina.

Estrutura preferencial futura:

```text
MachineQrCode
- Id
- MachineId
- Token
- Active
- CreatedAt
- RevokedAt
```

O QR pode ser revogado sem excluir a máquina.

Manter compatibilidade inicial com QR Codes existentes.

## Registro de Horímetro

Entidade central:

```text
HourMeterReading

Id
MachineId
OperatorId
WorkSiteId
Value
ReadingType
Status

CapturedAtDevice
ReceivedAtServer
SyncedAt

ClientEventId

CreatedAt
```

## Tipo de Leitura

Tipos iniciais:

```text
OPENING
CLOSING
INTERMEDIATE
MAINTENANCE
CORRECTION
```

O tipo não deve ser ignorado, pois uma leitura feita no início do dia pode representar o fechamento operacional do período anterior.

## Status da Leitura

```text
VALIDATED
PENDING
SUSPECT
REJECTED
CORRECTED
```

### VALIDATED
Leitura passou pelas regras.

### PENDING
Ainda aguarda algum processamento ou análise.

### SUSPECT
Uma ou mais regras indicaram possível inconsistência.

### REJECTED
Registro não deve ser tratado como válido operacionalmente.

### CORRECTED
Registro possui correção posterior rastreável.

## Evidência

Foto do horímetro é evidência primária.

Nunca salvar apenas o número.

Cada leitura deve preferencialmente possuir:

```text
PhotoEvidence

Id
ReadingId
StoragePath
Hash
MimeType
Size
CreatedAt
```

A foto não deve ser substituída silenciosamente.

## OCR

Resultado do OCR:

```text
OcrResult

Id
ReadingId
RawText
DetectedValue
Confidence
Engine
EngineVersion
ProcessedAt
```

O OCR é auxiliar.

Nunca substituir automaticamente a evidência ou a confirmação humana sem regra explícita.

## Valor Informado x Valor OCR

Armazenar separadamente quando aplicável:

```text
operatorConfirmedValue
ocrDetectedValue
ocrConfidence
```

Se houver divergência relevante:
- marcar anomalia;
- manter ambos os valores;
- solicitar revisão quando necessário.

## Offline First

O app deve funcionar sem internet.

Fluxo:

```text
QR
↓
Foto
↓
Valor
↓
Obra
↓
Salvar localmente
↓
Criar ClientEventId
↓
PENDING_SYNC
↓
Internet disponível
↓
Sincronizar
↓
Servidor confirma
```

Nunca bloquear operação de campo apenas porque a rede está indisponível.

## Idempotência

Cada evento local recebe UUID único.

A API deve impedir duplicação por reenvio.

Exemplo:

```text
ClientEventId = "e83d..."
```

Constraint única obrigatória.

## Datas

Guardar no mínimo:

```text
CapturedAtDevice
ReceivedAtServer
SyncedAt
```

Motivo:
- celular pode ficar offline;
- relógio do celular pode estar incorreto;
- sincronização pode acontecer horas depois.

Não substituir uma data pela outra.

## Validação da Leitura

Ao receber registro, verificar:

```text
1. usuário
2. permissão
3. máquina
4. QR
5. obra
6. clientEventId
7. valor
8. leitura anterior
9. intervalo de tempo
10. evidência
11. OCR
12. anomalias
```

## Regras de Anomalia

### Leitura menor que anterior

```text
previous = 4328.7
current  = 4120.0
```

Criar:

```text
READING_DECREASE
severity = HIGH
```

Possíveis exceções devem ser tratadas explicitamente, como troca de horímetro.

### Aumento impossível

Comparar delta do horímetro com tempo transcorrido.

Exemplo:

```text
24h entre leituras
+42h no horímetro
```

Criar:

```text
IMPOSSIBLE_HOUR_INCREASE
```

### Duplicidade

Mesmo valor em intervalo muito curto ou mesmo `ClientEventId`.

Criar:

```text
DUPLICATE_READING
```

ou simplesmente rejeitar duplicação idempotente.

### Falta de leitura

Se máquina ativa deveria registrar e não registrou dentro da janela esperada:

```text
MISSING_READING
```

A janela deve ser configurável.

### OCR com baixa confiança

```text
LOW_OCR_CONFIDENCE
```

### Divergência OCR x operador

```text
OCR_OPERATOR_DIVERGENCE
```

### Atraso de sincronização

Quando necessário:

```text
LATE_SYNC
```

Não tratar automaticamente atraso como fraude. Pode ser falta de internet.

## Confiabilidade

O sistema pode futuramente calcular um score, mas o score nunca substitui os dados de auditoria.

Possíveis sinais:

```text
QR válido
foto presente
OCR coerente
sequência coerente
usuário autenticado
tempo plausível
sync normal
```

## Correções

Nunca apagar uma leitura original para "corrigir".

Registrar:

```text
OriginalReading
Correction
Reason
User
Timestamp
```

Toda correção deve ser auditável.

## Audit Log

Toda operação crítica gera evento.

Exemplos:

```text
USER_LOGIN
MACHINE_CREATED
QR_REVOKED
READING_CREATED
READING_REVIEWED
READING_CORRECTED
READING_REJECTED
REPORT_EXPORTED
USER_PERMISSION_CHANGED
```

## Relatórios

Relatórios devem utilizar dados validados e deixar claro quando incluem registros suspeitos.

Excel pode conter:

```text
Frota
Modelo
Obra
Data
Horímetro anterior
Horímetro atual
Diferença
Operador
Status
Anomalia
```

PDF pode conter:
- período;
- totais;
- máquinas sem leitura;
- anomalias;
- horas por máquina;
- resumo executivo.

## Fechamento Diário

Prever processo diário de conferência.

Exemplo:

```text
Máquinas ativas: 52
Atualizadas: 47
Sem leitura: 3
Suspeitas: 1
Pendentes de sync: 1
```

Gerar lista de pendências.

## Dashboard

O dashboard não deve mostrar apenas totais.

Deve priorizar ação operacional:

```text
Sem leitura
Leitura suspeita
Sync pendente
OCR baixo
Correção aguardando revisão
```

## Segurança

Premissas:
- API é autoridade;
- cliente não é confiável;
- toda entrada é validada;
- operação sensível exige permissão;
- secrets nunca ficam em código;
- histórico não é apagado;
- logs não guardam secrets;
- transporte é HTTPS.

## Fonte Oficial

A informação oficial é o registro persistido no servidor, junto com:
- evidência;
- metadados;
- histórico;
- status;
- auditoria.

O app local é uma origem temporária até a sincronização.

## Regras para o Frontend

Frontend:
- exibe status claramente;
- nunca mascara anomalia;
- nunca altera informação crítica sem justificativa;
- sempre mostra origem e horário quando necessário;
- deve permitir abrir foto associada;
- deve diferenciar valor informado e OCR quando houver divergência.

## Regras para o Mobile

Mobile:
- leitura por QR como fluxo principal;
- câmera integrada;
- banco local;
- fila de sincronização;
- indicação visual clara de pendente/sincronizado;
- impedir perda de registro ao fechar o app;
- impedir duplicação;
- não exigir rede para registrar;
- não permitir edição silenciosa após sincronização.

## Regras para Banco

Banco deve suportar:
- histórico;
- foreign keys;
- unique constraints;
- timestamps;
- auditoria;
- índices por frota/data/status;
- UUIDs;
- soft state quando aplicável.

Não usar cascade delete em dados históricos críticos sem justificativa arquitetural.

## Regra de Ouro

Quando houver conflito entre:
- facilidade de implementação;
- integridade da informação;

priorizar integridade da informação.

Quando houver conflito entre:
- automação;
- evidência humana;

manter a evidência e permitir revisão.

Quando houver conflito entre:
- correção rápida;
- rastreabilidade;

manter rastreabilidade.
