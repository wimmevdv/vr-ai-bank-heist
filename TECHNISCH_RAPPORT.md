# Technisch rapport — training van een ML-Agents bewaker in een VR-bankoverval

## Inleiding

Dit rapport documenteert het ontwerp, de training en de evaluatie van
een single-agent bewaker in het VR-stealth-spel *Heist and Seek*. De
agent wordt aangedreven door de Unity ML-Agents Toolkit (PPO + LSTM) en
moet binnen vijf minuten een menselijke speler vangen die kunst-,
contant- en kluisvoorwerpen probeert te stelen.

Het doel van dit rapport is drieledig: de gemaakte ontwerpkeuzes
expliciet vastleggen, de waargenomen prestaties van de getrainde policy
beschrijven, en de zekerheid waarmee uitspraken over die prestaties
gedaan kunnen worden voorzichtig afbakenen. Het rapport is bedoeld voor
de begeleidende docenten van het vak VR Experience en voor toekomstige
teamleden die op deze code willen voortbouwen.

## Materialen en methoden

### Hardware en omgeving

De training draaide op een Windows-werkstation met PyTorch 2.12.0
(CPU-build) binnen de conda-omgeving `mlagents-bank`. De Unity-Editor
versie is 6000.3.9f1 met Universal Render Pipeline 17.3.0, AI Navigation
2.0.12 en XR Interaction Toolkit 3.4.1. De VR-build is getest op de
Meta Quest 3S via OpenXR 1.16.1 met de Meta-OpenXR plugin 2.5.0. De
ML-Agents-toolkit is geïnstalleerd in twee componenten: het
Unity-package `com.unity.ml-agents` 4.0.3 en de Python-zijde
`mlagents` 1.1.0 (Unity Technologies, 2024).

### Trainingsomgeving (HeistEnvController)

De trainingsscène (`kean_scene_Training2.unity`) bevat de werkelijke
bank-geometrie inclusief NavMesh, deposit-clusters en drop-zone. Per
episode (maximaal 3 000 stappen of 60 seconden gesimuleerd) gebeurt het
volgende:

1. De bewaker spawnt op `guardSpawn` met een willekeurige
   beginrotatie tussen 0° en 360°.
2. Maximaal zes deposits worden geactiveerd. Wanneer
   `randomizeDepositPositions` aanstaat, krijgt elke actieve deposit een
   willekeurige NavMesh-positie binnen 80 × 80 m.
3. Eén willekeurige deposit wordt "gealarmeerd" (8-15 s), met een
   luid noise-event (loudness 1).
4. Een `ScriptedThief` spawnt op een willekeurig NavMesh-punt en
   doorloopt een state-machine: kies-deposit → grijp → breng naar
   drop-zone → herhaal. Tijdens beweging produceert hij voetstap-noise.
5. De episode eindigt bij timer-op (TimeUp), alle deposits gestolen
   (AllStolen) of vangst van de speler (Caught).

Bij interactie met een menselijke VR-speler wordt `MaxStep` op 0 gezet
(geen step-limiet) en vervangt `VRPlayerBridge` de `ScriptedThief`. De
randomisatie van deposits blijft actief.

### Twee primaire componenten

#### Behavior Parameters

Het `BehaviorParameters`-component op de bewaker bepaalt het contract
tussen Unity en de ML-Agents-trainer. De finale configuratie staat in
tabel 1.

**Tabel 1.** Configuratie van `BehaviorParameters` op
`BankGuardAgent`.

| Veld | Waarde |
|---|---|
| Behavior Name | `BankGuard` |
| Vector Observation Size | 17 |
| Stacked Vectors | 1 |
| Continuous Actions | 2 |
| Discrete Branches | 0 |
| Behavior Type | Default (Inference Only in `MAIN_SCENE2`) |
| Inference Device | CPU |
| Use Child Sensors | true |
| Use Child Actuators | true |
| Model | `BankGuard-3003449.onnx` (na export) |

De child-sensor `RayPerceptionSensorComponent3D` voegt automatisch
ray-observaties toe (13 rays per direction, max-degrees 120,
`sphereCastRadius` 0,3, `rayLength` 20 m, detectabele tags `Wall`,
`Player`, `Deposit`). Het totale observatie-budget bedraagt daarmee 17
vector- en 65 ray-features per stap.

#### Agent (BankGuardAgent)

