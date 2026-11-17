# Runbook de produção — preparação

Este procedimento exige validação em ambiente isolado antes da utilização. Execuções que alterem infraestrutura, banco ou publicação dependem de autorização específica.

## Diagnóstico

1. Registre UTC, commit esperado, resultado dos workflows e correlation ID. Evite copiar tokens, senhas, cabeçalhos ou dados de fotografias.
2. Consulte /health/live, /health/ready e /health. Live cobre o processo. Ready também verifica SQL. Consulte disponibilidade do frontend e logs de inicialização, OCR e requisições.
3. Em falhas SQL, verifique rede, disponibilidade e configuração sem expor connection strings. Em fila offline, preserve eventos locais, ClientEventId e blobs até confirmação; avalie autenticação, identidade de dispositivo, status e Retry-After. Não force limpeza da fila.
4. Investigue erros de OCR sem alterar a leitura original. Consulte status e tentativas de processamento com acesso autorizado.

## Migrations controladas

O código aplica migrations na inicialização quando Database:ApplyMigrationsOnStartup está ausente. Confirme o valor efetivo no App Service sem divulgar segredos. Para uma alteração futura, compare __EFMigrationsHistory e schema, revise o script incremental EF, teste aplicação e recuperação em SQL Server descartável e confirme restauração point-in-time, limites e janela. Descreva locks e compatibilidade entre versões. A aplicação em produção depende de autorização específica. Nunca recrie banco ou presuma rollback após timeout.

## Publicação

Os dois workflows versionados executam em push na main. O job de deploy depende do build do respectivo workflow, sem dependência explícita da suíte do PR. Como proteção externa a ser configurada, no GitHub crie um environment de produção, habilite revisores obrigatórios se disponíveis no plano, selecione-o nos jobs de deploy e valide a espera de aprovação em repositório de teste. Configure também uma ruleset para exigir a CI na main. Confirme elegibilidade do plano e mantenha acesso administrativo de recuperação. Até que a configuração seja verificada, trate a proteção como não aplicada.

Antes de publicar, registre os artefatos imutáveis, commit aprovado e anterior, compatibilidade do schema, configuração e estado de saúde. Após autorização e publicação, confirme ambos os serviços, versionamento implantado, endpoints de saúde, logs e consultas administrativas de leitura.

## Rollback proposto

Identifique e retenha previamente artefatos íntegros da API e frontend da última versão funcional. Valide a restauração em ambiente isolado, usando as mesmas configurações, teste de saúde e consultas autenticadas. Determine a ordem pela compatibilidade de cada par API/frontend. Alterações aditivas de schema devem ser avaliadas para compatibilidade retroativa; não execute automaticamente Down ou restauração de banco, pois podem afetar dados posteriores. Para schema incompatível, prepare recuperação específica e avalie perda de dados antes de qualquer alteração. Confirme disponibilidade de artefatos, permissões e suporte do plano — slots não são presumidos.

A reversão da publicação e qualquer ação sobre o banco em produção exigem autorização explícita e acompanhamento dos logs e saúde.
