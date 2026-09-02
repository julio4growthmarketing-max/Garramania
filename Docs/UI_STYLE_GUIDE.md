# 🕹️ GarraMania — Design System & UI Style Guide (2026)

Este guia documenta os padrões visuais, tokens de cor, tipografia e diretrizes de interação do **GarraMania**, garantindo coesão estética e técnica no desenvolvimento de novas telas e recursos.

---

## 1. Filosofia Visual: Cyber-Arcade Glassmorphism

O design do GarraMania combina a vibração dos arcades japoneses (luzes de néon, botões com relevo tátil, botões estilo Sanwa Denshi) com o refinamento moderno de interfaces móveis (*glassmorphism*, painéis translúcidos em azul-marinho profundo e tipografia com relevo).

### Pilares de Design:
1. **Profundidade sem Ruído**: Em vez de texturas rasterizadas pesadas que esticam ou pixelam, usamos retângulos arredondados 9-slice vetoriais com chanfros luminosos (*bevel*).
2. **Alto Contraste Mobile**: Todos os textos utilizam contornos escuros (`ColorTextOutline`) e sombras projetadas para garantir legibilidade instantânea sob luz solar direta.
3. **Identidade Tátil**: Todo botão interativo oferece feedback duplo imediato (depressão geométrica de 3px + estalo sonoro + vibração háptica).

---

## 2. Paleta Oficial de Cores (Design Tokens)

Centralizada na classe estática [`UITheme.cs`](../Assets/Scripts/UITheme.cs).

| Token | Hex | RGB (0-1) | Uso Recomendado |
|---|---|---|---|
| `ColorBgDeepNavy` | `#14172E` | `(0.08, 0.09, 0.18, 0.95)` | Fundo de modais, bottom sheets e painéis de menu. |
| `ColorCardDark` | `#1C2140` | `(0.11, 0.13, 0.25, 0.96)` | Superfície de cards individuais e slots do álbum. |
| `ColorCardSlot` | `#262E52` | `(0.15, 0.18, 0.32, 0.90)` | Pedestais de prêmios e slots vazios / bloqueados. |
| `ColorNeonCyan` | `#33E0FF` | `(0.20, 0.88, 1.00, 1.00)` | Bordas cibernéticas, botões secundários, HUD de timer. |
| `ColorNeonGold` | `#FFD91F` | `(1.00, 0.85, 0.12, 1.00)` | Fichas, troféus, títulos comemorativos e destaques VIP. |
| `ColorNeonPink` | `#FF4099` | `(1.00, 0.25, 0.60, 1.00)` | Botões de impacto, itens lendários e celebrações. |
| `ColorNeonPurple` | `#9940FF` | `(0.60, 0.25, 1.00, 1.00)` | Cards de raridade épica e temas de mistério. |
| `ColorNeonGreen` | `#1FEB73` | `(0.12, 0.92, 0.45, 1.00)` | Botões de compra, confirmação positiva e 'JOGAR'. |
| `ColorNeonRed` | `#FF3840` | `(1.00, 0.22, 0.25, 1.00)` | Grande Botão Sanwa (AGARRAR) e ações de alerta. |
| `ColorTextOutline`| `#0A0D1F` | `(0.04, 0.05, 0.12, 0.98)` | Contorno obrigatório para textos sobre fundos luminosos. |

---

## 3. Tipografia Oficial

A tipografia do GarraMania é a **Lilita One**, uma fonte *display* geométrica arredondada com alta personalidade de arcade.

* **Caminho do Asset**: `Assets/Resources/Fonts/LilitaOne-Regular.ttf`
* **Fallback Dinâmico**: `LegacyRuntime.ttf` / `Arial.ttf`

### Escala de Tamanhos Mobile:

| Nível | Tamanho (Mobile Portrait) | Tamanho (Landscape) | Estilo | Sombra / Contorno |
|---|---|---|---|---|
| **Display / Header** | `28 – 32 pt` | `32 – 36 pt` | Bold | Outline 2.2px + Shadow 2.5px |
| **Títulos de Card / Botões** | `18 – 22 pt` | `20 – 24 pt` | Bold | Outline 2.0px + Shadow 2.0px |
| **Labels Secundários** | `14 – 16 pt` | `15 – 17 pt` | Bold | Outline 1.8px + Shadow 1.5px |
| **Texto de Apoio / Dicas** | `12 – 14 pt` | `13 – 15 pt` | Regular | Sem outline pesado |

---

## 4. Anatomia dos Componentes

### 4.1. Grande Botão Sanwa (AGARRAR)
* **Objetivo**: O ponto focal tátil da tela de jogo.
* **Camadas**:
  1. **Base Metálica** (`ActionButton_Ring`): Círculo escuro (`#0C0E1C`) expandido em 12px.
  2. **Chanfro Externo** (`RingBevel`): Círculo com contorno translúcido (`rgba(255,255,255,0.18)`).
  3. **Núcleo Esférico** (`ActionButton_Core`): Círculo vermelho néon (`ColorNeonRed`).
  4. **Reflexo de Cúpula** (`DomeHighlight`): Oval branco suave (`rgba(255,255,255,0.35)`) ancorado na parte superior do domo para simular acrílico brilhante.

### 4.2. Botões Retangulares 3D (`CreateArcadeButton`)
* **Estrutura 9-Slice**: Retângulo arredondado com raio de 9px e bordas de 10px que nunca pixelam.
* **Chanfro de Profundidade** (`BevelBorder`): Moldura interna posicionada como primeiro irmão (*first sibling*) com luminosidade controlada pelo tema.
* **Animação de Pressione** (`ArcadePressEffect`):
  * Pointer Down: `anchoredPosition += (0, -3px)`, `scale = 0.96`.
  * Pointer Up: Retorna à posição e escala originais.

### 4.3. Janelas e Bottom Sheets
* **Fundo**: `ColorBgDeepNavy` com 95% de opacidade, permitindo ver a iluminação 3D da máquina no fundo.
* **Borda**: Filete sutil de 1px com 35% a 45% de opacidade da cor temática (ex: `ColorNeonCyan * 0.40f`).

---

## 5. Diretrizes de Acessibilidade (a11y)

1. **Touch Targets (Área Mínima de Toque)**:
   * Todo elemento clicável deve possuir área mínima de **48x48 dp** (≈ 48px em telas padrão) para evitar toques acidentais em dedos de diferentes tamanhos.
2. **Alto Contraste (`AccessibilityManager.HighContrast`)**:
   * Labels devem respeitar a taxa de contraste mínima de **4.5:1** (WCAG AA).
3. **Controle Tátil e Movimento**:
   * Vibrações podem ser desativadas no menu de configurações através de `AccessibilityManager.Instance.SetHaptics(false)`.
   * Câmeras não devem balançar se `AccessibilityManager.Instance.ReduceMotion` estiver ativo.

---

## 6. Internacionalização (i18n)

Todas as strings visíveis são carregadas a partir de [`Assets/Resources/Localization/strings.json`](../Assets/Resources/Localization/strings.json).

```csharp
// Exemplo de uso correto em qualquer script de UI:
string grabLabel = LocalizationManager.Get("BTN_GRAB");
string progress = LocalizationManager.Format("ALBUM_PROGRESS", count, total, percent);
```

Idiomas suportados atualmente:
* 🇧🇷 `pt-BR` (Português do Brasil - Padrão)
* 🇺🇸 `en-US` (English)
* 🇪🇸 `es-ES` (Español)