De `BankGuardAgent`-klasse erft van `Unity.MLAgents.Agent` en
implementeert het beloningsprofiel, de bewegings-cinematica en de
detectie-logica. Beweging vindt plaats in `FixedUpdate` op de
`Rigidbody`, met smoothing van de actie-input en een
hellings-projectie tegen het NavMesh om verticale stuiterbewegingen te
voorkomen. De patrouille-snelheid bedraagt 3,5 m/s; bij zichtcontact
schakelt de agent naar 5,5 m/s (`chaseSpeed`).

### Override-methods

De volgende override-methods van `Agent` worden geïmplementeerd:

- `Initialize()` — haalt het `Rigidbody`-component op, freezet rotatie
  op X- en Z-as om kantelen te voorkomen, en zet `MaxStep` op
  `maxStepsPerEpisode` (3 000) voor training of 0 (oneindig) voor
  live-VR-gameplay.
- `OnEpisodeBegin()` — leest de curriculum-parameters uit
  `EnvironmentParameters`, herinitialiseert smoothing en idle-teller,
  plaatst de bewaker op `guardSpawn` met willekeurige yaw en initialiseert
  de `last-known-position`-status.
- `CollectObservations(VectorSensor sensor)` — voegt de 17 vector-
  observaties toe (zie [Observatie-ruimte](#observatie-ruimte)) en
  bijwerkt de `last-known-position` wanneer de speler op dat moment
  zichtbaar is.
- `OnActionReceived(ActionBuffers actions)` — leest de twee continue
  acties, past tijd-, beweeg- en shaping-rewards toe, controleert
  zicht en vangst, registreert wand-contact en verwerkt
  `last-known-position`-bonussen.
- `Heuristic(in ActionBuffers actionsOut)` — biedt een handmatige
  W/A/S/D-besturing voor handmatig testen en debugging.

### Observatie-ruimte

De vector-observaties zijn opgebouwd in vier groepen:

- *Eigen toestand* (4 features): `sin(yaw)`, `cos(yaw)`,
  velocity X en velocity Z (genormaliseerd op `patrolSpeed`).
- *Laatste geluid-event* (4 features): lokale X en Z van de
  geluidsbron, recentheid (1 − leeftijd/5 s) en loudness.
- *Speler-detectie* (5 features): vlag "ziet speler", lokale X en Z
  van de speler, afstand (genormaliseerd op `worldSize` = 50 m) en
  resterende episodetijd (genormaliseerd).
- *Versie 7-extra* (4 features): lokale Y van de speler (verticale
  awareness), vlag voor geldige `last-known-position`, en de X en Z
  van die positie.

### Actie-ruimte

De agent kiest per stap twee continue waarden tussen −1 en 1: de eerste
schaalt de voorwaartse beweging op `patrolSpeed` (of `chaseSpeed`), de
tweede schaalt het rotatie-commando (180°/s). Er zijn geen discrete
branches.

### Beloningsfunctie

**Tabel 2.** Standaardwaarden van de beloningssignalen. De parameters
met prefix `w_` zijn instelbaar via `EnvironmentParameters` en
worden in curriculum-runs gevarieerd.

| Trigger | Waarde |
|---|---|
| Per timestep | −0,001 |
| Beweegt (snelheid > 0,5 m/s) | +0,005 |
| Stilstaan (> 100 stappen) | −0,01 |
| Progress richting prioritair doel | `w_progress × clamp(Δd, ±0,5)`; standaard 0,03 |
| Speler in zicht | +0,02 |
| Speler-nabijheid (exp-shaping) | `0,2 × exp(−d/2)` |
| Speler vangen (< 1 m) | +100,0 (episode eindigt) |
| Deposit bereiken | +0,1 (+0,5 als gealarmeerd) |
| Geluid onderzoeken (< 2,5 m) | `0,5 × loudness` |
| Last-known-position bereiken | +0,3 |
| Wand-contact (per FixedUpdate) | −0,01 |
| Tijd op (TimeUp) | `0,25 × niet-gestolen deposits` |

### Hyperparameters

De finale trainings-configuratie (`config/BankGuard_v7.yaml`) staat in
tabel 3. Curiosity is toegevoegd als intrinsiek reward-signaal om
exploratie te bevorderen.

**Tabel 3.** Trainer-configuratie voor run `BankGuard_v7`.

| Parameter | Waarde |
|---|---|
| trainer_type | ppo |
| batch_size | 2 048 |
| buffer_size | 20 480 |
| learning_rate | 3,0 × 10⁻⁴ (linear schedule) |
| beta | 5,0 × 10⁻² (linear) |
| epsilon | 0,2 (linear) |
| lambd | 0,95 |
| num_epoch | 3 |
| gamma (extrinsiek) | 0,99 |
| time_horizon | 256 |
| hidden_units | 256 |
| num_layers | 2 |
| memory (LSTM) | sequence_length 64, memory_size 128 |
| curiosity strength | 0,02 |
| max_steps | 10 000 000 |
| summary_freq | 5 000 |
| checkpoint_interval | 100 000 |
| num_envs / num_areas | 1 / 1 |
| time_scale | 20 |

### Trainings-iteraties

De finale run is voorafgegaan door een reeks experimenten. **Tabel 4**
geeft de belangrijkste configuratie-veranderingen weer.

**Tabel 4.** Belangrijkste experimentele iteraties.

| Run | Trainer-kern | Bijzonderheden | Max steps |
|---|---|---|---|
| `Bewaker_Phase1_v3`-`V20` | PPO zonder curiosity | Vroege navigatie-tests, behavior `BewakerAgent` | ≤ 3,0 M |
| `BankGuard_Curr_v1`-`v3` | PPO + curriculum (7 stages) | num_areas 8, beta verhoogd tot 3 × 10⁻² (constant) | ≤ 12 M |
| `BankGuard_Curr_v4` | PPO + curiosity, geen curriculum | Single-stage, 6 deposits, alle features actief | 15 M |
| `BankGuard_v5_kean` | PPO + curiosity + LSTM | Onderbroken door trainer-crash | 15 M |
| `BankGuard_v7` | PPO + curiosity + LSTM | Finale configuratie, num_areas 1 | 10 M (3,0 M gedraaid) |

### Methodologische beperkingen

Er zijn geen herhalingen met verschillende random seeds uitgevoerd
(alle runs gebruiken `seed: -1`). Statistische uitspraken over
gemiddelde en variantie van de eindreward zijn daardoor niet
onderbouwd. De training liep CPU-only; daardoor was het aantal
beschikbare steps per run beperkt ten opzichte van een vergelijkbare
GPU-opstelling.

## Resultaten

### Eindwaarden van de finale run

Run `BankGuard_v7` is bij 3 003 449 steps gestopt (van de
geconfigureerde 10 000 000 maximum). De cumulative mean reward
bedraagt op het laatste opgeslagen checkpoint **83,79**. De hoogste
geobserveerde waarde tijdens de run is **91,42** rond 3,0 M steps.

### Verloop van de reward-curve

> **[AFBEELDING 1: TensorBoard — Cumulative Reward voor
> `BankGuard_v7`. Zonder smoothing. Asbenamingen "Steps" en
> "Cumulative Reward". Bereik X: 0-3,0 M, Y: 0-100.]**

De waargenomen progressie kan in drie segmenten worden beschreven:

- **0-1,2 M steps**: cumulative reward ligt tussen 5 en 15.
- **1,2-2,3 M steps**: opbouw tussen 15 en 35.
- **2,3-3,0 M steps**: sprong tot ≈ 85, gevolgd door fluctuaties
  tussen 49 en 91.

### Episode-lengte

> **[AFBEELDING 2: TensorBoard — Episode Length voor
> `BankGuard_v7`. 0-3,0 M steps.]**

De gemiddelde episodelengte daalt van waarden nabij de maximumlimiet
(3 000 stappen) in de eerste fase tot 1 200-1 800 stappen in het
laatste half miljoen.

### Verlies- en entropie-curves

> **[AFBEELDING 3: TensorBoard — Policy Loss en Value Loss voor
> `BankGuard_v7`.]**

> **[AFBEELDING 4: TensorBoard — Entropy voor `BankGuard_v7`,
> 0-3,0 M steps.]**

`Policy Loss` daalt geleidelijk tot ≈ 0,015 en blijft daar; `Value
Loss` toont een lokaal maximum rond 2,3 M steps (samenvallend met de
reward-doorbraak) en vlakt daarna af. `Entropy` daalt monotoon van
≈ 1,1 naar ≈ 0,55.

### Curiosity-signaal

> **[AFBEELDING 5: TensorBoard — Curiosity Reward voor
> `BankGuard_v7`.]**

De curiosity-reward neemt af tijdens de eerste 500 K steps en
stabiliseert daarna op een laag niveau.

### Vergelijking met eerdere iteraties

> **[AFBEELDING 6: TensorBoard — Cumulative Reward voor
> `BankGuard_Curr_v3`, `BankGuard_Curr_v4` en `BankGuard_v7` op één
> grafiek. Geen smoothing.]**

In `Curr_v3` blijft de cumulative reward over 12,0 M steps onder 0,
met een eindwaarde van −0,77. `Curr_v4` bereikt na 15,0 M steps een
eindwaarde van +11,12; de stijging zet voornamelijk na 6 M steps in.
`v7` overstijgt deze waarden, ondanks een lager step-budget.

### Onzekerheid bij de observaties

Aangezien de runs geen seed-herhalingen kennen, kan over de
betrouwbaarheid van een enkel datapunt geen kwantitatieve uitspraak
gedaan worden. De zichtbare fluctuatie tussen 49 en 91 op de
reward-curve van `v7` ná de doorbraak suggereert een aanzienlijke
stochastische component, die deels te verklaren is door de
domein-randomisatie van deposit-posities en thief-spawnpunten.

## Conclusie

De waarnemingen wijzen op een succesvol getrainde policy die de
bewaker in staat stelt om patrouilleren, geluid onderzoeken en
achtervolgen te combineren binnen één geleerd gedragspatroon. De
duidelijke positieve trend in cumulative reward, de gestage daling van
de policy-loss en de afname van de gemiddelde episodelengte zijn
consistent met een model dat zijn taak effectiever uitvoert naarmate
de training vordert.

Het lijkt erop dat de overstap van curriculum-learning naar
single-stage training met een curiosity-signaal en een
geheugenmodule een grotere bijdrage heeft geleverd aan het bereiken
van een hoog reward-niveau dan het toevoegen van extra
curriculum-stages alleen. Eerdere curriculum-runs bleven onder de
laagste reward die `v7` haalt; pas met de gecombineerde aanpak werd
een merkbaar plateau bereikt.

De observaties moeten met voorzichtigheid worden geïnterpreteerd. De
afwezigheid van herhalingen met verschillende seeds beperkt de
mogelijkheid om de gevonden eindreward statistisch te verantwoorden.
Bovendien is een deel van de na-doorbraak-variantie waarschijnlijk
toe te schrijven aan de stochastische omgeving en niet aan
beleidsverbeteringen. Verder onderzoek zou minimaal drie
seed-herhalingen per configuratie moeten omvatten en zou kunnen
profiteren van GPU-gebaseerde training om meer steps binnen dezelfde
wandkloktijd te realiseren.

## Referenties

- Juliani, A., Berges, V.-P., Teng, E., Cohen, A., Harper, J.,
  Elion, C., … Lange, D. (2020). *Unity: A general platform for
  intelligent agents* (arXiv:1809.02627v2). arXiv.
- Schulman, J., Wolski, F., Dhariwal, P., Radford, A., & Klimov, O.
  (2017). *Proximal Policy Optimization Algorithms*
  (arXiv:1707.06347). arXiv.
- Burda, Y., Edwards, H., Pathak, D., Storkey, A., Darrell, T., &
  Efros, A. A. (2019). *Large-scale study of curiosity-driven
  learning* (arXiv:1808.04355). arXiv.
- Unity Technologies. (2024). *ML-Agents Toolkit documentation
  (release 21)*. <https://github.com/Unity-Technologies/ml-agents>
- Unity Technologies. (2024). *XR Interaction Toolkit 3.0 manual:
  Installation*. <https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/installation.html>
- AP Hogeschool. (2025). *ML-Agents — deel 1*
  [PowerPoint-presentatie]. Digitap, vak VR Experience.
- AP Hogeschool. (2025). *ML-Agents — slides*
  [PowerPoint-presentatie]. Digitap, vak VR Experience.
- AP Hogeschool. (2025). *ML-Agents — hyperparameters*
  [PowerPoint-presentatie]. Digitap, vak VR Experience.
- AP Hogeschool. (2025). *Toelichting hyperparameters ML-Agents*
  [Tekstdocument]. Digitap, vak VR Experience.
- AP Hogeschool. (2025). *Hoe werkt VR* [PowerPoint-presentatie].
  Digitap, vak VR Experience.
- AP Hogeschool. (2025). *Locomotions* [PowerPoint-presentatie].
  Digitap, vak VR Experience.
- AP Hogeschool. (2025). *VR Lab 1* [PowerPoint-presentatie].
  Digitap, vak VR Experience.
