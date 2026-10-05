# Transjap Horímetros — Web

Primeira versão do painel administrativo para gestão de horímetros da Transjap. Esta entrega contém somente o frontend web e não implementa login, backend, OCR ou aplicativo mobile.

## Executar localmente

Requisitos:

- Node.js 20.9 ou superior
- npm

Comandos:

~~~bash
npm install
npm run dev
~~~

Acesse http://localhost:3000.

Para validar a versão de produção:

~~~bash
npm run lint
npm run build
npm start
~~~

## Rotas

- / — Dashboard
- /frota — Cadastro real da frota fornecida
- /frota/[fleetNumber] — Detalhes da máquina
- /horimetros — Leituras e filtros
- /obras — Obras
- /alertas — Inconsistências
- /relatorios — Relatórios e exportações

## Dados e integrações

O cadastro de frota em src/data/machines.ts é a única fonte local preenchida. Leituras, obras e alertas retornam coleções vazias pelos serviços correspondentes. A interface não cria registros fictícios.

As páginas consomem funções em src/services/, mantendo a origem dos dados isolada para a futura API ASP.NET Core. Os contratos do domínio ficam em src/types/domain.ts.

Quando o backend for iniciado, o frontend poderá ser movido para apps/web e a API para apps/api sem alterar a separação atual por features.

## Funcionalidades futuras

- autenticação e RBAC;
- integração com API ASP.NET Core;
- persistência PostgreSQL;
- upload e consulta de evidências;
- OCR e comparação com valor informado;
- sincronização do aplicativo mobile;
- exportações Excel e PDF;
- auditoria e revisão de anomalias.
