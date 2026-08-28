# Dossiê técnico de evolução — ClawMachine

**Projeto analisado:** ClawMachine, protótipo Unity 6 / URP

**Perspectiva:** engenharia mobile, desenvolvimento de jogos, arquitetura de software e operação de produto

**Objetivo:** transformar o protótipo atual em uma experiência mobile de máquina de garra com captura de bichinhos, progressão, coleção, feedback audiovisual e base técnica sustentável.

## 1. Sumário executivo

O projeto possui uma boa prova de conceito visual e uma fantasia de jogo imediatamente compreensível: movimentar uma garra, tentar capturar uma pelúcia e receber uma recompensa. A implementação atual é adequada para validar a ideia, mas ainda não deve receber uma camada extensa de conteúdo ou monetização sem uma etapa de fundação técnica.

Os riscos mais importantes são: ausência de input touch real, falta de uma máquina de estados, contagem de prêmio no momento errado, dependência de nomes de objetos para detectar capturas, arquitetura excessivamente concentrada em `ClawController` e `UIManager`, configuração de cena ainda parcialmente herdada de template e ausência de persistência/progressão.

A recomendação é executar primeiro uma versão técnica 0.2. Essa versão deve entregar uma partida completa em mobile: abrir o jogo, tocar em jogar, controlar a garra por touch, descer, tentar capturar, retornar, entregar ou falhar, contabilizar corretamente o resultado, terminar o tempo e reiniciar sem estados presos. Só depois disso o projeto deve avançar para coleção, máquinas temáticas, economia, anúncios, compras e eventos.

## 2. Estado atual identificado

O projeto utiliza Unity 6000.5.9f1, Universal Render Pipeline, Input System e uGUI. A cena principal é `Assets/Scenes/SampleScene.unity`. O gameplay está concentrado em `Assets/Scripts/ClawController.cs`, enquanto `Assets/Scripts/UIManager.cs` constrói a interface e controla créditos, timer e contador de prêmios.

O `ClawController` monta gabinete, garra, cabo, piso, iluminação e prêmios em runtime. A captura usa `Physics.OverlapSphere`, filtra por nome contendo `Pelucia`, torna o rigidbody cinemático e parenta o objeto à garra. A UI é criada em runtime com `UnityEngine.UI.Text`, fonte legada e textos contendo emojis.

A configuração mobile já possui um `Mobile_RPAsset` separado, com render scale de 0,8, MSAA 1x, SRP Batcher e sombras adicionais desligadas. Entretanto, a entrada de gameplay lê diretamente teclado e não usa as ações touch presentes no asset de Input System. A cena também mantém campos serializados antigos (`gripForce`, `openVelocity`, `closeVelocity`) que não aparecem no script atual, além de um objeto `Dente` residual com componentes físicos.

## 3. Metas de produto

A versão recomendada deve ser um jogo casual de sessões curtas, com duração de aproximadamente 30 a 60 segundos por tentativa. O jogador deve compreender a mecânica sem tutorial longo, sentir tensão durante a descida e receber uma recompensa clara ao entregar um bichinho.

A proposta diferenciadora não deve ser apenas “mais uma máquina de garra”. Ela deve combinar **habilidade de posicionamento**, **coleção de bichinhos**, **máquinas temáticas** e **feedback emocional**. O jogador precisa ter um motivo para voltar mesmo quando falhar: completar coleções, desbloquear variantes, cumprir missões e receber tentativas gratuitas ou recompensas diárias.

## 4. Arquitetura-alvo

A arquitetura deve separar o domínio do jogo da apresentação e da plataforma. O código não deve depender de nomes visuais, busca global de objetos ou referências implícitas.

