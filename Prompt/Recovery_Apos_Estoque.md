# Recuperação após quebra do sistema de estoque

## Sintoma observado

Após a integração do estoque vivo, a cena entrou em jogo com apenas um bichinho visível e um indicador de tempo visualmente incorreto. O screenshot também mostrou que o runtime estava usando a UI moderna, portanto o problema não era o retorno ao `UIManager` legado.

## Causa provável

O spawner passou a depender de estado persistido em `PlayerPrefs`. Dados gerados durante uma execução parcial podem deixar reservas zeradas ou inconsistentes. O primeiro sistema também usava um preenchimento que não garantia um conjunto mínimo quando o estoque persistido estava esgotado.

## Correções

- Adicionada versão de dados `CurrentStockVersion = 2`.
- Dados antigos de estoque são invalidados automaticamente na primeira inicialização da nova versão.
- Se todas as reservas estiverem zeradas, o sistema restaura as capacidades padrão antes de montar a cena.
- Adicionado fallback que instancia um prefab conhecido mesmo se a reserva persistida estiver inconsistente.
- O indicador radial de tempo foi substituído por uma barra horizontal sem dependência de sprite externo.
- O timer não cria mais um quadrado vazio por falta de textura.
- O manager de estoque foi colocado como objeto editável na `SampleScene`.
- Foi criada uma cópia de segurança em `Prompt/RecoveryBeforeStockFix/` antes dos ajustes.

## Distribuição atual

```text
24 comuns: Fox, GreenBear, BalloonFish
9 incomuns: Koala, Badger
3 raros: Porky
36 posições totais no monte
```

## Teste obrigatório

1. Pare o Play Mode.
2. Salve a cena, se o Unity solicitar.
3. Aguarde a recompilação.
4. Feche e reabra `SampleScene` se houver aviso de alteração externa.
5. Pressione Play.
6. Confira no Hierarchy se aparece `Monte_De_Ursos` com dezenas de filhos `Pelucia_...`.
7. Confira o Console pela mensagem `[ClawController] Monte distribuído pelo estoque vivo: 36/36 posições.`.
8. Verifique que a barra de tempo é horizontal e que há vários bichinhos no monte.
9. Inicie uma jogada e confirme que o contador de fichas diminui uma vez.
10. Entregue um prêmio e confirme que o contador de prêmios aumenta uma vez.

## Limitação

A execução final dentro do Unity não foi confirmada automaticamente porque o Editor estava aberto no computador do usuário. A correção está aplicada aos arquivos e o estado persistido antigo foi invalidado por versão, mas a confirmação definitiva requer um novo Play Mode no editor.

## Correção adicional de visibilidade

Como o primeiro fallback ainda permitia que os rigs desaparecessem pelo comportamento físico, o spawn inicial foi alterado novamente:

- posição inicial em grade determinística de seis colunas, duas linhas e três camadas;
- alturas de repouso acima do platô, sem penetração inicial no piso;
- todos os rigidbodies ficam cinemáticos durante a montagem;
- a física é liberada em conjunto após 0,75 s;
- o primeiro monte não depende de sorteio nem de estado persistido;
- logs informam prefab, nome instanciado e contagem final.

A última validação deve confirmar `Spawn inicial concluído. Filhos no monte: 36` e `Física do monte liberada: 36 rigidbodies`.

## Causa raiz do primeiro spawn

Os prefabs dos bichinhos são criados dentro de um wrapper runtime. O componente `Prize` possuía `[RequireComponent(typeof(Rigidbody), typeof(Collider))]`; como `Collider` é uma classe base abstrata, adicionar `Prize` ao wrapper podia interromper o método de spawn depois do primeiro prefab. A exigência foi reduzida para `Rigidbody`, e o `BoxCollider` concreto agora é criado explicitamente pelo spawner no wrapper. Isso permite que cada um dos 36 prêmios seja instanciado sem depender de um collider abstrato.
