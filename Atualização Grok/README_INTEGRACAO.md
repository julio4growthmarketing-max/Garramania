# Atualização — Física Realista de Captura + Escorregamento (GarraMania)

## Arquivos nesta pasta

- `Assets/Scripts/Prize.cs` → **substitua completamente** o arquivo atual
- `Assets/Scripts/ClawController_RealisticCapture.cs` → contém os métodos novos (referência)

---

## Passo a passo de integração no ClawController.cs

### 1. Adicione estes campos na classe (perto dos outros Headers)

```csharp
[Header("Captura Realista")]
[SerializeField] private float captureRadius = 0.55f;
[SerializeField] private float maxHorizontalAlignDistance = 0.48f;
[SerializeField] private float idealVerticalOffset = -0.38f;
[SerializeField] private float verticalTolerance = 0.35f;
[SerializeField] private LayerMask prizeLayer;

[Header("Grip")]
[SerializeField, Range(0.3f, 1.2f)] private float baseGripForce = 0.85f;
[SerializeField] private float gripLossPerSecondWhileMoving = 0.18f;
[SerializeField] private float gripLossPerSwayDegree = 0.012f;

private float currentGripForce;
private Prize currentHeldPrize;
private float slipTimer;
private bool isSlipping;
```

### 2. Substitua o método FecharGarraFisica()

Troque o conteúdo atual por:

```csharp
private void FecharGarraFisica()
{
    isClosed = true;
    if (dentes != null)
    {
        foreach (var d in dentes) d.localRotation = Quaternion.Euler(0, d.localEulerAngles.y, 10f);
    }

    TryGrabRealistic();   // ← nova lógica
}
```

### 3. Cole os métodos novos

Copie do arquivo `ClawController_RealisticCapture.cs` os métodos:
- `EvaluateBestCandidate()`
- `TryGrabRealistic()`
- `UpdateHeldPrizePhysics()`
- `ReleasePrizeWithPhysics()`
- e o struct `CaptureEvaluation`

### 4. Dentro da coroutine RotinaCicloFliperama

Nas fases de **subida** e **viagem até a calha**, adicione a chamada:

```csharp
UpdateHeldPrizePhysics();
AtualizarCabo();
```

Exemplo (fase de subida):

```csharp
while (transform.position.y < LIM_YMAX - 0.04f)
{
    transform.position = Vector3.MoveTowards(...);
    UpdateHeldPrizePhysics();   // ← adicionar
    AtualizarCabo();
    yield return null;
}
```

Faça o mesmo no while da viagem até a calha.

### 5. Ajuste o AbrirGarraFisica

```csharp
private void AbrirGarraFisica()
{
    isClosed = false;
    if (dentes != null)
    {
        foreach (var d in dentes) d.localRotation = Quaternion.Euler(0, d.localEulerAngles.y, 45f);
    }

    SetTrailColorDefault();

    if (currentHeldPrize != null)
    {
        // Entrega normal (chegou na calha)
        currentHeldPrize.MarkDelivered();
        if (GameSession.Instance != null)
            GameSession.Instance.RegisterPrizeDelivered(currentHeldPrize);

        currentHeldPrize = null;
        premioAgarrado = null;
    }
    else if (premioAgarrado != null)
    {
        // fallback antigo
        Prize p = premioAgarrado.GetComponentInParent<Prize>();
        if (p != null) p.Detach();
        premioAgarrado = null;
    }
}
```

### 6. Configuração no Unity

1. Crie uma **Layer** chamada `Prize`
2. Atribua essa layer em todos os prefabs de prêmio
3. No Inspector do ClawController, configure o campo `prizeLayer` para a layer Prize
4. Ajuste os valores de grip se necessário (comece com os defaults)

---

## Resultado esperado

- Captura deixa de ser binária (agora tem qualidade 0-1)
- Prêmios raros são fisicamente mais difíceis
- Escorregamento progressivo com feedback (haptic + câmera + som)
- Sensação de peso e esforço muito maior
- Compatibilidade mantida com o ciclo atual da garra

---

## Observação sobre GameJuice e Câmera

Os métodos `HapticsMedium()`, `HapticsSlip()`, `HapticsHeavy()` e `PunchFOV()` precisam existir.
Se ainda não existirem no seu `GameJuice` e `ClawCameraController`, use os fallbacks que já estão no código (HapticsLight / ScreenShake).