| Componente | Responsabilidade | Dependências permitidas |
|---|---|---|
| `GameSession` | Estados da partida, créditos, tempo, pontuação e resultado | Serviços de configuração e eventos |
| `ClawMachineController` | Orquestra o ciclo da máquina | `ClawMovement`, `ClawGrip`, `PrizeDeliveryZone` |
| `ClawMovement` | Movimento horizontal, profundidade e vertical | Input abstrato, configuração |
| `ClawGrip` | Abertura, fechamento, captura e soltura | `PrizeDetector`, `PrizeAttachmentPoint` |
| `Prize` | Identidade, peso, raridade, estado e corpo físico | Rigidbody, colliders |
| `PrizeSpawner` | Posicionamento inicial e reposição | `PrizeCatalog`, configuração |
| `PrizeDeliveryZone` | Confirma que o prêmio chegou à calha | Eventos de entrega |
| `InputRouter` | Converte touch, teclado e gamepad em comandos | Input System |
| `UIController` | Exibe estado; não toma decisões de negócio | Eventos de `GameSession` |
| `AudioFeedbackController` | Sons e feedback tátil | Eventos de gameplay |
| `SaveService` | Persistência local e futura sincronização | Dados serializáveis |
| `MachineConfig` | Parâmetros por máquina via `ScriptableObject` | Nenhuma lógica de cena |

O domínio deve comunicar eventos como `GameStarted`, `TimeChanged`, `ClawClosed`, `PrizeAttached`, `PrizeDelivered`, `PrizeDropped` e `GameFinished`. A UI deve reagir a esses eventos em vez de procurar objetos ou manter uma segunda versão da regra do jogo.

## 5. Modelo de estados obrigatório

A implementação deve ter estados explícitos. É proibido permitir que um comando altere a máquina quando o estado atual não o autoriza.

| Estado | Movimento | Ação da garra | Entrada permitida | Saída |
|---|---|---|---|---|
| `Idle` | Não | Não | Botão Jogar | `Playing` |
| `Playing` | Sim | Sim | Joystick e botão | `Capturing`, `GameOver` |
| `Capturing` | Limitado ou não | Animação | Nenhuma | `Returning` |
| `Returning` | Automático | Bloqueada | Nenhuma | `Delivering` ou `Playing` |
| `Delivering` | Automático | Aberta | Nenhuma | `Playing` |
| `GameOver` | Não | Soltar com segurança | Reiniciar | `Idle` |

Ao terminar o timer, a partida deve impedir novas entradas, finalizar a garra de modo seguro, liberar qualquer prêmio preso e emitir um resultado consistente. O contador de prêmios só deve aumentar após a confirmação de `PrizeDelivered`.

## 6. Controles mobile

O controle primário recomendado é um joystick virtual ou pad direcional para X/Z, uma área ou slider para altura e um botão grande para fechar/abrir a garra. Uma alternativa mais simples é usar arraste horizontal/vertical na própria tela e um botão dedicado para a ação.

Todos os dispositivos devem alimentar a mesma interface de comando. O gameplay não deve consultar `Keyboard.current` diretamente.

```csharp
public interface IClawInput
{
    Vector2 Movement { get; }
    float Vertical { get; }
    bool GripPressedThisFrame { get; }
}
```

Os controles devem ter área mínima confortável, não bloquear o botão de ação com a mão e respeitar safe areas. O sistema deve suportar pausa quando o aplicativo perde foco, bloqueio de duplo toque acidental e feedback visual de limite de movimento.

## 7. Captura e física

A detecção deve ser baseada em componente e camada, nunca em substring do nome. O collider pode estar em um filho; por isso a resolução deve usar `GetComponentInParent<Prize>()`.

```csharp
public sealed class Prize : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [SerializeField] private PrizeDefinition definition;

    public Rigidbody Body => body;
    public PrizeDefinition Definition => definition;
    public PrizeState State { get; private set; } = PrizeState.InPile;

    public void Attach(Transform anchor)
    {
        State = PrizeState.Attached;
        Body.isKinematic = true;
        transform.SetParent(anchor, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
```

A máquina deve ter uma estratégia de captura previsível. A física pode ser híbrida: os prêmios permanecem físicos no monte; durante a captura, um prêmio elegível é anexado a um ponto controlado; durante o transporte, existe uma chance configurável de escorregamento, baseada em peso, alinhamento e força da garra. O comportamento deve ser testável com seed fixa para reproduzir falhas.

A `OverlapSphere` deve receber `LayerMask`, raio configurável e ordenação por distância ou prioridade. O sistema deve impedir capturar dois prêmios, capturar um prêmio já anexado ou contar uma captura que não foi entregue.

## 8. Entrega e loop completo

A calha precisa ser uma zona de entrega real, com trigger e confirmação. O fluxo recomendado é:

