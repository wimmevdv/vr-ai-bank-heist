# Kean Scene Setup — Bank Heist VR

Complete referentie voor de hoofd-scene (VR + AI guard + game-loop) en de trainings-scene.
Geldt voor het getrainde `BankGuard_v7` model (17-dim observations, **3M steps**, mean reward 85).

> **Main scene voor jouw VR/AI setup:** `Assets/Scenes/MAIN_SCENE2.unity`
> **Team-scene (NIET aanraken):** `Assets/Scenes/MAIN-SCENE.unity` (Wim's domein — gemaakt vrij van conflicten)
> Wim's game-loop scripts (HeistManager, GameUI, HeistEndBridge) gebruik je in MAIN_SCENE2.

---

## 1. Game-architectuur — wie doet wat

```
┌─────────────────────┐    catch event    ┌──────────────────┐
│ BankGuardAgent (AI) │ ────────────────► │ HeistEnvCtrl     │
│ + RaySensor         │  episodeOver=true │                  │
│ + Rigidbody         │                   └────────┬─────────┘
└─────────────────────┘                            │ polls
                                                   ▼
┌─────────────────────┐                  ┌────────────────────┐
│ VRPlayerBridge      │  noise events    │ HeistEndBridge     │
│ (op XR Origin)      │  ──────────────► │ LoseCaught()       │
└─────────────────────┘                  └────────┬───────────┘
                                                   │
                                                   ▼
┌─────────────────────┐  loot secured    ┌────────────────────┐
│ DropZone            │ ───────────────► │ HeistManager       │
│ ExtractionButton    │                  │ • 5-min timer      │
│ SafeZone            │                  │ • currentScore     │
│ LootItem(s)         │                  │ • OnGameEnded event│
└─────────────────────┘                  │ • DisableGuard() ★ │
                                          └────────┬───────────┘
                                                   │ subscribes
                                                   ▼
                                          ┌────────────────────┐
                                          │ GameUI             │
                                          │ End screen,        │
                                          │ Play Again button  │
                                          └────────────────────┘

★ DisableGuard stopt de AI bij elk game-einde (win, timeout, caught)
```

### Win/Lose-flow

| Situatie | Trigger | Resultaat |
|---|---|---|
| Speler ontsnapt | In `SafeZone` + `ExtractionButton` indrukken | `WinGame()` → `GameResult.Won` |
| Tijd op | `timeRemaining ≤ 0` | `LoseGame()` → `GameResult.LostTimeout` |
| Gepakt door AI | Guard <1m van speler → `HeistEnvController.EndEpisode(Caught)` → `HeistEndBridge` ziet `episodeOver=true` → `LoseCaught()` | `GameResult.LostCaught` |

Bij elk eind vuurt `HeistManager.OnGameEnded` met `HeistEndInfo` struct → `GameUI` toont end-screen + `DisableGuard()` stopt de AI.

---

## 2. Bank-prefab — locaties + colliders

**Pad:** `Assets/Prefabs/bank_and_XR_kean_prefab.prefab`

### Inhoud
- 7771 GameObjects, 479K regels YAML
- **170 walls** met `Wall` tag ✅
- **73 deposits** met `Deposit` tag ✅ (→ clusteren, zie sectie 8)
- 1367 colliders
- Meerdere verdiepingen + trappen met ramp-colliders (Untagged)
- Vault, safe rows, benches, decor
- Eigen `Player` object — **moet worden uitgevinkt** (jij gebruikt XR Origin)
- Van/Busje apart in `Assets/Prefabs/Busje.prefab`

### Waarom de nieuwe colliders beter zijn voor de AI
Rays detecteren colliders, niet meshes. Voorheen: visuele objecten zonder collider → AI zag ze niet. Nu: elk object met passende collider → AI ziet de wereld zoals jij hem ziet.

**Stairs:** ramp/plane colliders (Untagged) onder trapgeometrie → AI glijdt automatisch omhoog.

---

## 3. Wat de AI ziet en waarop hij let

### A. 17 vector observations

| # | Observatie | Bereik | Wat |
|---|---|---|---|
| 0-1 | Eigen yaw (sin, cos) | [-1, 1] | Kijkrichting |
| 2-3 | Eigen velocity (x, z) | [-1, 1] | Snelheid |
| 4-5 | Lokale positie laatste noise | [-1, 1] | Waar hoorde hij geluid |
| 6 | Versheid noise | [0, 1] | Hoe recent |
| 7 | Luidheid noise | [0, 1] | Hoe hard |
| 8 | Ziet hij speler | 0/1 | Lijn-of-zicht check |
| 9-10 | Lokale positie speler | [-1, 1] | Waar speler is |
| 11 | Afstand speler | [0, 1] | Hoe dichtbij |
| 12 | Episode-tijd over | [0, 1] | Urgentie |
| **13** | **(v7) Verticaal verschil speler** | **[-1, 1]** | **Boven/onder verdieping** |
| **14** | **(v7) Heeft last-known-pos** | **0/1** | **Onthoudt waar hij speler zag** |
| **15-16** | **(v7) Lokale last-known-pos** | **[-1, 1]** | **Loopt naar laatst-geziene plek** |

### B. 27 rays in halve cirkel vooraan

- 13 rays per zijkant + 1 centraal = **27 totaal**
- 120° spreiding, 20m bereik, bolradius 0.3m
- Vertical offset 0.5m start / 1.0m eind
- Detecteert tags: `Wall`, `Player`, `Deposit`

### C. Wat hij NIET ziet
- Achter zich (alleen 120° vooraan)
- Door muren heen (TrySeePlayer doet wall-block check)
- Deposits / spelers >20m weg

---

## 4. Beloningen tijdens training

| Beloning | Waarde | Wanneer |
|---|---|---|
| `w_catchPlayer` | **+100** | Speler <1m → episode caught |
| `w_seePlayer` | +0.02/step | Speler in zicht |
| `w_thiefProximity` | +0.2 × exp(-d/2) | Dichter bij speler |
| `w_progress` | +0.03 × Δdistance | Beweegt richting target |
| `w_investigateNoise` | +0.5 × loudness | Naar geluid lopen |
| `w_reachDeposit` (alarmed) | +0.5 | Alarmed deposit aanraken |
| Beweging-bonus | +0.005/step | Patrouilleren (anti-idle) |
| Wall hit penalty | -0.01/frame | Tegen muur lopen |

Voor inference irrelevant — beleid ligt vast in `.onnx`.

---

## 5. Alle scripts — wat draait waar

### Wimme/Test (AI-stack — niet aanpassen)
| Script | Doel | Op object |
|---|---|---|
| `BankGuardAgent.cs` | De AI agent — observations, beloningen, beweging | BankGuard GameObject |
| `HeistEnvController.cs` | Episode-state, deposits, noise events | HeistEnv GameObject |
| `VRPlayerBridge.cs` | XR Origin movement → noise voor AI | XR Origin |
| `GuardAnimator.cs` | Leest Rigidbody.velocity → Animator "Speed" param | BankGuard's Model-child |
| `ScriptedThief.cs` | Fake-thief voor training | **Alleen training-scene** |
| `PlayerThiefController.cs` | Keyboard-thief voor dev (mouselook) | Niet in main |
| `NavMeshRuntimeBootstrap.cs` | Auto-rebake NavMesh in builds | Static class, automatisch |

### Wimme (UI / glue / scene-tools)
| Script | Doel | Op object |
|---|---|---|
| `HeistEndBridge.cs` | Polls `episodeOver` → `HeistManager.LoseCaught()` | HeistEndBridge GameObject |
| `GameUI.cs` | End-screen + Play Again | Canvas met EndPanel |
| `NoiseEmitter.cs` ⭐ | Wereld-geluiden naar AI pushen (alarm, cash, glas, ...) | Op elk geluid-objects |

### Editor tools
| Script | Doel |
|---|---|
| `HeadlessBuild.cs` | Build-script voor training-exe via batchmode |
| `DepositClusterTool.cs` ⭐ | `Tools > Bank Heist > Cluster Deposits` — 73 deposits → 8-12 groepen |
| `HeistTrainingSetup.cs` | (legacy) |

### Player (game-logic — Wim's domein)
| Script | Doel | Op object |
|---|---|---|
| `HeistManager.cs` | **Singleton**, 5-min timer, score, `OnGameEnded` event, **`DisableGuard()` ⭐** | GameManager |
| `LootItem.cs` | Pakbaar item — naam, waarde, hover-highlight | Op stealable child van Deposit-group |
| `DropZone.cs` | Trigger zone — LootItem inleveren → score++ | DropZone GameObject |
| `SafeZone.cs` | Trigger zone — speler "is binnen" voor extraction | SafeZone GameObject |
| `ExtractionButton.cs` | XR-knop — vereist speler-in-SafeZone | Op knop in scene |
| `HeistHUD.cs` | Watch-style HUD (timer + score, billboard naar camera) | UI-element |
| `PlayerFootsteps.cs` ⭐ | Speelt footstep-audio + pusht noise naar AI op basis van beweging | XR Origin |

⭐ = nieuw / gewijzigd in deze sessie

### Verwijderd (geen functie)
- `PlayerStealthController.cs` — berekende noise-radius maar gaf niet door aan AI. Vervangen door VRPlayerBridge.
- `SoundSpawn.cs` — leeg placeholder (class heette `NewMonoBehaviourScript`)

---

## 6. Guard-model — Ch18_nonPBR

**Pad:** `Assets/Models/Guard/`

```
Ch18_nonPBR (1).fbx          ← 3D-model (humanoid rig)
Ch18_Body.mat                 ← material
Ch18_1001_Diffuse/Glossiness/Normal/Specular.png   ← textures
GuardController.controller    ← Animator state machine
Idle (1).fbx                  ← idle clip
Walking.fbx                   ← walk clip
Running.fbx                   ← run clip
```

**Hoe wirelen op BankGuard (zie sectie 7.3):**
1. Sleep `Ch18_nonPBR (1).fbx` als child onder BankGuard
2. Reset Transform child (lokale 0,0,0)
3. Op die child: Animator component → Controller = `GuardController`
4. Op die child: GuardAnimator script (leest van parent's Rigidbody)
5. GuardAnimator.walkThreshold = 0.5, runThreshold = 4.0
6. Test in Play: agent loopt → Walk anim. Sprint → Run anim. Stop → Idle.

> **De zichtbare Capsule-collider blijft op de BankGuard parent** (voor fysica). De Ch18 is alleen visueel.

---

## 7. MAIN_SCENE2 setup — stap voor stap

### Status bij start (Wim's MAIN_SCENE2.unity heeft al):
- `GameManager` (HeistManager singleton, timer 300s)
- `HeistEnv` (HeistEnvController)
- `HeistEndBridge`
- `Canvas` + `EndPanel` + `TitleText` + `SummaryText` + `PlayAgainButton` (GameUI)
- `EventSystem`, Lighting (Directional), Camera "Main"
- ProBuilder meshes (basale geometry)

### Wat je nog moet doen:

#### Stap 7.1 — Bank prefab in scene
1. Open `Assets/Scenes/MAIN_SCENE2.unity`
2. Sleep `Assets/Prefabs/bank_and_XR_kean_prefab.prefab` in Hierarchy root
3. Reset Transform (0,0,0)
4. In prefab: vink child `Player` **uit** (wij gebruiken XR Origin)
5. Verwijder evt. oude ProBuilder-meshes als ze conflicteren met de prefab

#### Stap 7.2 — Cluster de deposits (nieuw — sectie 8 hieronder)
**Doe dit nu** om van 73 → 8-12 groepen te gaan. Anders raken AI-rays in de war.

#### Stap 7.3 — BankGuard GameObject + guard-model
1. Maak leeg GameObject `BankGuard`, plaats op start-positie in de hal
2. Componenten op de **root** BankGuard:
   - **Rigidbody** — Mass 1, useGravity ✓, Freeze Rotation X + Z ✓
   - **CapsuleCollider** — radius 0.5, height 2.0, center (0,1,0), Is Trigger ❌
   - **BankGuardAgent** (script):
     - `env` → drag `HeistEnv`
     - `patrolSpeed` 3.5, `chaseSpeed` 5.5
     - `enableV7Observations` ✅ **true**
   - **Behavior Parameters**:
     - Behavior Name: **`BankGuard`** (exact deze string)
     - Vector Observation → Space Size: **17**
     - Actions → Continuous Actions: **2**
     - Behavior Type: **Inference Only**
     - Model: `BankGuard-3003449.onnx` (kopieer eerst naar `Assets/Models/Guard/` of `Assets/ai-models/`)
   - **Decision Requester** — Period 5, Take Actions Between Decisions ✓
   - **RayPerceptionSensorComponent3D**:
     - Sensor Name: `ForwardRays`
     - Detectable Tags: `Wall, Player, Deposit` (in deze volgorde)
     - Rays Per Direction: **13** (= 27 rays totaal, KRITIEK voor model-compat)
     - Max Ray Degrees: 120
     - Sphere Cast Radius: 0.3
     - Ray Length: 20
     - Start Vertical Offset: 0.5, End Vertical Offset: 1.0
     - Stacked Raycasts: 1
3. **Guard-model als child:**
   - Sleep `Assets/Models/Guard/Ch18_nonPBR (1).fbx` onder BankGuard
   - Reset Transform child
   - Voeg toe op de child: **Animator** (Controller = `GuardController`)
   - Voeg toe op de child: **GuardAnimator** script (auto vindt Animator + parent Rigidbody)

#### Stap 7.4 — XR Origin (de speler in VR)
1. GameObject → XR → XR Origin (VR)
2. Plaats op start-positie speler (bv. bij entree)
3. Op XR Origin root:
   - **Tag:** `Player`
   - **CapsuleCollider** — radius 0.6, height 1.8, center (0, 0.9, 0), Is Trigger ✓
   - **VRPlayerBridge** script → `env` → drag `HeistEnv`
4. Locomotion components:
   - **Continuous Move Provider** (stick beweegt XR Origin)
   - **Snap Turn Provider** of Continuous Turn
   - **Locomotion System**
5. Camera Offset → Main Camera (HMD-tracked)
6. Controllers (Left/Right) met XR Direct Interactor of Ray Interactor voor grabben

> **VR-gedrag:** met continuous-locomotion beweegt XR Origin met je stick. Je hoofd kan binnen ~30cm leunen/bukken zonder dat XR Origin meeloopt — dat is binnen de 1m catch-tolerantie, dus geen probleem.

#### Stap 7.5 — HeistEnv configureren
Klik `HeistEnv` in Hierarchy:
- `guardSpawn` → leeg GameObject op BankGuard's startplek
- `thiefSpawns[]` → mag leeg (alleen voor training relevant)
- `dropOffZone` → optioneel
- `guard` → drag BankGuard
- `thief` → **LEEG** (geen ScriptedThief in VR)
- `vrPlayer` → **drag XR Origin** ← ESSENTIEEL
- `depositSlots[]` → drag de 8-12 cluster-parents uit Hierarchy (na clustering)
- `episodeSeconds` → 300 (genegeerd in VR mode)
- `maxDepositsActive` → 6 of 8
- `randomizeDepositPositions` → **false**

#### Stap 7.6 — HeistEndBridge
Op `HeistEndBridge` GameObject:
- `env` → drag `HeistEnv` (of laat leeg — auto-find)

#### Stap 7.7 — Game-loop objecten

**SafeZone** — extraction-area:
- Plaats trigger-box in een logische escape-zone (bv. bij de Busje/Van)
- Box Collider, Is Trigger ✓
- SafeZone script

**ExtractionButton** — fysieke knop:
- 3D model van knop in/bij SafeZone
- XRSimpleInteractable component
- ExtractionButton script:
  - `safeZone` → drag SafeZone
  - `errorAudio` → AudioSource met `Assets/Prefabs/Sound/error.mp3`

**DropZone** — loot inlever-punt:
- Trigger-box (bv. in het Busje, of een tas)
- Box Collider, Is Trigger ✓
- DropZone script:
  - `cashRegisterAudio` → AudioSource met `Assets/Prefabs/Sound/cash.mp3`
- Optioneel: voeg `NoiseEmitter` script toe (alarm-niveau geluid voor AI bij elke loot-drop)

#### Stap 7.8 — UI checken (al aanwezig)
Op het Canvas zit `GameUI` script:
- `endPanel` → drag EndPanel
- `titleText` / `summaryText` → drag respective TextMeshPro
- `playAgainButton` → drag PlayAgainButton
- `snapToPlayerOnShow` ✅ (panel verschijnt voor je gezicht)
- Teksten staan al goed ("ESCAPE GELUKT" / "TIJD VOORBIJ" / "BETRAPT")

#### Stap 7.9 — Save & test
Ctrl+S, druk Play, controleer alle drie de uitkomsten.

---

## 8. Deposits clusteren — 73 → 8-12 groepen

### Waarom
AI-rays detecteren ALLE objecten met tag `Deposit`. 73 deposits = AI ziet 73 targets → mismatch met training (~5-6 deposits). Clusteren = AI ziet 8-12 realistische targets.

### Automatisch (aanbevolen) — Editor-tool
1. Open `MAIN_SCENE2.unity` met bank-prefab erin
2. Menu: **Tools > Bank Heist > Cluster Deposits**
3. Pas radius aan (default 3.0m) — alles binnen die afstand wordt 1 groep
4. Klik "Voorbeeld telling" om aantal clusters te checken
5. Tweak radius tot je ~8-12 groepen krijgt (radius hoger → minder groepen)
6. Klik "Cluster Now"
7. Resultaat: parents `Deposit_Group_01`, `Deposit_Group_02`, ... aangemaakt
8. **Ctrl+Z** undoet alles als je niet tevreden bent

### Per groep handwerk (nodig na clustering)
1. Open een groep in Hierarchy
2. Bekijk welke child het meest grijpbaar uitziet (goudbar, briefcase, painting, ...)
3. Op die child:
   - Add **Rigidbody** (mass 0.5, useGravity ✓)
   - Add **XRGrabInteractable**
   - Add **LootItem** script:
     - `itemName` = "Gold Bar" / "Briefcase" / etc.
     - `monetaryValue` = 500-5000
     - `highlightMaterial` = optionele emissive mat
4. Decoratieve children laat je zoals ze zijn

### Tot slot
5. Sleep alle `Deposit_Group_X` parents in `HeistEnvController.depositSlots[]`
6. Stel `HeistEnvController.maxDepositsActive` = 6 (of 8)

### Wat de tool doet (technisch)
- Vindt alle GameObjects met tag `Deposit` in actieve scene
- Greedy-clustering op nabijheid (binnen radius van willekeurig lid van groep)
- Per cluster: nieuw empty GameObject met tag `Deposit` + trigger BoxCollider om cluster heen
- Children worden ondergebracht onder parent, tag → Untagged
- Undo wordt geregistreerd

### Collider-flow tijdens game
- BoxCollider parent is **trigger** → speler-hand gaat erdoorheen
- Child LootItems hebben solid collider → speler-hand grabbed ze
- AI-rays detecteren parent's trigger-BoxCollider (RayPerceptionSensor doet dat by default)
- Player kan dus de "kluis" binnenlopen en losse items pakken

---

## 9. Sound-systeem

### Beschikbare audio-assets in project
| Asset | Pad | Gebruik |
|---|---|---|
| `cash.mp3` | `Assets/Prefabs/Sound/` | ✅ DropZone success |
| `error.mp3` | `Assets/Prefabs/Sound/` | ✅ ExtractionButton zonder safe zone |
| `Button_22_click.wav` | `Assets/VRTemplateAssets/Audio/` | ✅ Play Again button |
| `Button_14_hover.wav` | `Assets/VRTemplateAssets/Audio/` | ✅ Knop hover |
| `freesound_community-infobleep-87963.mp3` | `Assets/audio/` | algemeen bleep |

### Mist nog (downloaden van freesound.com / Mixkit)
- ❌ Footsteps (lopen op vloer) — 1-2 clips
- ❌ Atmospheric music (ambient bank/heist) — 1-2 min loopable
- ❌ Alarm sirene — 5-10s loop
- ❌ Glass break — voor ramen/vitrines
- ❌ Vault-door open/sluiten — als gameplay-relevant

> Vrije bronnen: [freesound.org](https://freesound.org), [Mixkit](https://mixkit.co/free-sound-effects/), [Pixabay Audio](https://pixabay.com/sound-effects/) — allemaal CC0 of vrije licentie.

### Hoe geluiden naar de AI komen

```
Geluid-bron → HeistEnvController.RegisterNoise(pos, loudness)
                  ↓
             AI observatie obs[4..7]: position + age + loudness
                  ↓
             AI patrouilleert er heen (reward +0.5 × loudness)
```

**Eén noise-slot** — nieuwe geluid overschrijft oude. Volgorde dus belangrijk: alarm > glasbreuk > voetstappen > kassa.

### Speler-geluiden

**Voetstappen — `PlayerFootsteps.cs` (al klaar):**
1. Op XR Origin: voeg `AudioSource` toe (al verplicht via `[RequireComponent]`)
2. Voeg `PlayerFootsteps` script toe op XR Origin
3. Voeg `NoiseEmitter` toe op XR Origin (env auto-find), sleep deze in `PlayerFootsteps.noiseEmitter` veld
4. Sleep 2-4 footstep `.wav`-clips in `footstepClips[]`
5. Tweaks:
   - `stepDistance` 0.6m (default, prima voor lopen)
   - `minSpeedForStep` 0.3 m/s (filtert HMD-wobble)
   - `quietLoudness` 0.15 / `loudLoudness` 0.7 (sluipen vs rennen voor AI)
6. Het script speelt random footstep + pusht noise-event met luidheid op basis van snelheid → AI hoort je harder rennen

**Footstep-clips downloaden:** [freesound.org](https://freesound.org) → zoek "footsteps marble" / "footsteps wood" → 2-4 korte clips (0.3-0.5s elk).

**Ambient music:**
1. Empty GameObject `AmbientMusic`
2. AudioSource met music-clip, loop ✓, volume 0.2-0.3, Spatial Blend = 0 (2D)
3. Géén NoiseEmitter — muziek moet AI NIET triggeren

### Wereld-geluiden via `NoiseEmitter.cs`

```csharp
// Drop dit script op elk geluid-object:
// • Alarm                → loudness 1.0
// • Glass break          → loudness 0.8
// • Door slam            → loudness 0.6
// • Cash register        → loudness 0.4
// • Background machinery → loudness 0.0 (geen AI-trigger)
```

Auto-mode: koppel een AudioSource → elke keer dat de clip start (isPlaying false→true), wordt eenmalig een noise-event gepusht.

Handmatige mode: roep `noiseEmitter.Emit()` aan vanuit code of UnityEvent.

---

## 10. Wat ABSOLUUT NIET aangepast mag worden

Deze instellingen moeten matchen met het getrainde model — anders **breekt het** (random gedrag).

| Setting | Vaste waarde | Locatie |
|---|---|---|
| Vector Observation Size | **17** | Behavior Parameters |
| Continuous Actions | **2** | Behavior Parameters |
| BehaviorName | **`BankGuard`** | Behavior Parameters |
| `enableV7Observations` | **true** | BankGuardAgent |
| Ray tags | **`Wall, Player, Deposit`** | RaySensor |
| Rays Per Direction | **13** | RaySensor (= 27 totaal rays) |
| Max Ray Degrees | **120** | RaySensor |
| Start/End Vertical Offset | **0.5 / 1.0** | RaySensor |
| Sphere Cast Radius | **0.3** | RaySensor |
| Ray Length | **20** | RaySensor |
| Decision Period | **5** | DecisionRequester |
| Catch-afstand | **1.0m** | BankGuardAgent.cs:186 |
| Rigidbody constraints | Freeze X + Z rotation | Rigidbody |
| CapsuleCollider | radius 0.5, height 2.0 | Collider |
| Mass | 1 | Rigidbody |

### Mag wel veranderd
- ✅ Reward weights (training-relevant, inference negeert)
- ✅ `patrolSpeed`, `chaseSpeed` (±1 veilig, drastisch wijzigt timing)
- ✅ `episodeSeconds`, `maxStepsPerEpisode` (VR mode negeert beide)
- ✅ Aantal deposits, randomization, distractors
- ✅ HeistManager-timer, score, restart-flow
- ✅ Welk `.onnx`-model je laadt
- ✅ UI, audio, lighting, decor, alle Player-scripts

---

## 11. Modellen — welke .onnx kiezen

In `C:\VR\results\BankGuard_v7\BankGuard\`:

| Checkpoint | Tijd | Reward niveau | Aanrader |
|---|---|---|---|
| `BankGuard-1399881.onnx` | ~15:25 | ~25 | – |
| `BankGuard-1999847.onnx` | 19:03 | ~35 | – |
| `BankGuard-2599878.onnx` | 20:16 | ~70 | Backup als 3M te agressief |
| `BankGuard-2899748.onnx` | 20:56 | ~85 | – |
| **`BankGuard-3003449.onnx`** | **21:08** | **~85** | **★ FINAL — gebruik deze** |

**Voor inference:** kopieer naar `Assets/Models/Guard/` (zodat Unity hem indexeert), sleep in BankGuard.Model field.

---

## 12. Training-scene `kean_scene_Training3` — optioneel fine-tunen

Alleen nodig als AI in MAIN_SCENE2 zich raar gedraagt door layout-verschillen (multi-floor, andere geometry).

### Verschillen tegenover MAIN_SCENE2

| Onderdeel | MAIN | Training |
|---|---|---|
| Bank prefab | ✅ | ✅ |
| Player | XR Origin | ❌ — gebruikt ScriptedThief |
| ScriptedThief | ❌ | ✅ |
| NavMeshSurface (gebakken) | ❌ | ✅ REQUIRED |
| HeistEnvController.vrPlayer | XR Origin | **LEEG** |
| HeistEnvController.thief | leeg | ScriptedThief |
| randomizeDepositPositions | false | **true** |
| BankGuard BehaviorType | Inference Only | **Default** |
| BankGuard Model | je .onnx | **leeg** |
| HeistEndBridge / HeistManager / UI | ✅ | ❌ niet nodig |
| Deposit-clusters | ja | ja (zelfde) |

### Setup
1. **Duplicate MAIN_SCENE2** → File > Save As → `kean_scene_Training3.unity`
2. **Verwijder** uit hiërarchie: XR Origin, HeistEndBridge, GameManager, Canvas, EventSystem
3. **Voeg `ScriptedThief` toe** als GameObject:
   - NavMeshAgent (radius 0.4, height 1.8)
   - CapsuleCollider, **tag = Player**
   - ScriptedThief script (stealSeconds=2.5, runMoveSpeed=3.5)
4. **NavMesh bake:**
   - Selecteer bank-prefab in Hierarchy → Inspector → **Static dropdown** → **Navigation Static** ✓ (yes, change children)
   - GameObject → AI → NavMesh Surface (op leeg GameObject)
   - Settings: Agent Radius 0.4, Height 1.8, **Step Height 0.5** (voor trappen!), Max Slope 45°
   - Klik **Bake** → blauwe overlay zichtbaar op vloeren EN trappen
   - Als trappen niet blauw: verhoog Step Height
5. **HeistEnvController** velden volgens tabel hierboven
6. **HeadlessBuild.cs** aanpassen:
   ```csharp
   scenes = new[] { "Assets/Scenes/kean_scene_Training3.unity" },
   ```
7. **Build:**
   ```
   Unity.exe -batchmode -quit -nographics -projectPath C:\VR -executeMethod Wimme.EditorTools.HeadlessBuild.BuildTraining
   ```
8. **Fine-tune training:**
   ```bat
   python scripts\launch_training_v3.py config\BankGuard_v7.yaml ^
     --run-id=BankGuard_v8 ^
     --env=C:\VR\Builds\VR_project.exe ^
     --initialize-from=BankGuard_v7 ^
     --no-graphics --num-envs=1 --timeout-wait=600 --force
   ```

~1M extra steps op nieuwe layout = ~3-4u. Behoudt geleerde catch/chase-skills, leert nieuwe geometrie.

### AI live observeren in training-scene

**Methode 1 — Editor met huidige .onnx (snelst, geen rebuild nodig):**
1. Open `kean_scene_Training3.unity` in Editor
2. Op BankGuard: BehaviorType = **Inference Only**, Model = `BankGuard-3003449.onnx`
3. Druk Play → guard patrouilleert, ScriptedThief steelt, guard catched
4. Gizmos aanzetten (top-right Scene View) om te zien:
   - 🔴 Rode lijn: huidige zichtlijn naar speler (`Debug.DrawLine` in agent)
   - 🟡 Gele lijn: laatst-bekende-positie
   - Ray-sensor visualisatie via Inspector

**Methode 2 — Training met visuele build (voor tunen):**
1. Verwijder `-nographics` in HeadlessBuild.cs en in launch_training_v3.py command
2. Rebuild → exe heeft window
3. Tijdens training zie je het venster met meerdere parallelle agents

**Methode 3 — TensorBoard trend:**
```bat
tensorboard --logdir=results
```
http://localhost:6006 → reward-curve, value-loss, policy-loss

---

## 13. Quick checklist voor "klaar voor presentatie"

### MAIN_SCENE2.unity
- [ ] Bank prefab erin, Player-kind inactive
- [ ] **Deposits geclusterd via Tools > Bank Heist > Cluster Deposits**
- [ ] Per cluster minimaal 1 LootItem met XRGrabInteractable + Rigidbody
- [ ] XR Origin: tag Player, CapsuleCollider trigger, VRPlayerBridge.env wired
- [ ] BankGuard: Rigidbody (mass 1, freeze X+Z), Capsule (0.5/2.0), Agent + 17 obs + V7 true, Ray sensor settings exact, BehaviorType Inference Only, Model = BankGuard-3003449.onnx
- [ ] BankGuard child: Ch18_nonPBR + Animator (GuardController) + GuardAnimator script
- [ ] HeistEnv: vrPlayer=XR Origin, thief=leeg, depositSlots gevuld met cluster-parents, randomize=false
- [ ] HeistEndBridge: env=HeistEnv (of leeg → auto-find)
- [ ] GameManager: timer 300
- [ ] SafeZone (trigger) + ExtractionButton (XR knop) + DropZone (trigger met cash audio)
- [ ] GameUI Canvas: refs naar EndPanel + texts + button
- [ ] Tags `Player`, `Wall`, `Deposit` bestaan
- [ ] Footstep + ambient music AudioSources op XR Origin / scene

### Test-loop
- [ ] Play → guard patrouilleert in nieuwe bank
- [ ] Loop naar guard binnen 1m → BETRAPT screen + guard stopt
- [ ] Pak loot uit cluster → drop in DropZone → score++
- [ ] In SafeZone → ExtractionButton drukken → ESCAPE GELUKT + guard stopt
- [ ] Wacht 5 min → TIJD VOORBIJ + guard stopt
- [ ] Play Again → scene reset, alles werkt opnieuw

---

## 14. Wijzigingen deze sessie

| File | Verandering |
|---|---|
| `Assets/Scripts/Player/HeistManager.cs` | + `DisableGuard()` na elk game-einde (stopt AI) |
| `Assets/Scripts/Wimme/NoiseEmitter.cs` | NIEUW — universeel script voor wereld-geluiden → AI |
| `Assets/Scripts/Editor/DepositClusterTool.cs` | NIEUW — Tools > Bank Heist > Cluster Deposits |
| `Assets/Scripts/Player/PlayerStealthController.cs` | VERWIJDERD (was dead code) |
| `Assets/Scripts/Wimme/SoundSpawn.cs` | VERWIJDERD (was leeg placeholder) |
| `Assets/Models/Guard/*` | HERSTELD van stash (Ch18_nonPBR + animaties + controller) |
| `Assets/Scripts/Wimme/Test/GuardAnimator.cs` | HERSTELD van stash |
| `Assets/Scripts/Player/PlayerFootsteps.cs` | NIEUW — voetstap-audio + auto-noise voor AI |

---

## 15. Open punten / handwerk dat nog moet gebeuren

- [ ] Cluster deposits via Tools-menu (sectie 8)
- [ ] Per cluster: LootItem-component op grijpbare child
- [ ] BankGuard GameObject opzetten in MAIN_SCENE2 (sectie 7.3)
- [ ] Ch18_nonPBR als child onder BankGuard + Animator-controller + GuardAnimator
- [ ] XR Origin met VRPlayerBridge (sectie 7.4)
- [ ] HeistEnv velden invullen (sectie 7.5)
- [ ] SafeZone + ExtractionButton + DropZone plaatsen (sectie 7.7)
- [ ] LootItems op stealable items in clusters
- [ ] **Audio downloaden**: footsteps, ambient music, alarm, glass break
- [ ] NoiseEmitter op alarm/glass/door-objecten
- [ ] Test alle 3 game-eindes in Play
- [ ] Niet vergeten — oude scenes opruimen na alles werkt:
  - `BankGuardTestArena.unity`
  - `COPY marwan.unity` (jouw kopie?)
  - `kean_scene.unity` (oude main)
  - `kean_scene_AItraining.unity`
  - `kean_scene_prefab.unity` (intermediair)
  - `kean_scene_Training2.unity`
  - `MarwanScene.unity`
  - `Renzo Scene.unity`
  - `Wimme Scene.unity`
- [ ] Optioneel: `Assets/_Recovery/` folder opruimen (Unity auto-recovery dumps)
- [ ] Optioneel: fine-tune training op `kean_scene_Training3` als AI raar doet in nieuwe layout

---

## 16. Troubleshooting

| Symptoom | Oorzaak | Fix |
|---|---|---|
| AI staat stil bij Play | Model niet geladen / BehaviorType Default | Behavior Parameters: Model field + Inference Only |
| AI loopt door muren | Walls hebben geen `Wall` tag | Check Tag in Hierarchy |
| AI ziet speler niet | XR Origin geen tag `Player` / geen collider | VRPlayerBridge.Start() zet dit auto — check refs |
| AI draait in cirkels | Observation-mismatch | Vector Obs Size 17, enableV7Observations=true |
| AI stopt niet bij win/lose | HeistManager.DisableGuard() niet aangeroepen | Verifieer dat script up-to-date is, BankGuard heeft tag op root |
| Episode-timer reset in VR | `vrPlayer` veld leeg in HeistEnv | Drag XR Origin in HeistEnv.vrPlayer |
| End screen toont niet | GameUI niet wired / Canvas inactive | Check GameUI refs + Canvas SetActive |
| End screen ver weg | `snapToPlayerOnShow` uit | Inschakelen op GameUI |
| Loot levert niets op | DropZone geen Trigger / LootItem mist | Check Is Trigger + XRGrabInteractable + LootItem |
| ExtractionButton doet niets | Speler niet in SafeZone, of ref leeg | Check SafeZone trigger + button.safeZone ref |
| Guard-model speelt geen animatie | Animator Controller leeg / GuardAnimator mist Rigidbody parent | Check Animator.controller = GuardController, GuardAnimator op child van BankGuard |
| Guard glijdt door trap | Ramp-collider mist of heeft Wall-tag | Check ramp-colliders zijn Untagged, niet Wall |
| Cluster tool vindt geen deposits | Tag bestaat niet in Tags & Layers | Voeg tag `Deposit` toe in Project Settings |
| Veel "Missing Script" in Hierarchy | GuardAnimator was gewist door stash | Hersteld in `Assets/Scripts/Wimme/Test/GuardAnimator.cs` |

---

## 17. Push-strategie — wat WEL en NIET pushen

### ✅ Veilig om te pushen (mijn deze-sessie + jouw v7-werk)

| Bestand | Status |
|---|---|
| `Assets/Models/Guard/` (hele folder) | NIEUW — Ch18 model + animations + controller + textures |
| `Assets/Scripts/Wimme/Test/GuardAnimator.cs` (+ .meta) | NIEUW |
| `Assets/Scripts/Wimme/NoiseEmitter.cs` (+ .meta) | NIEUW |
| `Assets/Scripts/Editor/DepositClusterTool.cs` (+ .meta) | NIEUW |
| `Assets/Scripts/Player/PlayerFootsteps.cs` (+ .meta) | NIEUW |
| `Assets/Scripts/Player/HeistManager.cs` | EDIT — `DisableGuard()` toegevoegd |
| `Assets/Scripts/Player/PlayerStealthController.cs` (+ .meta) | DELETE |
| `Assets/Scripts/Wimme/SoundSpawn.cs` (+ .meta) | DELETE |
| `KEAN_SCENE_SETUP.md` | EDIT — deze doc |
| `Assets/Scripts/Wimme/Test/BankGuardAgent.cs` | EDIT — v7-features (17 obs) |
| `Assets/Scripts/Wimme/Test/HeistEnvController.cs` | EDIT — vrPlayer field, VR-mode skip |
| `Assets/Scripts/Wimme/Test/VRPlayerBridge.cs` | EDIT — noise integration |
| `Assets/Scripts/Wimme/Test/PlayerThiefController.cs` | EDIT — CharacterController stair fix |
| `config/BankGuard_v7.yaml` | EDIT — checkpoint_interval 100K |

### ❌ NIET pushen (training-output, lokale logs, scene-WIP)

```
results/BankGuard_v7/                ← ~30 MB checkpoints (.onnx + .pt)
training_v7.log / training_v7.err    ← runtime logs
Assets/Scenes/kean_scene.unity       ← oude scene (wordt vervangen)
Assets/Scenes/kean_scene_AItraining.unity  ← oude training
Assets/Scenes/kean_scene_Training2.unity   ← oude training (waar v7 op trainde)
Assets/Scenes/COPY marwan.unity      ← jouw werkkopie
Assets/Scenes/MAIN_SCENE2.unity       ← (laat Wim dit beheren) — NIET wijzigen tot je live setup gedaan hebt
Assets/Settings/Project Configuration/*.asset  ← Unity auto-updates
Assets/URPDefaultResources/*.asset   ← Unity auto-updates
Assets/XR/AndroidXR/AndroidXRSettingsInitializer ← Unity auto-update
ProjectSettings/EditorBuildSettings.asset       ← (alleen pushen als je expliciet build-scenes wijzigt)
VR.slnx                              ← Visual Studio sln (Unity regenereert)
```

> **Belangrijk voor je teamgenoot Wim**: BankGuardAgent.cs heeft nu 17 observations (was 13). Oudere `.onnx`-modellen (v3/v4/v5/v5b/v6) werken niet meer. Alleen `BankGuard_v7*.onnx` is compatibel. Vermeld dit in de PR-beschrijving.

### Commit-stappen (zelf uitvoeren of door mij laten doen)

```bash
# 1. Stage NIEUWE files
git add Assets/Models/Guard.meta Assets/Models/Guard/
git add Assets/Scripts/Wimme/Test/GuardAnimator.cs Assets/Scripts/Wimme/Test/GuardAnimator.cs.meta
git add Assets/Scripts/Wimme/NoiseEmitter.cs Assets/Scripts/Wimme/NoiseEmitter.cs.meta
git add Assets/Scripts/Editor/DepositClusterTool.cs Assets/Scripts/Editor/DepositClusterTool.cs.meta
git add Assets/Scripts/Player/PlayerFootsteps.cs Assets/Scripts/Player/PlayerFootsteps.cs.meta

# 2. Stage EDITS (AI/game-relevant)
git add Assets/Scripts/Player/HeistManager.cs
git add Assets/Scripts/Wimme/Test/BankGuardAgent.cs
git add Assets/Scripts/Wimme/Test/HeistEnvController.cs
git add Assets/Scripts/Wimme/Test/VRPlayerBridge.cs
git add Assets/Scripts/Wimme/Test/PlayerThiefController.cs
git add config/BankGuard_v7.yaml
git add KEAN_SCENE_SETUP.md

# 3. Stage DELETIONS
git rm Assets/Scripts/Player/PlayerStealthController.cs Assets/Scripts/Player/PlayerStealthController.cs.meta
git rm Assets/Scripts/Wimme/SoundSpawn.cs Assets/Scripts/Wimme/SoundSpawn.cs.meta

# 4. Commit + push
git commit -m "AI v7 integration: guard model, game-end stop, deposit clustering, noise system"
git push origin main
```

---

## 18. Conventies & nice-to-knows

- **Catch-trigger:** AI catched ALTIJD bij <1.0m afstand (line-of-sight niet nodig op moment van catch). Niet aanpassen.
- **Distance check in `OnActionReceived`** EN trigger-collider in `OnTriggerEnter/Stay` — dubbele veiligheid, voorkomt missed catches door fast-moving frames.
- **`HeistEndBridge` polls** elke frame `episodeOver` boolean. Geen event-subscription nodig.
- **VR-mode auto-detect:** `BankGuardAgent.Initialize()` checkt `env.vrPlayer != null` → `MaxStep = 0` (timer uit) + skipt episode-reset in OnEpisodeBegin.
- **`randomizeDepositPositions = true`** is alleen voor training — gebruikt `NavMesh.SamplePosition` dus vereist NavMesh.
- **Multi-floor:** v7-model heeft vertical-awareness observations, maar trappen-fysica is afhankelijk van ramp-colliders. Werkt zolang ramps Untagged zijn.
- **Editor-tool ongedaan maken:** Ctrl+Z na "Cluster Now" undoet alle reparenting + parent-creatie.
