# Fase 8 — Experiência mobile do operador

## Objetivo

A Fase 8 transforma `/coleta` em um fluxo mobile-first para operação de campo sem substituir a infraestrutura offline entregue na Fase 7. O navegador continua usando o mesmo `IndexedDbOfflineStore`, os mesmos eventos `READING_CREATED` e `PHOTO_UPLOADED`, o mesmo `OfflineSyncService`, o mesmo transporte HTTP, as mesmas regras de retry/idempotência e a mesma tela `/sincronizacao`.

Nenhum contrato de backend, migration ou recurso Azure foi alterado.

## Arquitetura

O estado visível da coleta é uma máquina de estados curta:

1. `IDENTIFY_MACHINE`: câmera ou conteúdo de QR digitado;
2. `ENTER_READING`: máquina confirmada, leitura, tipo, obra e horário;
3. `CAPTURE_PHOTO`: captura, validação, prévia e repetição da foto;
4. `REVIEW`: resumo completo antes da confirmação;
5. `SAVING`: hash e gravação atômica no IndexedDB;
6. `SUCCESS`: confirmação local imediata e evolução do estado de sincronização.

As transições permitidas estão centralizadas em `reading-flow.ts`. Ao confirmar, uma trava síncrona impede double submit. A leitura e o blob da foto são persistidos na mesma transação local antes de qualquer tentativa de rede. Depois do enqueue, o provider existente atualiza a fila e chama o motor de sincronização em segundo plano quando há conectividade.

## Identificação por QR

O scanner usa `BarcodeDetector` com formato `qr_code` quando o navegador declara suporte. Nos demais navegadores, `qr-scanner` é carregado dinamicamente como fallback leve. A implementação oferece câmera traseira preferencial, troca de câmera, cooldown contra leituras repetidas e mensagens específicas para permissão negada ou câmera ausente.

O stream é encerrado após uma leitura, ao fechar o leitor, ao trocar de etapa e no unmount do componente. Todas as tracks ativas recebem `stop()` e o `srcObject` do vídeo é removido.

Digitar o código permanece sempre disponível. Tanto a câmera quanto a entrada manual passam pela mesma resolução segura:

1. procura no cache oficial de máquinas ativas;
2. se não encontrar e houver rede, consulta `/api/qr-codes/resolve`;
3. atualiza o cache de referência;
4. só aceita o `machineId` retornado se a máquina existir no cache oficial recém-atualizado.

Assim, texto vindo do QR não se torna um `machineId` confiável por si só.

## Leitura, obra, horário e foto

- A unidade é herdada da máquina (`HOURS` ou `KM`) e não possui controle editável.
- O campo numérico aceita vírgula ou ponto e no máximo duas casas decimais.
- A leitura anterior é mostrada quando o cache a disponibilizar. Na ausência desse dado, a interface informa que o servidor fará a validação. Uma leitura menor gera aviso não bloqueante.
- O tipo `CORRECTION` não é oferecido ao operador.
- Uma única obra disponível é pré-selecionada; escolhas posteriores são preservadas ao iniciar a próxima máquina.
- O horário local atual é preenchido automaticamente e pode ser alterado explicitamente.
- A foto aceita JPEG, PNG ou WEBP até 10 MB, com `capture="environment"`, prévia e ação para tirar novamente.
- Metadados EXIF não são tratados como fonte autoritativa do horário.

## Offline e prontidão

O cartão de prontidão informa conectividade, quantidade de máquinas/obras, instalação do service worker e data do cache. Cache com mais de 24 horas recebe alerta de atualização. A atualização manual é desabilitada offline.

Se o dispositivo estiver offline e sem referências suficientes, a tela explica que a primeira preparação exige conexão. Depois da primeira carga autenticada e da preparação do cache, `/coleta` e `/sincronizacao` podem ser reabertos pelo app shell; eventos e fotos continuam duráveis no IndexedDB durante reloads.

Limitação deliberada: a sincronização automática ocorre quando o aplicativo está aberto e recebe conectividade. A Fase 8 não adiciona Background Sync, push ou processamento OCR local.

## PWA e shell do operador

O manifesto inicia em `/coleta`, usa modo `standalone`, orientação portrait e ícones quadrados de 192 e 512 pixels derivados do símbolo oficial da Transjap. O service worker da Fase 7 foi mantido restrito ao app shell e a recursos estáticos seguros; requisições `/api/*` continuam fora do cache.

Quando `beforeinstallprompt` é emitido, a interface apresenta uma sugestão discreta e dispensável. No iOS, exibe instrução para “Adicionar à Tela de Início”. A sugestão dispensada não reaparece na mesma sessão.

O perfil exclusivamente `OPERATOR` recebe:

- redirecionamento para `/coleta` após login e ao tentar acessar uma rota administrativa;
- menu reduzido a Coleta e Sincronização;
- navegação inferior mobile com alvos de toque de pelo menos 44 px;
- identificação “Operação de campo” no cabeçalho.

Safe areas de iOS são respeitadas no cabeçalho, conteúdo, navegação inferior e prompt de instalação. Controles possuem rótulos, foco visível, feedback com `role="alert"`/`aria-live` e textos que não dependem apenas de cor.

## Componentes principais

- `reading-capture-form.tsx`: orquestração e telas do fluxo;
- `reading-flow.ts`: estados, transições e validações puras;
- `qr-camera.tsx`: experiência visual e ciclo de vida da câmera;
- `qr-scanner-controller.ts`: scanner nativo/fallback e liberação de recursos;
- `qr-resolution.ts`: resolução segura via cache e API;
- `operator-bottom-navigation.tsx`: navegação mobile do operador;
- `install-prompt.tsx`: instalação PWA e orientação iOS;
- `SyncProvider`: apenas estendido para expor a prontidão do service worker existente.

## Validação

Os testes frontend cobrem parsing, validações, estados, double submit, cache vencido, QR por cache/API, rejeição de `machineId` não reconciliado, suporte do scanner nativo, liberação de tracks, shell do operador, manifesto e segurança do service worker. As suítes de IndexedDB e sincronização da Fase 7 permanecem inalteradas e continuam compondo a regressão.

Comandos de validação:

```text
npm run lint
npm test
npm run build
dotnet build apps/api/TransjapHorimetros.sln --configuration Release --no-restore
dotnet test apps/api/tests/TransjapHorimetros.UnitTests/TransjapHorimetros.UnitTests.csproj --configuration Release --no-build
```

A integração backend depende de SQL Server descartável e é executada pelo workflow oficial do pull request.