1. O jogador inicia a partida e consome um crédito.
2. A garra pode ser movida dentro dos limites.
3. O jogador fecha a garra.
4. O sistema identifica um prêmio elegível ou registra falha.
5. A garra retorna automaticamente ou recebe comando de subida.
6. Se o prêmio entrar na zona da calha, ele é marcado como entregue.
7. A coleção, a pontuação e o feedback são atualizados.
8. O jogo oferece nova tentativa enquanto houver tempo ou créditos.
9. Ao terminar, a sessão é encerrada sem deixar objeto preso ou input ativo.

O resultado deve conter pelo menos `Delivered`, `Dropped`, `Missed` e `Aborted`. Isso permitirá analytics e balanceamento sem inferências frágeis.

## 9. Progressão e coleção de bichinhos

A referência mais forte para a direção “pegar bichinhos e colecionar” é **Clawbert**, da HyperBeard. A proposta oficial combina uma garra com personalidade, ovos surpresa, criaturas colecionáveis, raridade, mundos e eventos; a página do Google Play registra mais de 10 milhões de downloads e nota 4,5 no momento da consulta.[1] A página da App Store registra nota 4,7, categoria Family, eventos e uma coleção de criaturas obtidas por ovos surpresa.[2]

O ClawMachine pode se diferenciar sem copiar a apresentação. Em vez de ovos que aguardam incubação, a recompensa pode ser um bichinho físico capturado diretamente. Cada bichinho deve ter nome, silhueta, raridade, cor, animação de comemoração e uma entrada na coleção.

Uma coleção inicial poderia ter 24 bichinhos divididos em quatro famílias: animais domésticos, criaturas da floresta, animais aquáticos e monstros fofos. Cada máquina temática teria seis a oito prêmios predominantes e uma criatura rara com condição especial de aparição.

A progressão deve combinar habilidade e longo prazo:

| Sistema | Primeira versão |
|---|---|
| Coleção | Álbum com silhuetas e bichinhos descobertos |
| Raridade | Comum, incomum, raro e lendário |
| Missões | “Capture dois bichinhos azuis”, “entregue três prêmios” |
| Máquinas | Uma máquina inicial e uma segunda desbloqueável |
| Recompensa diária | Uma ficha ou tentativa especial |
| Personalização | Skins da máquina, adesivos e cores da garra |
| Falha | Recompensa parcial, moeda ou progresso de missão |

É importante evitar timers longos que interrompam a diversão principal. Reviews de Clawbert mencionam frustração com tempos de espera crescentes e recompensas pouco satisfatórias; isso é um alerta de design para não transformar a coleção em uma fila de espera excessiva.[2]

## 10. Referências de mercado atuais

Existem, sim, jogos conhecidos de máquina de garra nas duas lojas. A referência mais próxima de **capturar e colecionar bichinhos virtuais** é Clawbert. A referência mais próxima de **simular uma máquina física com pets de pelúcia** é Claw Crane Little Pets. Para uma referência de **operação online de máquinas reais e economia baseada em créditos**, Clawee é o principal caso.

| Jogo | Loja e sinal de mercado | O que estudar | Risco a evitar |
|---|---|---|---|
| **Clawbert** | Google Play: 10M+ downloads e 4,5; App Store: 4,7 | Personagem forte, coleção, raridade, mundos, eventos e retorno diário.[1][2] | Esperas longas, excesso de anúncios e sensação de grind, relatados em reviews.[2] |
| **Claw Crane Little Pets** | App Store: 4,6; série declarada pela desenvolvedora como tendo mais de 7 milhões de jogadores acumulados | Pets fofos, física/“pelúcia mole”, controle simples, coleção e vibração ao adquirir prêmio.[3] | Controles limitados e problemas de captura quando o pet está em determinada orientação, observados em reviews.[3] |
| **Clawee** | Google Play: 10M+ downloads, 4,3 e 230K reviews; App Store: 4,7 e 738K ratings | Catálogo, eventos, torneios, tickets, recompensas, vídeo da jogada e economia de créditos.[4][5] | Dependência de entrega física, custos de envio, VIP e críticas de monetização/atrasos.[4][5] |
| **Prize Claw** | App Store: 4,3 e 1,9K ratings | Mais de 60 prêmios, várias máquinas, missões, poderes, upgrades e personalização.[6] | Ads, compras e progressão que podem afastar a experiência do loop simples. |
| **Claw Machine Games Crane Game** | Google Play: 1M+ downloads e 3,9 | Máquinas temáticas, desbloqueio de arenas e progresso gratuito.[7] | Reviews mencionam excesso de anúncios, música sem opção clara e problemas de fluidez.[7] |

