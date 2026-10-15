# Fase 3 — Segurança e autenticação

## Visão geral

A Fase 3 protege a API com ASP.NET Core Identity, JWT Bearer, autorização por papéis e refresh tokens rotativos. O Next.js funciona como BFF para que os tokens não fiquem acessíveis ao JavaScript do navegador.

A migration `AddIdentityAndAuthentication` foi criada, revisada e **não foi aplicada no Azure**.

## Autenticação da API

O `Program.cs` registra o esquema oficial `JwtBearer` e valida issuer, audience, chave, assinatura, expiração e lifetime. A chave vem de `Authentication__Jwt__SigningKey`, deve ter pelo menos 32 bytes e nunca possui fallback hardcoded. `UseAuthentication` executa antes de `UseAuthorization`.

A fallback policy exige usuário autenticado. Somente estes endpoints são anônimos:

- `POST /api/v1/auth/login`;
- `POST /api/v1/auth/refresh`;
- `/health`, `/health/live` e `/health/ready`.

## Papéis e policies

Papéis oficiais: `OPERATOR`, `SUPERVISOR`, `ADMINISTRATIVE`, `MANAGER` e `ADMIN`.

| Policy | Papéis autorizados |
| :--- | :--- |
| `CanRecordReading` | OPERATOR, SUPERVISOR, MANAGER, ADMIN |
| `CanResolveQr` | OPERATOR, SUPERVISOR, ADMINISTRATIVE, MANAGER, ADMIN |
| `CanManageMachines` | ADMINISTRATIVE, MANAGER, ADMIN |
| `CanManageQrCodes` | MANAGER, ADMIN |
| `CanViewQrHistory` | SUPERVISOR, ADMINISTRATIVE, MANAGER, ADMIN |
| `CanManageWorkSites` | ADMINISTRATIVE, MANAGER, ADMIN |
| `CanViewDashboard` | SUPERVISOR, MANAGER, ADMIN |
| `CanReviewAnomalies` | SUPERVISOR, MANAGER, ADMIN |
| `CanManageUsers` | ADMIN |

O menu filtra itens incompatíveis com os papéis do usuário, mas isso é apenas UX. A API é sempre a autoridade real e retorna `403` quando o papel não atende à policy.

## Sessão e refresh rotation

O endpoint de login retorna access e refresh tokens somente para a camada BFF. Os Route Handlers do Next.js removem os tokens da resposta ao navegador e os armazenam em cookies com `HttpOnly=true`, `SameSite=Strict`, `Secure=true` em produção, `Path=/` e expiração explícita.

Não há token em `localStorage` ou `sessionStorage`. O BFF implementa `/api/auth/login`, `/api/auth/me`, `/api/auth/refresh` e `/api/auth/logout`. O Proxy renova a sessão perto da expiração e encaminha os cookies rotacionados para a renderização corrente. Em falha de autenticação, a navegação vai para `/login`; em `403`, vai para `/acesso-negado`.

Cada refresh token é armazenado somente como SHA-256 no banco. Ao renovar, o token atual é revogado e ligado ao substituto. Reutilizar o token anterior retorna `401`. Logout revoga os tokens ativos do usuário.

## Lockout e rate limiting

O Identity bloqueia a conta por 15 minutos após cinco falhas de senha. O limite geral da API é configurado em `RateLimiting`, e o login possui a policy separada `auth-login`, com padrão de cinco requisições por minuto por IP. Ambos podem ser ajustados sem recompilar.

## CurrentUser e auditoria

`ICurrentUser` é scoped e extrai `UserId`, e-mail, nome, papéis e estado autenticado apenas das claims validadas do JWT. Serviços de aplicação usam esse contexto para auditoria; nenhum endpoint aceita `actorUserId` enviado no body.

A migration acrescenta `user_email` a `audit_logs` e cria `users`, `roles`, `user_roles`, `user_claims`, `user_logins`, `role_claims`, `user_tokens` e `refresh_tokens`.

## Bootstrap do primeiro administrador

Na inicialização, os cinco papéis são criados de forma idempotente. O primeiro `ADMIN` só é provisionado quando ainda não existe membro desse papel e as três variáveis estão presentes:

```text
BootstrapAdmin__Email
BootstrapAdmin__Password
BootstrapAdmin__FullName
```

A senha nunca é registrada em log e não é redefinida automaticamente. Depois do provisionamento, remova esses secrets da configuração.

## Secrets no Azure

Configure no App Service Configuration ou por referências do Key Vault:

```text
Authentication__Jwt__SigningKey
Authentication__Jwt__Issuer
Authentication__Jwt__Audience
ConnectionStrings__DefaultConnection
BootstrapAdmin__Email
BootstrapAdmin__Password
BootstrapAdmin__FullName
API_URL
```

Use HTTPS, mantenha `Secure` ativo e não use valores reais em `appsettings.json`, `.env.example`, logs ou commits.

## Testes e banco seguro

A suíte cobre `401`, login válido e inválido, usuário inativo, policies de OPERATOR/ADMIN/SUPERVISOR, rotação, rejeição do refresh anterior, logout e `/auth/me`. O `TestDatabaseGuard` continua exigindo o banco local com nome exato `transjap_horimetros_tests` e rejeita Azure SQL.
