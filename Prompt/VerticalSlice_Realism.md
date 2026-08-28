# Vertical Slice de Realismo — GarraMania

## Objetivo

Construir uma única máquina de garra mobile-first que pareça uma máquina de shopping e cuja captura dependa do contato físico entre garra e pelúcia. A referência de qualidade mecânica é o princípio de simulação de Claw Machine Sim, sem copiar arte, nomes, modelos ou interface.

## Arquitetura atual

`ClawController` controla movimento, ciclo da garra, cabo e captura. `PrizePileSpawner` controla abastecimento, wrappers físicos, colliders, materiais físicos, queda, settle e reposição. `Prize` controla estado de gameplay e transições entre monte, garra, entrega e queda. `PrizeStockManager` controla raridade, reservas, pity e contagem ativa. O rig Blender deve ser apenas filho visual do wrapper.

## Contrato de um prêmio

Cada prêmio criado pelo spawner possui um root wrapper com `Prize`, `Rigidbody` e `BoxCollider` ajustado a partir dos bounds visuais. Rigidbodies, colliders, Animator e Prize internos do rig são removidos/desativados. A massa fica no centro do collider, e o material físico usa atrito alto e quique mínimo para comportamento de pelúcia.

## Contrato do monte

O spawner cria 72 prêmios em lotes, acima do topo real do platô, com posições espalhadas por volume e rotações variadas. Eles caem sobre um piso físico invisível e sobre os outros prêmios. O settle não reposiciona o monte em uma grade: preserva as posições encontradas pela simulação, corrige apenas travessia do piso e mantém os corpos dinâmicos adormecidos para que possam reagir quando a garra remover um prêmio.

## Contrato de captura

A garra procura o prêmio mais próximo do centro físico abaixo dela, em uma esfera de contato restrita. Não deve capturar qualquer prêmio apenas por estar dentro de um raio amplo. O candidato precisa estar em `InPile`, ter corpo físico e estar próximo horizontal e verticalmente do ponto de contato. Ao capturar, `Prize.Attach` torna o wrapper cinemático e prende-o à garra; ao soltar, `Prize.Detach` libera gravidade e remove o parent.

## Critérios de aprovação

1. Ao abrir a cena, os prêmios caem e formam um volume irregular apoiado no platô.
2. Nenhum rig visual fica separado do próprio collider ou do contato aparente com o monte.
3. A remoção de um prêmio deixa os vizinhos capazes de acordar e preencher parcialmente o espaço.
4. A garra só captura prêmios próximos do seu centro de contato.
5. A entrega marca o prêmio corretamente e reduz a contagem ativa sem duplicar reserva.
6. Reposição adiciona unidades somente quando há espaço no monte.
7. Não existem erros de compilação ou warnings recorrentes no Console.
8. Depois da aprovação visual no Editor, o sistema deve ser perfilado em Android intermediário antes de escalar conteúdo.

## Nota de validação

O Editor Unity estava aberto no computador conectado durante a implementação. A compilação e o runtime após a última edição não devem ser considerados comprovados até executar Play Mode e verificar Game View/Console. O teste pedido ao usuário é parar Play, aguardar recompilação, reabrir `SampleScene`, pressionar Play e observar a queda e o settle.