A conclusão de benchmarking é que há espaço para um produto híbrido: **a acessibilidade e a fantasia de coleção de Clawbert, a fofura e o foco em pets de Claw Crane Little Pets, e a variedade sistêmica de Prize Claw**, sem assumir o custo logístico de prêmios reais de Clawee.

## 11. Arte, câmera e apresentação

A máquina deve ter uma câmera fixa em landscape ou portrait definido pelo produto. A configuração atual combina referência de Canvas 1080 × 1920 com autorrotação em todas as direções; isso precisa ser resolvido antes do polish.

Para mobile, recomendo portrait se a prioridade for uma experiência casual de uma mão, ou landscape se a prioridade for controle espacial preciso e visão ampla da máquina. O MVP deve escolher apenas uma orientação. A câmera deve mostrar claramente a garra, a área de prêmios e a calha, mantendo a interface fora do campo de ação.

A geometria procedural é ótima para o protótipo, mas a versão de produto deve transformar o gabinete em prefab, compartilhar materiais, reduzir objetos redundantes e deixar o artista ajustar proporções no editor. Os 81 tiles do chão e os componentes de teste da cena devem ser revisados. O objeto residual `Dente`, além de campos antigos serializados, deve ser removido ou documentado.

## 12. Performance mobile

O pipeline mobile atual é um bom ponto de partida, mas deve ser validado em hardware real. O perfil possui render scale 0,8, MSAA 1x, sombras adicionais desligadas e SRP Batcher ativo. A luz principal usa sombras e há múltiplas luzes pontuais criadas em runtime; o custo deve ser medido em GPU, não inferido apenas pelo tamanho da cena.

| Métrica | Critério inicial |
|---|---:|
| FPS em aparelho intermediário | 60 FPS alvo; mínimo aceitável de 30 FPS sustentados |
| Picos de frame time | Sem picos recorrentes acima de 33 ms em aparelho de entrada |
| GC durante partida | Zero alocações recorrentes no loop normal |
| Tempo de abertura | Primeira cena interativa em até 4 segundos em aparelho de entrada |
| Memória | Sem crescimento contínuo durante 10 minutos de partidas |
| Temperatura | Sem degradação severa de desempenho após 10 minutos |
| Tamanho de instalação | Medir e estabelecer orçamento antes do conteúdo final |

O profiling deve usar Unity Profiler, Memory Profiler e testes em pelo menos três classes de Android. Toda otimização deve registrar antes/depois. Não reduzir qualidade visual indiscriminadamente: o gabinete e os bichinhos são o produto e precisam continuar legíveis.

## 13. UI, acessibilidade e operação

Substituir `UnityEngine.UI.Text` por TextMeshPro e trocar a construção integral da UI em código por prefabs editáveis. Manter a lógica em código, mas permitir ajuste visual no editor. Garantir contraste suficiente, escala de texto, feedback visual além de cor, vibração opcional, controle de volume, opção de reduzir efeitos e suporte a pausa.

A UI deve exibir claramente: créditos, tempo restante, objetivo, estado da garra, prêmio capturado, resultado da tentativa e botão de continuar. O botão de ação deve ter estados visuais `Ready`, `Closing`, `Holding`, `Releasing` e `Disabled`.

O jogo precisa tolerar interrupções: perda de foco, rotação bloqueada, retorno do background, chamada telefônica e falta de memória. A sessão deve ser salva em pontos seguros, nunca no meio de uma transação de compra sem idempotência.

## 14. Persistência, analytics e monetização futura

A primeira implementação pode usar JSON local versionado. O save deve conter versão do schema, créditos, coleção, moedas, configurações e progresso de missões. Nunca salvar referências de `GameObject` ou estado físico bruto da cena.

