# Skill: Next.js Admin Dashboard — Transjap Horímetros

## Objetivo
Orientar o desenvolvimento do painel administrativo web do projeto **Transjap Horímetros**, com foco em organização, consistência visual, acessibilidade, manutenção e integração futura com uma API própria.

## Contexto do Projeto
O sistema será utilizado para gestão de horímetros de máquinas pesadas da Transjap.

O painel web deverá permitir:
- visualizar o estado geral da frota;
- consultar leituras de horímetro;
- visualizar evidências fotográficas;
- identificar máquinas sem leitura;
- identificar leituras suspeitas;
- administrar máquinas, obras e usuários;
- gerar relatórios em Excel e PDF;
- acompanhar alertas e inconsistências;
- manter histórico rastreável.

O aplicativo móvel será desenvolvido posteriormente e consumirá a mesma API utilizada pelo painel web.

## Stack Recomendada
- Next.js
- TypeScript
- React
- Tailwind CSS
- shadcn/ui
- React Hook Form
- Zod
- TanStack Table
- TanStack Query ou equivalente
- Lucide Icons

## Princípios de Arquitetura Frontend

### 1. Separação por domínio
Não concentrar toda a lógica em `app/`.

Estrutura preferencial:

```text
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
```

### 2. Componentes reutilizáveis
Criar componentes compartilhados para:
- tabelas;
- filtros;
- paginação;
- badges de status;
- cards;
- modais;
- formulários;
- visualização de foto;
- confirmação de ações;
- mensagens de erro;
- estados vazios;
- carregamento.

### 3. Server e Client Components
Usar Server Components por padrão.

Usar `"use client"` somente quando necessário, como:
- formulários interativos;
- tabelas com filtros client-side;
- dialogs;
- componentes que dependem de hooks;
- componentes que dependem de browser APIs.

### 4. API como fonte dos dados
O frontend nunca acessa diretamente PostgreSQL.

Fluxo obrigatório:

```text
Next.js Web
   ↓ HTTPS
API
   ↓
Banco de Dados
```

Criar uma camada de serviços:

```text
services/
├── auth.service.ts
├── machines.service.ts
├── readings.service.ts
├── worksites.service.ts
├── users.service.ts
├── anomalies.service.ts
└── reports.service.ts
```

## Identidade Visual

Usar como base a identidade institucional da Transjap.

Diretrizes:
- azul escuro como cor estrutural;
- azul médio como cor secundária;
- amarelo como cor de destaque;
- branco e cinzas neutros para áreas de leitura;
- evitar visual futurista;
- evitar excesso de gradientes;
- evitar estética genérica de dashboard de IA.

A interface deve transmitir:
- operação;
- engenharia;
- confiabilidade;
- controle;
- clareza.

## Layout Principal

### Sidebar
Itens iniciais:

```text
Dashboard
Frota
Horímetros
Obras
Usuários
Alertas
Relatórios
Configurações
```

### Header
Mostrar:
- título da página;
- usuário autenticado;
- perfil;
- logout;
- eventualmente obra/filtro global.

## Dashboard

Exibir indicadores como:

```text
Total de máquinas
Atualizadas hoje
Sem leitura
Leituras pendentes
Leituras suspeitas
Alertas críticos
```

Seções recomendadas:
- indicadores;
- alertas recentes;
- últimas leituras;
- máquinas sem atualização;
- evolução de registros;
- atalhos operacionais.

## Página de Frota

Tabela mínima:

```text
Frota
Modelo
Status
Último horímetro
Data da última leitura
Obra
QR Code
Situação
Ações
```

Filtros:
- número da frota;
- modelo;
- status;
- obra;
- com/sem leitura recente.

### Página individual da máquina
Mostrar:
- frota;
- modelo;
- status;
- QR;
- horímetro atual;
- última atualização;
- obra;
- histórico;
- fotos;
- alertas;
- gráfico de evolução;
- auditoria relacionada.

## Página de Horímetros

Tabela mínima:

```text
Data/hora
Frota
Modelo
Horímetro
Diferença
Operador
Obra
Status
OCR
Foto
Ações
```

Filtros:
- período;
- frota;
- obra;
- operador;
- status;
- com alerta;
- sem alerta.

Status previstos:

```text
VALIDATED
PENDING
SUSPECT
CORRECTED
REJECTED
```

Representação visual:
- verde: validado;
- amarelo: pendente;
- laranja: suspeito;
- azul: corrigido;
- vermelho: rejeitado.

Não depender somente de cor. Sempre mostrar texto ou ícone.

## Formulários

Usar:
- React Hook Form;
- Zod;
- validação visual;
- mensagens claras;
- confirmação para operações críticas.

Nunca confiar apenas na validação do frontend.

O backend deve repetir todas as validações importantes.

## Tabelas

Para tabelas grandes:
- paginação;
- ordenação;
- busca;
- filtros;
- colunas configuráveis;
- loading skeleton;
- estado vazio;
- erro recuperável.

Não carregar milhares de registros de uma só vez.

Preferir paginação server-side para leituras de horímetro.

## Estados de Interface

Toda tela que depende de dados deve possuir:
- loading;
- success;
- empty;
- error.

Exemplo:

```text
Carregando registros...
Nenhum registro encontrado.
Erro ao carregar registros.
Tentar novamente.
```

## Responsividade

O painel é prioritariamente desktop, mas deve funcionar em:
- notebooks;
- tablets;
- celulares.

Em telas pequenas:
- sidebar recolhível;
- tabelas podem virar cards ou permitir scroll horizontal;
- ações críticas continuam acessíveis.

## Segurança no Frontend

Nunca armazenar:
- senha;
- segredo;
- connection string;
- chave de storage;
- chave de banco.

Tokens sensíveis devem ser tratados de forma segura.

Nunca considerar autorização do frontend como segurança real.

A API deve validar permissões.

## Perfis de Usuário

Preparar a interface para RBAC:

```text
OPERATOR
SUPERVISOR
ADMINISTRATIVE
MANAGER
ADMIN
```

O menu e as ações podem mudar conforme a permissão, mas a API continua sendo a autoridade.

## Boas Práticas de Código

- TypeScript estrito;
- evitar `any`;
- componentes pequenos;
- nomes explícitos;
- schemas centralizados;
- não duplicar regra de negócio;
- tratamento centralizado de erro HTTP;
- evitar chamadas API diretamente dentro de componentes visuais;
- usar DTOs/tipos bem definidos;
- usar aliases de importação.

## Qualidade

Antes de concluir uma funcionalidade:
- validar TypeScript;
- executar lint;
- testar navegação;
- testar estado vazio;
- testar falha da API;
- verificar responsividade;
- verificar acessibilidade básica;
- verificar que não existem secrets no bundle.

## Critério de Conclusão

Uma página só está pronta quando:
1. está integrada ao layout;
2. funciona com dados reais ou mock claramente isolado;
3. possui estados de loading/erro/vazio;
4. possui tipos;
5. possui validação;
6. respeita permissões;
7. possui tratamento de erros;
8. está responsiva;
9. mantém identidade Transjap;
10. está preparada para consumir a API oficial.
