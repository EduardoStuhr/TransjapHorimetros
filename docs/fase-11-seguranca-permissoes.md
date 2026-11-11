# Fase 11 — Segurança, permissões e controle de acesso

## Diagnóstico inicial

A árvore analisada já possui JWT com emissor, audiência, assinatura e expiração
validados, fallback de autenticação, rate limiting, políticas por papel,
identidade de dispositivo P-256, auditoria de mutações e proteção de uploads.

O endpoint `GET /operations/readings/{id}/review` e a migration
`AddReadingReviews` mencionados no escopo não estão presentes nesta revisão do
repositório. Portanto, não foi criado um contrato de revisão sem entidade,
persistência ou regra de negócio correspondente. Esse item permanece uma
pendência de integração com a versão que contém a Fase 10.

Foram encontrados dois problemas concretos nesta árvore:

1. O Route Handler de exportação gerava Excel/PDF sem validar sessão ou papel;
   a proteção da navegação não protegia URLs diretas.
2. Consultas de leitura autenticadas retornavam para operadores e-mails,
   identificadores de usuário/dispositivo, detalhes de OCR, anomalias e
   correções administrativas.

## Matriz de permissões efetiva

| Capacidade | Operador | Supervisor | Administrativo | Gerente | Admin |
| --- | --- | --- | --- | --- | --- |
| Registrar leitura e foto | Sim | Sim | Sim | Sim | Sim |
| Resolver QR | Sim | Sim | Sim | Sim | Sim |
| Dashboard | Não | Sim | Sim | Sim | Sim |
| Consultar anomalias e OCR administrativo | Não | Sim | Sim | Sim | Sim |
| Exportar relatórios | Não | Sim | Sim | Sim | Sim |
| Gerenciar máquinas, QR e obras | Não | Não | Sim | Sim | Sim |
| Gerenciar dispositivos | Não | Não | Não | Sim | Sim |
| Gerenciar usuários | Não | Não | Não | Não | Sim |

Esta matriz reflete as policies registradas em `Program.cs`. A consulta
operacional de leituras continua autenticada, mas seus campos administrativos
ficam redigidos para operadores.

## Correções implementadas

- Exportações agora validam o token e consultam `/api/v1/auth/me` antes de
  carregar dados ou gerar o arquivo; respostas diretas sem permissão retornam
  `401` ou `403`.
- A navegação passou a incluir `ADMINISTRATIVE` nas capacidades que o backend
  já autorizava.
- `GET /api/v1/readings`, `GET /api/v1/readings/{id}` e leituras por máquina
  redigem, para operadores, autoria, dispositivo, vínculo de correção,
  dispositivo registrado, OCR e listas detalhadas de anomalias/correções.
  Contagens e status operacionais permanecem disponíveis.
- A criação de leitura preserva a resposta completa necessária à
  compatibilidade do fluxo offline; a redação aplica-se às consultas.

## Riscos residuais e pendências

- O contrato de revisão administrativa citado no plano precisa ser integrado
  antes de implementar aprovação/rejeição e auditoria de decisões.
- Downloads de fotos ainda usam a policy operacional ampla e não registram
  visualização; isso deve ser resolvido junto com a regra de escopo por obra ou
  máquina.
- Não há endpoint de relatórios no backend; as exportações são geradas pelo
  frontend usando consultas existentes.

## Compatibilidade

Não houve alteração de schema ou migration. A identidade de dispositivos,
fila offline, JWT, auditoria histórica e dados de máquinas, obras, leituras,
fotos e OCR foram preservados.