Analytics recomendados para o MVP: abertura de sessão, início de partida, posição final da garra, tentativa de captura, captura, queda, entrega, duração da sessão, abandono, dispositivo, FPS médio e erro de carregamento. Os eventos devem ser anônimos e compatíveis com consentimento e política de privacidade.

A monetização deve ser construída depois de o loop ser divertido. A opção mais segura para o conceito é monetização leve: anúncio recompensado para uma ficha extra, compra para remover anúncios e cosméticos. Evitar vender diretamente uma chance obscura de captura sem comunicar probabilidade e resultado. A economia precisa ser simulada em planilha ou script antes de ser implementada.

## 15. Backlog priorizado

| ID | Prioridade | Tarefa | Critério de aceite |
|---|---|---|---|
| T01 | P0 | Implementar `GameSession` e máquina de estados | Nenhum comando funciona fora do estado permitido. |
| T02 | P0 | Substituir teclado direto por `InputRouter` | Touch controla a garra em dispositivo Android. |
| T03 | P0 | Criar `Prize` e `PrizeDefinition` | Captura não depende de nome do objeto. |
| T04 | P0 | Criar zona de entrega | Só prêmio na calha incrementa coleção/pontuação. |
| T05 | P0 | Resolver fim de partida | Timer encerra input, solta prêmio e retorna a estado limpo. |
| T06 | P0 | Corrigir orientação e safe area | Layout validado em quatro proporções. |
| T07 | P1 | Migrar UI para TextMeshPro/prefabs | UI editável no editor e legível em mobile. |
| T08 | P1 | Criar ScriptableObjects de configuração | Balanceamento altera valores sem editar lógica. |
| T09 | P1 | Adicionar áudio, vibração e VFX | Cada evento importante possui feedback claro. |
| T10 | P1 | Implementar coleção e raridade | Entrega salva bichinho e atualiza álbum. |
| T11 | P1 | Adicionar tutorial curto | Novo jogador completa uma captura guiada. |
| T12 | P2 | Profiling e otimização | Critérios de frame time e memória atendidos. |
| T13 | P2 | Missões, daily reward e segunda máquina | Retenção sem bloquear o loop principal. |
| T14 | P2 | Save robusto e analytics | Dados persistem após reiniciar e eventos são verificáveis. |

## 16. Plano de execução em sprints

**Sprint 1 — Fundação.** Limpar cena, remover campos antigos, documentar orientação, definir `MachineConfig`, criar `GameSession`, estados e eventos. O resultado é uma partida controlada, ainda sem polish.

**Sprint 2 — Mobile e captura.** Criar `InputRouter`, controles touch, componente `Prize`, máscara de colisão, captura segura, soltura e zona de entrega. O resultado é uma partida completa jogável em aparelho real.

**Sprint 3 — UX e feedback.** Migrar para TextMeshPro, criar prefabs de UI, áudio, vibração opcional, VFX, tutorial e tratamento de pausa. O resultado é uma experiência compreensível e emocionalmente legível.

**Sprint 4 — Coleção.** Criar catálogo de bichinhos, raridades, álbum, save local, recompensa por entrega e máquina temática inicial. O resultado é um loop com motivo de retorno.

**Sprint 5 — Qualidade mobile.** Profiling em aparelhos, otimização de render, física e carregamento, testes de regressão e build de distribuição interna.

## 17. Prompt operacional para outro agente

Copie o texto abaixo integralmente para o agente responsável pela execução.

