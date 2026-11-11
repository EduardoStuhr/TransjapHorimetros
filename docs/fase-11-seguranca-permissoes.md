# Fase 11 — Segurança, permissões e controle de acesso

## Diagnóstico inicial

A árvore analisada já possui JWT com emissor, audiência, assinatura e expiração
validados, fallback de autenticação, rate limiting, políticas por papel,
identidade de dispositivo P-256, auditoria de mutações e proteção de uploads.

O fluxo administrativo da Fase 10 está presente: a entidade `ReadingReview`, a
migration `20261111170305_AddReadingReviews`, o serviço operacional e os
endpoints `GET`/`POST /api/v1/operations/readings/{id}/review`. O GET estava
protegido por `CanRecordReading`, permitindo que operadores consultassem
justificativas e e-mails de revisores.

Foram encontrados dois problemas concretos nesta árvore:

1. O Route Handler de exportação gerava Excel/PDF sem validar sessão ou papel;
   a proteção da navegação não protegia URLs diretas.
2. Consultas de leitura autenticadas retornavam para operadores e-mails,
   identificadores de usuário/dispositivo, detalhes de OCR, anomalias e
   correções administrativas.
3. O build Release falhava por argumentos C# nomeados e posicionais fora de
   ordem, e dois testes de exportação usavam um mock incompatível com a nova
   camada de autenticação.

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
- `GET /api/v1/operations/readings/{id}/review` agora reutiliza
  `CanReviewAnomalies`, bloqueando operadores no backend e preservando
  justificativa/e-mail somente para perfis administrativos.
- O Route Handler de relatórios voltou a usar a chamada autenticada existente
  (`apiFetch`), preservando os testes de filtros e a autorização sem duplicar
  a consulta de sessão.

## Riscos residuais e pendências

- Downloads de fotos ainda usam a policy operacional ampla e não registram
  visualização; isso deve ser resolvido junto com a regra de escopo por obra ou
  máquina.
- Não há endpoint de relatórios no backend; as exportações são geradas pelo
  frontend usando consultas existentes.

## Compatibilidade

Não houve alteração de schema ou migration. A migration da Fase 10 foi
preservada. A identidade de dispositivos, fila offline, JWT, auditoria
histórica e dados de máquinas, obras, leituras, fotos, OCR e revisões foram
preservados.
