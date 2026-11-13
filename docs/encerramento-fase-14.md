# Encerramento da Fase 14

**APROVADA PARA HOMOLOGAÇÃO DE PRODUÇÃO** em 13/11/2026.

Os PRs #27 e #28 foram integrados à main. O merge do PR #28 é 226d05628fcffe2746276392b650123b93de51a1. O escopo compreende apuração de utilização, dashboard, filtros, paginação e exportações Excel e PDF. A Fase 14 não introduziu migrations.

O responsável confirmou em 13/11/2026 a convenção operacional de atribuir à mesma obra as horas entre leituras consecutivas elegíveis da mesma máquina que indiquem a mesma obra. Ambas devem usar HOURS e respeitar as regras de validação, sem diferença negativa, duplicidade, correção ou inconsistência impeditiva. Obras diferentes ou ausentes deixam horas sem atribuição. A convenção não comprova localização contínua.

A [CI 43458683637](https://github.com/EduardoStuhr/TransjapHorimetros/actions/runs/43458683637) aprovou 265 testes unitários, 226 de integração e 112 de frontend, além de lint e builds Release e Next.js. A inspeção em Chrome, exportações, segurança e carga foram validadas em ambiente isolado. Em SQL Server descartável, 10.000 leituras tiveram p95 de 443 ms com três consultas simultâneas. As 10.001 leituras foram rejeitadas. Excel e PDF de 10.000 registros levaram 0,724 s e 4,201 s com API simulada. Detalhes e limitações constam em fase-14-relatorios-inteligencia.md.

A implantação e o funcionamento em Azure ainda precisam de confirmação. Os tempos do ambiente isolado não garantem desempenho em produção. Testes físicos seguem para a Fase 16.

## Plano de homologação em produção

Após aprovação e merge deste PR, verificar workflows e versões efetivamente implantadas da API e frontend, consultar /health e /health/ready e logs, conferir relatórios autenticados com filtros e paginação e abrir Excel e PDF com dados autorizados. Usar consultas de leitura. Antes da operação, identificar a versão anterior comprovadamente funcional e os procedimentos de reversão dos dois App Services, verificando compatibilidade com o banco e capacidade de recuperação. Qualquer deploy ou rollback exige autorização.