```text
Você é um engenheiro sênior de Unity especializado em jogos mobile casuais e sistemas de interação física. Trabalhe no projeto Unity ClawMachine já existente. Não reescreva o projeto do zero e não altere assets originais sem necessidade. Antes de editar, inspecione a estrutura, a cena ativa, os scripts, o manifest de pacotes, as configurações de qualidade e o pipeline mobile.

OBJETIVO
Transformar o protótipo em uma vertical slice mobile jogável de uma máquina de garra que captura bichinhos de pelúcia virtuais. A vertical slice deve permitir iniciar partida, controlar a garra por touch, capturar ou falhar, retornar, entregar o prêmio na calha, registrar a recompensa, terminar por tempo e reiniciar corretamente.

RESTRIÇÕES
1. Preserve Unity 6 e URP.
2. Não dependa de teclado para o gameplay final; teclado pode existir apenas como fallback de editor.
3. Não use nomes de GameObject como regra de negócio.
4. Não use FindFirstObjectByType em operações frequentes.
5. Não deixe lógica de créditos e timer dentro de código de apresentação da UI.
6. Não adicione dependências externas sem justificar.
7. Não introduza monetização nesta etapa.
8. Não produza apenas código: valide a cena, compile scripts e execute a vertical slice.
9. Mantenha alterações pequenas e reversíveis, com commits ou checkpoints por etapa.
10. Sempre que uma decisão de design não estiver definida, escolha a opção mais simples, determinística e adequada a mobile, documentando a decisão.

ETAPA 1 — DIAGNÓSTICO
Leia os arquivos relevantes e produza um diagnóstico curto antes de implementar. Verifique especificamente:
- Assets/Scripts/ClawController.cs
- Assets/Scripts/UIManager.cs
- Assets/Scenes/SampleScene.unity
- Assets/InputSystem_Actions.inputactions
- Assets/Settings/Mobile_RPAsset.asset
- ProjectSettings/ProjectSettings.asset
- ProjectSettings/QualitySettings.asset

Identifique referências quebradas, campos serializados antigos, objetos residuais, orientação conflitante, input não utilizado e riscos de física. Não ignore erros de compilação.

ETAPA 2 — ARQUITETURA
Crie componentes separados:
- GameSession
- ClawMachineController
- ClawMovement
- ClawGrip
- Prize
- PrizeDefinition, preferencialmente como ScriptableObject
- PrizeSpawner
- PrizeDeliveryZone
- InputRouter
- UIController
- AudioFeedbackController, mesmo que inicialmente tenha stubs
- SaveService ou interface equivalente

Use eventos C# ou UnityEvents bem definidos. O domínio deve ser testável sem depender da hierarquia visual inteira.

ETAPA 3 — ESTADOS
Implemente os estados Idle, Playing, Capturing, Returning, Delivering e GameOver. Bloqueie movimentos e ações fora do estado adequado. Ao expirar o timer:
- bloqueie input;
- interrompa ou finalize a operação da garra com segurança;
- solte qualquer prêmio anexado;
- não incremente a pontuação automaticamente;
- retorne para GameOver;
- permita reiniciar sem duplicar objetos ou listeners.

ETAPA 4 — INPUT MOBILE
Crie ações específicas para gameplay: Move, VerticalMove e Grip. Use Input System com bindings touch e fallback de teclado no editor. Se usar joystick virtual, forneça uma implementação clara e configurável. O botão da garra deve emitir um evento de borda, não ficar repetindo a cada frame. Respeite safe area e bloqueie o input quando a UI estiver sobreposta.

ETAPA 5 — CAPTURA
Crie o componente Prize com Rigidbody, definição, estado e métodos Attach, Detach, MarkDelivered e MarkDropped. Use LayerMask configurável e GetComponentInParent<Prize>(). Não use h.name.Contains. Garanta que apenas um prêmio possa ser anexado. Use um ponto de ancoragem explícito na garra. A captura deve ser determinística quando uma seed de teste estiver ativa.

ETAPA 6 — ENTREGA
Adicione um trigger na calha e um PrizeDeliveryZone. O contador de prêmios e a coleção só devem ser atualizados após OnPrizeDelivered. Diferencie Delivered, Dropped, Missed e Aborted. Crie feedback visual e sonoro diferente para cada resultado.

ETAPA 7 — UI
Migre textos para TextMeshPro se o pacote estiver disponível. Prefira prefabs e referências serializadas. Exiba créditos, timer, estado da garra, mensagem de captura, falha, entrega e botão de reinício. Não use emojis como única forma de comunicação. Garanta que o jogo funcione em pelo menos quatro proporções de tela e na orientação escolhida para o produto.

ETAPA 8 — COLEÇÃO MÍNIMA
Implemente um catálogo inicial de pelo menos seis bichinhos. Cada bichinho deve ter nome, raridade, ícone ou representação visual, valor e estado descoberto. Ao entregar, salve a descoberta e mostre uma tela curta de recompensa. Não implemente gacha ou monetização ainda; use distribuição determinística de teste.

ETAPA 9 — SAVE
Implemente save local versionado contendo créditos, bichinhos descobertos, configurações e versão do schema. O save deve ser tolerante a arquivo ausente ou corrompido. Não salve referências de GameObject. Teste fechar e reabrir o aplicativo.

ETAPA 10 — PERFORMANCE
Remova geração redundante de objetos em cada reinício. Reutilize materiais. Evite alocações no Update e no loop normal. Meça frame time, GC e memória em aparelho Android real. Mantenha o render scale mobile apenas se a nitidez permanecer aceitável. Reduza luzes, sombras e pós-processamento somente com evidência de profiling.

ETAPA 11 — TESTES
Crie ou execute testes para:
- não iniciar partida sem crédito;
- consumir exatamente um crédito por partida;
- não mover em Idle ou GameOver;
- não capturar dois prêmios;
- não contar captura sem entrega;
- entregar apenas dentro da calha;
- encerrar corretamente no timer;
- reiniciar sem duplicar UI, listeners ou prêmios;
- carregar save válido;
- recuperar de save corrompido;
- operar com collider no objeto filho do prêmio.

ETAPA 12 — VERIFICAÇÃO FINAL
Compile o projeto sem erros. Execute uma partida completa no editor e em Android. Verifique orientação, safe area, legibilidade, controles, som, vibração opcional, pausa e retorno do background. Faça uma lista de problemas restantes classificados como P0, P1 e P2. Não declare concluído se houver input mobile quebrado, erro de compilação, contador incorreto ou estado preso.

ENTREGÁVEIS
1. Código implementado e organizado.
2. Cena limpa e funcional.
3. Prefabs ou ScriptableObjects necessários.
4. Lista de arquivos alterados.
5. Instruções de teste manual.
6. Resultados de profiling disponíveis.
7. Relatório final com limitações conhecidas e próximos passos.
```

