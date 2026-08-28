# Revisão de controles e estabilidade — GarraMania

## Diagnóstico

A versão executável havia voltado aos controles antigos porque `ClawController.Start()` procurava e destruía o objeto `ProfessionalUIController`, além de recriar `UIManager` quando necessário. A cena também continha somente o `UIManager` legado ativo. `GameSession` reforçava o comportamento antigo ao auto-instanciar `UIManager` e iniciar a partida automaticamente em `Start()`.

## Correções aplicadas

- Removida a destruição da UI moderna no `ClawController`.
- Removida a criação de `UIManager` pelo `ClawController`.
- `GameSession` agora garante `ProfessionalUIController`, não `UIManager`.
- A partida não inicia automaticamente; começa pelo menu.
- `SampleScene` agora mantém o `UIManager` legado desativado e `ProfessionalUIController` ativo.
- Restaurados `SafeAreaFitter`, `VirtualJoystickView` e `HoldInputButton`.
- A `ProfessionalUIController` foi reescrita com uGUI nativo, evitando bloqueio por TMP Essentials.
- O layout moderno possui joystick X/Z, subir, descer, ação, câmera, menu, HUD, resultado e game over.
- O botão usa semântica coerente: `FECHAR GARRA` quando aberta e `SOLTAR` quando fechada.
- O ciclo da garra publica `Capturing`, `Returning` e `Delivering` quando aplicável.
- Buscas de `Prize` no reset e na soltura aceitam componentes em objetos pais.
- A sessão não repõe fichas automaticamente quando o saldo chega a zero.

## Estado esperado da cena

```text
GarraManiaUI
  UIManager (disabled)
  ProfessionalUIController (enabled)

EventSystem
InputRouter
BaseGarra / ClawController
GameSession
```

## Teste manual obrigatório

1. Pare o Play Mode, se estiver ativo.
2. Feche qualquer painel modal do Unity e aceite a recompilação.
3. Se o Unity informar que `SampleScene` foi alterada no disco, escolha recarregar a cena.
4. Aguarde o fim da compilação e confirme que o Console não possui erros vermelhos.
5. Pressione Play.
6. O menu deve aparecer e a partida não deve começar sozinha.
7. Pressione `INICIAR JOGADA`.
8. O joystick deve mover X/Z, os botões devem subir/descer e o botão principal deve iniciar o ciclo automático de captura.
9. O botão de câmera deve alternar a visão.
10. Ao entregar prêmio, o contador deve aumentar uma vez e a tela de resultado deve aparecer.
11. Ao terminar o tempo, os controles devem ser bloqueados e a tela de fim deve aparecer.
12. Ao reiniciar, deve existir apenas um Canvas e uma UI.

## Limitação de validação

A revisão foi estática e a cena foi atualizada no arquivo YAML. A compilação automática não pôde ser confirmada de forma independente porque havia uma instância do Unity aberta na mesma pasta e o ambiente não possui SDK .NET adequado para substituir a compilação do Unity. A validação final deve ser feita no Unity Editor e, depois, em Android real.

## Próximo trabalho

Depois que o fluxo acima passar, o próximo passo é estabilizar o balanceamento do ciclo automático e substituir a criação runtime da UI por prefabs editáveis. Não adicionar novas máquinas ou monetização antes desse teste manual.
