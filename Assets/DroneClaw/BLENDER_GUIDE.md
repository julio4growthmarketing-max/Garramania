# 🎨 Guia do Blender para o Projeto DroneClaw / Garramania

Este guia foi criado para orientar a modelagem, escala, materiais e exportação de bonecos 3D (bichinhos e criaturas) do **Blender** diretamente para a **Unity**, garantindo máxima compatibilidade, escala correta e ótimo desempenho.

---

## 1. ⚙️ Configuração Inicial do Blender

Antes de começar a modelar, ajuste a cena do Blender para falar a mesma língua da Unity:

1. Vá na aba **Scene Properties** (ícone de cone com esfera na lateral direita).
2. Na seção **Units**:
   * **Unit System:** `Metric`
   * **Unit Scale:** `1.000000`
   * **Length:** `Meters`
3. **Regra de Ouro da Unity:** `1 Unidade na Unity = 1 Metro real`.
   * Um bichinho de pelúcia ou criatura fofa deve ter entre **0.3m (30 cm)** e **0.6m (60 cm)** de altura.
   * O Drone deve ter aproximadamente **0.8m a 1.2m** de envergadura.

---

## 2. 📍 Ponto de Origem / Pivô (Pivot Point)

O ponto de origem (ponto amarelo no Blender) define onde o objeto gira e como ele se apoia no chão:

* **Para Bichinhos e Criaturas:**
  * Coloque a base dos pés **exatamente no chão (Z = 0)**.
  * O ponto de origem deve ficar entre as duas patinhas/pés na altura do chão:
    * *No Blender:* No modo Object, selecione o modelo -> `Object` -> `Set Origin` -> `Origin to 3D Cursor` (com o cursor em 0,0,0) ou `Origin to Bottom`.
  * Isso evita que o bichinho "afunde" no chão quando o colocarmos na Unity.
* **Para o Drone:**
  * O ponto de origem deve ficar exatamente no **centro de gravidade** do drone (centro do corpo).
* **Para a Garra:**
  * O ponto de origem deve ficar no **topo** onde o cabo se conecta.

---

## 3. 🧸 Dicas de Estilo para Bichinhos (Chibi / Fofinhos)

* **Proporção da Cabeça:** Cabeça grande (40% a 50% da altura total) e olhos grandes e expressivos dão o charme clássico dos jogos japoneses e de colecionáveis (estilo Pop Mart / Funko / Animal Crossing).
* **Bordas Suaves (Bevel):** Brinquedos reais não têm cantos afiados como navalha. Use o modificador `Bevel` com pouca largura para dar aquele brilho especular nas quinas.
* **Shading:** No modo de objeto, clique com o botão direito no modelo e selecione **Shade Auto Smooth** (ou `Shade Smooth`) para que o modelo pareça macio e orgânico.
* **Contagem de Polígonos (Polycount):**
  * Entre **800 e 3.000 triângulos** por bichinho é o número perfeito (leve para mobile/WebGL e detalhado o suficiente para ficar lindo de perto).

---

## 4. 🎨 Texturas e Cores (A Abordagem Mais Fácil e Leve)

A forma mais rápida, estilosa e performática de texturizar modelos estilo *low-poly* é usando uma **Texture Palette (Paleta de Cores)**:
1. Crie uma imagem PNG pequena (ex: 256x256 ou 512x512) contendo quadrados com suas cores favoritas (tons pastel, cores vibrantes, metálicos).
2. No Blender, adicione um material com essa textura como `Base Color`.
3. Abra o **UV Editor**, selecione as faces do modelo e simplesmente encolha os vértices UV (`S -> 0`) e posicione-os sobre a cor desejada na paleta.
4. **Vantagem:** Todos os seus bichinhos podem compartilhar a mesma paleta de cores, economizando memória e deixando o jogo super leve!

---

## 5. 🚀 Exportando para a Unity

### Opção A: Copiar o arquivo `.blend` direto (Super prático!)
A Unity lê arquivos `.blend` nativamente se você tiver o Blender instalado no mesmo computador.
* Basta salvar seu arquivo `.blend` dentro da pasta:
  `Assets/DroneClaw/Models/SeuBichinho.blend`
* A Unity vai importar automaticamente!

### Opção B: Exportar como `.FBX` (Recomendado para produção final)
1. Selecione o modelo que deseja exportar.
2. Vá em `File` -> `Export` -> `FBX (.fbx)`.
3. Marque as opções recomendadas na barra lateral:
   * **Include:** Marque `Selected Objects` e tipo `Mesh`.
   * **Transform:**
     * `Scale`: `1.0`
     * `Apply Scalings`: `FBX All`
     * `Forward`: `-Z Forward`
     * `Up`: `Y Up`
     * Marque `Apply Transform`!
4. Salve na pasta `Assets/DroneClaw/Models/`.

---

## 📦 Estrutura de Nomes Sugerida
Para mantermos a organização dos seus modelos:
* `Bichinho_Raposa.fbx`
* `Bichinho_Coelho.fbx`
* `Bichinho_Panda.fbx`
* `Drone_Scout.fbx`
* `Garra_Tripla.fbx`
