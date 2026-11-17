# Runbook de produção — preparação

Este procedimento exige validação em ambiente isolado antes da utilização. Execuções que alterem infraestrutura, banco ou publicação dependem de autorização específica.

## Diagnóstico

1. Registre UTC, commit esperado, resultado dos workflows e correlation ID. Evite copiar tokens, senhas, cabeçalhos ou dados de fotografias.
2. Consulte /health/live, /health/ready e /health. Live cobre o processo. Ready também verifica SQL. Consulte disponibilidade do frontend e logs de inicialização, OCR e requisições.
3. Em falhas SQL, verifique rede, disponibilidade e configuração sem expor connection strings. Em fila offline, preserve eventos locais, ClientEventId e blobs até confirmação; avalie autenticação, identidade de dispositivo, status e Retry-After. Não force limpeza da fila.
4. Investigue erros de OCR sem alterar a leitura original. Consulte status e tentativas de processamento com acesso autorizado.

## Migrations controladas

Nesta revisão, startup só aplica migrations e seed em Development ou Testing, com Database:ApplyMigrationsOnStartup explicitamente habilitado na configuração composta. Mantenha o ambiente do App Service em Production. Confirme o valor efetivo no App Service sem divulgar segredos. Para uma alteração futura, compare __EFMigrationsHistory e schema, revise o script incremental EF, teste aplicação e recuperação em SQL Server descartável e confirme restauração point-in-time, limites e janela. Descreva locks e compatibilidade entre versões. A aplicação em produção depende de autorização específica. Nunca recrie banco ou presuma rollback após timeout.

## Publicação

Os workflows propostos exigem acionamento manual na main e permitem o job com credenciais Azure apenas quando o SHA corresponde à variável de repositório APPROVED_API_SHA ou APPROVED_WEB_SHA. Em Settings → Secrets and variables → Actions → Variables, um responsável autorizado registra o SHA integral revisado após CI verde. Rode o workflow correspondente na main e retire o valor após a publicação. Restrinja permissão de editar variáveis e workflows. A mesma versão pode ser reexecutada enquanto a variável continuar preenchida. O controle por SHA sozinho não garante segregação entre aprovador e executor. Configure proteção da main e, onde o plano permitir, revisores obrigatórios no environment de produção. Antes de vincular o environment ao job, valide a identidade federada do Azure para o novo subject OIDC. Configurações externas e autenticação efetiva permanecem pendentes.

Antes de publicar, registre os artefatos imutáveis, commit aprovado e anterior, compatibilidade do schema, configuração e estado de saúde. Após autorização e publicação, confirme ambos os serviços, versionamento implantado, endpoints de saúde, logs e consultas administrativas de leitura.

## Rollback proposto

Identifique e retenha previamente artefatos íntegros da API e frontend da última versão funcional. Valide a restauração em ambiente isolado, usando as mesmas configurações, teste de saúde e consultas autenticadas. Determine a ordem pela compatibilidade de cada par API/frontend. Alterações aditivas de schema devem ser avaliadas para compatibilidade retroativa; não execute automaticamente Down ou restauração de banco, pois podem afetar dados posteriores. Para schema incompatível, prepare recuperação específica e avalie perda de dados antes de qualquer alteração. Confirme disponibilidade de artefatos, permissões e suporte do plano — slots não são presumidos.

A reversão da publicação e qualquer ação sobre o banco em produção exigem autorização explícita e acompanhamento dos logs e saúde.

## Falha de readiness SQL observada em 17/11/2026

Com live 200 e ready 503, suspenda publicações. Pesquise o traceId e a janela de tempo da ocorrência nos logs da API. Consulte Resource Health e métricas de conexões do Azure SQL, eventos de pausa/retomada, falhas de autenticação e conectividade do App Service. Confira somente o nome do banco e servidor, identidade e regras de rede aplicáveis. Registre códigos SQL sanitizados. Repita os GETs depois da análise e confirme uma consulta autenticada antes de encerrar o incidente. Reinício, rollback ou alteração de firewall exigem diagnóstico e autorização.

O diagnóstico do portal mostrou Paused e zero segundos de vCore, com a oferta mensal esgotada. Confira a janela de renovação da oferta. Retomar com cobrança requer aprovação financeira específica. Evite retries intensivos. Após a recuperação, confirme ready, consulta autenticada e consumo da cota.

### Ensaio reproduzível no PR

A CI usa um SQL Server local descartável. O script fase15_recovery.sh cria um backup COPY_ONLY com CHECKSUM, verifica o backup, restaura em um banco distinto e compara o conteúdo das tabelas nas duas direções. Em seguida inicia a API candidata e a base do PR contra a cópia, verifica ready e o bloqueio de leitura anônima, e repete a comparação. A coleta dos planos acrescenta 1.000 leituras sintéticas somente na cópia depois de concluir a comparação. O teste de guarda inicia também a candidata em Production com a flag ligada e verifica que uma base ausente permanece ausente. Esse procedimento não deve ser apontado para o Azure. O ensaio local do frontend alternou os artefatos no mesmo endereço e validou um PDF em cada versão.

A execução 44096801024 validou o ensaio isolado de backup, comparação das tabelas, inicialização da candidata e retorno da API à base. No Azure, o erro 42119 no fluxo da API coincide com o esgotamento da oferta. O processo usa Production e a configuração consultada de migrations estava false. Após a renovação da cota, confira se o worker retoma as tarefas existentes e se o consumo cabe no orçamento antes de habilitar publicação. Guarde versões compatíveis com patches de segurança e verifique retenção dos artefatos e PITR reais antes de agendar mudanças.
