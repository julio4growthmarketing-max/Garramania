# Sistema de Estoque Vivo — GarraMania

## O que foi implementado

Implementei um sistema de **estoque físico e balanceamento de raridade** inspirado no modelo de coleção do Clawbert. Agora, a máquina não apenas sorteia bichinhos aleatórios; ela gerencia uma reserva real que influencia a presença física no monte e a dificuldade de captura.

### 1. Distribuição de Bichinhos (Monte Inicial)

A máquina começa com **36 bichinhos** distribuídos da seguinte forma:
- **24 Comuns:** Fox, GreenBear, BalloonFish (grande volume, sustentam a diversão).
- **9 Incomuns:** Koala, Badger (variedade e descoberta).
- **3 Raros:** Porky (momento especial, desejo do jogador).

### 2. Mecânica de Estoque e Reposição

- **Reserva Limitada:** Cada tipo de bichinho tem uma capacidade máxima na reserva (ex: 100 comuns, 50 incomuns, 10 raros).
- **Consumo Real:** Capturar e entregar um prêmio remove permanentemente uma unidade da reserva.
- **Reposição Temporal (Offline):** A cada 6 horas fora do jogo, a máquina reabastece parte da reserva (ex: +12 comuns, +4 incomuns, +1 raro).
- **Reposição por Atividade (Online):** A cada 90 segundos de jogo, se houver espaço no monte, a máquina tenta instanciar novos bichinhos da reserva.

### 3. Balanceamento e Pity System (Proteção de Chance)

- **Dificuldade Física:** Bichinhos raros são mais pesados e possuem colliders que dificultam o encaixe perfeito da garra.
- **Resistência à Captura:** Mesmo que a garra feche sobre um raro, ele tem uma chance base de "escapar" (escorregar).
- **Pity System:** Cada tentativa de captura que não resulta em um prêmio raro aumenta a chance de um raro aparecer na próxima reposição e a chance de ele não escorregar da garra. Ao capturar um raro, o bônus reseta.

## Arquivos adicionados/alterados

- `Assets/Scripts/PrizeStockManager.cs`: Gerenciador central de estoque, persistência e pity system.
- `Assets/Scripts/Prize.cs`: Atualizado para carregar dados de raridade e física dinâmica.
- `Assets/Scripts/ClawController.cs`: Integrado ao estoque para construção do monte e validação de captura.

## Como testar no Unity

1. Abra `SampleScene` e pressione Play.
2. Observe o Console: ele indicará a distribuição inicial do monte.
3. Tente capturar bichinhos diferentes.
4. Note que os comuns (Fox, GreenBear) são mais fáceis de segurar.
5. Se encontrar um Porky (Raro), note que ele pode escorregar se você não tiver "pity" acumulado.
6. Verifique que, após algumas capturas, novos bichinhos surgem no monte (reposição online).
7. Reinicie o jogo: o estoque disponível e o seu progresso de "pity" são persistidos via PlayerPrefs.

## Próximo passo recomendado

Agora que o estoque e a raridade estão funcionando, o próximo passo é criar o **Álbum de Coleção** na UI para que o jogador possa ver quais bichinhos já capturou e quais ainda faltam para completar a máquina.