## 18. Critérios de aceite da vertical slice

A entrega só deve ser considerada pronta quando um usuário novo conseguir iniciar e concluir uma partida sem instrução verbal. O jogo deve funcionar sem teclado em Android, não pode contabilizar um prêmio antes da entrega, não pode aceitar input após o fim da partida e deve reiniciar sem duplicar UI, prêmios ou listeners.

A cena precisa abrir sem erros de console. O jogo deve apresentar feedback claro para captura, falha, queda e entrega. O save deve sobreviver ao fechamento do aplicativo. O frame time deve ser medido em aparelho real e não apresentar degradação progressiva durante dez minutos de uso.

## 19. Conclusão

O melhor caminho é posicionar o projeto como um **jogo de coleção casual baseado em habilidade**, e não apenas como uma simulação de máquina. A referência de mercado comprova que a combinação “garra + bichinhos + coleção” já possui demanda, especialmente em Clawbert e Claw Crane Little Pets. O espaço para diferenciação está em oferecer controle mais responsivo, resultado mais justo, sessões curtas, coleção visualmente recompensadora e uma economia menos agressiva.

A decisão técnica central é simples: primeiro estabilizar o ato de jogar; depois aumentar o conteúdo. Com input mobile, estados, captura/entrega corretas, UI editável e uma coleção mínima, o protótipo passa a ser uma vertical slice real. A partir dela será possível fazer playtests, medir retenção e decidir com segurança se vale investir em mais máquinas, personagens, eventos e monetização.

## Referências

[1]: https://play.google.com/store/apps/details?id=com.Bow3.TheClaw&hl=en_US "Clawbert — Google Play"

[2]: https://apps.apple.com/us/app/clawbert/id1208244349 "Clawbert — App Store"

[3]: https://apps.apple.com/us/app/claw-crane-little-pets/id1098601762 "Claw Crane Little Pets — App Store"

[4]: https://play.google.com/store/apps/details?id=com.gigantic.clawee&hl=en_US "Clawee — Google Play"

[5]: https://apps.apple.com/us/app/clawee-real-claw-machines/id1315539131 "Clawee — App Store"

[6]: https://apps.apple.com/us/app/prize-claw/id434800417 "Prize Claw — App Store"

[7]: https://play.google.com/store/apps/details?id=com.hfl.claw.machine.games.crane.games&hl=en_US "Claw Machine Games Crane Game — Google Play"
