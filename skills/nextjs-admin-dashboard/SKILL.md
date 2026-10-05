---
name: nextjs-admin-dashboard
description: Projetar, implementar e revisar o painel administrativo Next.js do Transjap Horímetros com TypeScript, Tailwind, shadcn/ui, formulários, tabelas, acessibilidade, responsividade, RBAC e integração com API. Usar em páginas, componentes, layouts, dashboards, serviços HTTP, schemas, estados de interface e relatórios web deste projeto.
---

# Next.js Admin Dashboard

Construir um painel operacional claro, consistente e preparado para consumir a API oficial do Transjap Horímetros.

## Organizar o frontend

Preferir a estrutura:

~~~text
src/
├── app/
├── components/
│   ├── layout/
│   ├── ui/
│   └── shared/
├── features/
│   ├── auth/
│   ├── machines/
│   ├── readings/
│   ├── worksites/
│   ├── users/
│   ├── anomalies/
│   └── reports/
├── lib/
├── services/
├── hooks/
├── schemas/
└── types/
~~~

- Usar Server Components por padrão.
- Adicionar use client apenas para interação, hooks ou APIs do navegador.
- Manter TypeScript estrito e evitar any.
- Criar componentes pequenos e reutilizáveis para tabelas, filtros, paginação, badges, cards, dialogs, formulários, fotos, confirmações, erros, estados vazios e skeletons.
- Centralizar schemas e tratamento de erro HTTP.
- Não chamar a API diretamente em componentes puramente visuais.

## Consumir dados

Nunca acessar PostgreSQL pelo frontend. Consumir apenas a API por HTTPS.

Criar uma camada de serviços:

~~~text
services/
├── auth.service.ts
├── machines.service.ts
├── readings.service.ts
├── worksites.service.ts
├── users.service.ts
├── anomalies.service.ts
└── reports.service.ts
~~~

Usar DTOs e tipos explícitos. Isolar mocks de forma clara para facilitar a troca por dados reais.

## Aplicar a identidade Transjap

- Usar azul escuro como cor estrutural, azul médio como secundária e amarelo como destaque.
- Usar branco e cinzas neutros nas áreas de leitura.
- Transmitir operação, engenharia, confiabilidade, controle e clareza.
- Evitar visual futurista, gradientes excessivos e estética genérica de dashboard gerado por IA.
- Usar ícones Lucide quando adequados.

## Montar o layout

Criar sidebar com Dashboard, Frota, Horímetros, Obras, Usuários, Alertas, Relatórios e Configurações.

Mostrar no header o título da página, usuário autenticado, perfil, logout e, quando útil, filtro global ou obra atual.

Adaptar o layout a notebooks, tablets e celulares. Recolher a sidebar em telas pequenas e preservar o acesso às ações críticas.

## Construir o dashboard

Priorizar informação acionável. Exibir total de máquinas, atualizadas hoje, sem leitura, leituras pendentes, suspeitas e alertas críticos.

Incluir indicadores, alertas recentes, últimas leituras, máquinas sem atualização, evolução de registros e atalhos operacionais.

## Construir páginas de frota e leituras

Na frota, mostrar no mínimo:

~~~text
Frota | Modelo | Status | Último horímetro | Última leitura
Obra | QR Code | Situação | Ações
~~~

Permitir filtrar por frota, modelo, status, obra e presença de leitura recente.

Na página da máquina, mostrar cadastro, QR, horímetro atual, última atualização, obra, histórico, fotos, alertas, evolução e auditoria.

Na página de horímetros, mostrar:

~~~text
Data/hora | Frota | Modelo | Horímetro | Diferença
Operador | Obra | Status | OCR | Foto | Ações
~~~

Permitir filtrar por período, frota, obra, operador, status e presença de alerta.

Representar VALIDATED, PENDING, SUSPECT, CORRECTED e REJECTED com texto ou ícone além da cor.

## Implementar formulários e tabelas

- Usar React Hook Form e Zod.
- Exibir mensagens claras e confirmação para operações críticas.
- Repetir no backend todas as validações importantes.
- Usar paginação, ordenação, busca, filtros, colunas configuráveis, skeleton, vazio e erro recuperável em tabelas grandes.
- Preferir paginação server-side para leituras.
- Permitir scroll horizontal ou visualização em cards em telas pequenas.

## Cobrir estados e acessibilidade

Toda tela dependente de dados deve possuir estados de loading, success, empty e error, incluindo ação de tentar novamente quando fizer sentido.

Garantir navegação por teclado, foco visível, labels, contraste e nomes acessíveis. Não comunicar status apenas por cor.

## Aplicar segurança e RBAC

- Nunca incluir senha, segredo, connection string ou chave de infraestrutura no frontend.
- Tratar tokens sensíveis com mecanismo seguro compatível com a arquitetura escolhida.
- Adaptar menus e ações aos perfis OPERATOR, SUPERVISOR, ADMINISTRATIVE, MANAGER e ADMIN.
- Considerar a API como autoridade final de autorização.

## Concluir a página

Só considerar uma página pronta quando estiver integrada ao layout, usar dados reais ou mock isolado, possuir loading/erro/vazio, tipos, validação, permissões, responsividade e acessibilidade básica. Validar TypeScript, lint, navegação, falhas da API e ausência de secrets no bundle.
