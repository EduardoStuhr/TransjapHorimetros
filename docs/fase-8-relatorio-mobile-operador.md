# Fase 8 — Relatório da experiência mobile do operador

1. **Arquitetura mobile:** fluxo mobile-first orientado por estados, mantendo a infraestrutura offline da Fase 7 como única implementação de persistência e sincronização.
2. **Componentes criados:** fluxo, validações, resolução de QR, controlador de câmera, UI da câmera, navegação inferior e prompt PWA foram separados por responsabilidade.
3. **Fluxo de etapas:** identificação → leitura → foto → revisão → salvamento → sucesso, com transições explícitas e retorno somente antes da confirmação.
4. **Scanner QR:** `BarcodeDetector` é a primeira escolha quando há suporte declarado a `qr_code`.
5. **Fallback QR:** `qr-scanner` é importado sob demanda; a digitação manual usa o mesmo resolvedor e permanece disponível.
6. **Câmera:** preferência pela câmera traseira, troca entre dispositivos, tratamento de permissão negada e liberação integral do stream.
7. **Preview:** a foto gera URL local temporária, visualizada antes de entrar na revisão.
8. **Foto:** JPEG/PNG/WEBP, limite de 10 MB, blob mantido no IndexedDB e EXIF sem autoridade operacional.
9. **Leitura:** aceita inteiro, vírgula ou ponto e até duas casas decimais; valor menor que referência conhecida gera alerta não bloqueante.
10. **Unidade:** exibida a partir do cadastro da máquina e nunca editável pelo operador.
11. **Obra:** obrigatória, pré-selecionada quando única e preservada para a próxima máquina.
12. **Horário:** horário do dispositivo preenchido por padrão e edição opcional explícita.
13. **Confirmação:** resumo reúne máquina, valor, unidade, tipo, obra, horário e foto antes do enqueue.
14. **Sucesso:** diferencia salvo localmente, sincronizando e confirmado pelo servidor.
15. **Próxima máquina:** limpa máquina, leitura, foto e horário; mantém a obra para produtividade em campo.
16. **Offline:** resolução pelo cache, gravação local atômica e fila sobrevivem sem internet e a reloads.
17. **Online:** cache/API resolvem QR e o SyncEngine existente envia leitura antes da foto.
18. **PWA:** manifesto standalone inicia em `/coleta`; service worker cacheia apenas shell e estáticos seguros.
19. **Install prompt:** prompt nativo customizado quando disponível e instrução específica para instalação no iOS.
20. **Safe area:** cabeçalho, conteúdo, prompt e navegação inferior consideram `env(safe-area-inset-*)`.
21. **Touch targets:** ações essenciais e navegação mobile têm altura mínima de 44 px.
22. **Acessibilidade:** foco visível, rótulos, status sem dependência exclusiva de cor, regiões de alerta e feedback ao vivo.
23. **OPERATOR redirect:** login, navegação e proteção cliente reconduzem o operador para `/coleta`; somente Coleta e Sincronização ficam visíveis.
24. **Cache readiness:** cartão informa conectividade, quantidades de referências, data do cache e prontidão do service worker.
25. **Cache stale:** referências com mais de 24 horas recebem alerta e ação de atualização quando online.
26. **IndexedDB reutilizado:** `IndexedDbOfflineStore.enqueueReadingWithPhoto` continua responsável pela transação leitura + blob + evento de foto.
27. **SyncEngine reutilizado:** `OfflineSyncService`, `BrowserSyncTransport`, locks, retries, IDs e provider da Fase 7 não foram duplicados.
28. **Testes frontend:** 59 testes aprovados em 6 arquivos, incluindo 25 novos testes/variações da Fase 8 e os 34 testes de regressão existentes.
29. **Testes backend:** 198 testes unitários aprovados localmente.
30. **Integration tests:** sem execução local por ausência de SQL Server/Docker e da variável segura de teste; o workflow do PR provisiona SQL Server descartável e é a evidência conclusiva.
31. **Lint:** ESLint aprovado sem erros ou avisos.
32. **Build frontend:** Next.js 16.3.8/Turbopack aprovado.
33. **Build backend:** solução .NET 10 em Release aprovada com 0 erros e 0 avisos.
34. **CI:** validação conclusiva feita pelos jobs backend e frontend do pull request contra `main`.
35. **Migration:** não necessária; nenhum schema backend ou IndexedDB foi alterado.
36. **Azure:** nenhum recurso, configuração ou deployment Azure foi alterado.
37. **Branch:** `feat/phase-8-mobile-operator`, criada sobre a correção de CI ainda aberta no PR #12 para manter o bootstrap dos testes de integração executável.
38. **Commits:** implementação consolidada no commit solicitado `feat: implement mobile operator experience`.
39. **PR:** deve apontar `feat/phase-8-mobile-operator` para `main`, permanecer aberto e não ser mergeado/deployado.
40. **Riscos restantes:** instalação depende do suporte do navegador; primeira preparação exige internet; leitura anterior só aparece quando fornecida pelo cache; sincronização automática exige o app aberto; permissões de câmera continuam sob controle do sistema/navegador.

## Critérios de aceite

- [x] `/coleta` é mobile-first e orientada a etapas.
- [x] QR por câmera, fallback de biblioteca e entrada manual estão implementados.
- [x] QR offline usa somente o cache oficial.
- [x] Máquina e unidade herdada são mostradas claramente.
- [x] Leitura, foto, preview, repetição, revisão e sucesso estão implementados.
- [x] Double submit é bloqueado e enqueue antecede qualquer sync.
- [x] Cache vazio, cache vencido e atualização são tratados.
- [x] Shell, safe areas, alvos de toque e acessibilidade do OPERATOR foram adaptados.
- [x] Manifesto, instalação e service worker seguro foram validados.
- [x] Fila e tela de sincronização da Fase 7 foram reutilizadas sem duplicação.
- [x] Lint, testes frontend, build frontend, testes unitários backend e build backend passaram localmente.
- [ ] Jobs de CI do PR verdes (evidência registrada no próprio pull request).
- [x] Nenhuma migration e nenhuma alteração Azure.
- [x] PR não deve ser mergeado.
