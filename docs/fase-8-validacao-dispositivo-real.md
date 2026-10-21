# Fase 8 — patch de validação em dispositivo real

Este patch não inicia a Fase 9. Ele corrige somente os achados da validação real da coleta móvel.

## Câmera Android

O leitor exige um contexto seguro. Em produção, use HTTPS. Para testar o servidor local no Chrome do Android por USB:

```text
adb reverse tcp:3000 tcp:3000
```

Abra `http://localhost:3000` no dispositivo. `localhost` é tratado como contexto seguro; acessar o IP HTTP da máquina não é equivalente.

O scanner abre uma única stream com preferência pela câmera traseira, valida que existe uma video track `live`, associa `video.srcObject`, aguarda os metadados e executa `video.play()`. O `BarcodeDetector` é tentado primeiro. Após duas falhas técnicas ou 2,5 segundos sem leitura, o mesmo stream passa ao worker do `qr-scanner`, sem nova solicitação de câmera. Ao fechar, trocar de tela ou detectar o QR, tracks e worker são encerrados.

Em desenvolvimento, falhas técnicas são registradas no console apenas com etapa, nome e mensagem do erro. A interface distingue permissão negada, câmera ausente, câmera ocupada, interrupção, restrição incompatível e contexto inseguro.

## Configuração de horímetro/odômetro

O campo `machines.meter_unit` já existe e continua anulável para representar unidade ainda não confirmada. Nenhum backfill é executado.

Procedimento seguro:

1. Um usuário `ADMIN` ou `MANAGER` abre **Frota** e seleciona a máquina.
2. Em **Unidade oficial da máquina**, confirma no equipamento ou cadastro operacional se a unidade é `HOURS` ou `KM`.
3. Salva a unidade. O servidor altera somente `Machine.MeterUnit`, registra auditoria e não modifica leituras históricas.
4. O cache offline do dispositivo atual é atualizado. Nos demais dispositivos, use **Atualizar dados de campo** antes da coleta.

`OPERATOR` não visualiza o formulário e recebe `403` se tentar chamar diretamente o endpoint. Leituras continuam sendo recusadas pelo servidor enquanto a unidade oficial estiver ausente.

Para conferir o total atual, a tela **Frota** mostra quantas máquinas retornadas pela API oficial ainda estão sem unidade. Uma conferência direta no banco, quando necessária, deve ser somente leitura:

```sql
SELECT COUNT(*) AS total_machines,
       SUM(CASE WHEN meter_unit IS NULL THEN 1 ELSE 0 END) AS without_meter_unit
FROM machines;
```
