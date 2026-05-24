# Kean Scene → AI Training Setup Checklist

Doel: `kean_scene` voorbereiden zodat v5 daar in getraind kan worden met **random deposit posities elke episode** + de agent kan ook door trappen en meerdere verdiepingen heen leren bewegen.

**Geschatte tijd: 60-90 min handmatig werk in Unity Editor.**

> **Voor je begint:** ik heb een editor wizard onder menu **"Heist Training"** (verschijnt in Unity menu bar bovenaan). Die doet 80% van het werk in 4 menuklikken. Sluit Unity nadat alles klaar is (anders kan ik niet builden).

---

## STAP 1 — Unity openen + scene duplicate (5 min)

> ⚠️ NOOIT direct in `kean_scene` werken. Maak een kopie zodat je originele scene veilig blijft.

1. Open Unity Hub → open project `C:\VR`
2. Unity zal even compileren (zie progress balk onderaan). Wacht tot menu **"Heist Training"** verschijnt bovenaan.
3. Project window → klap `Assets/Scenes` open
4. Rechtsklik op `kean_scene.unity` → **Duplicate** (Ctrl+D)
5. Hernoem de kopie naar `kean_scene_AItraining`
6. Dubbelklik om die kopie te openen
7. Vanaf hier werk je in `kean_scene_AItraining`

---

## STAP 2 — Mark walkable surfaces als Navigation Static (10-15 min)

NavMesh bakt alleen over objecten die als "Navigation Static" gemarkeerd zijn.

1. Hierarchy → selecteer **alle vloeren, trappen, gangen, terrassen** van de bank.
   - Tip: als je een parent hebt zoals `Geometry/Floors` of `Building` → selecteer die parent, dat dekt alle kinderen recursief.
   - **Belangrijk:** trappen MOETEN meegenomen — agent moet leren ze op te lopen.
2. Menu: **Heist Training → 4. Mark Selected as Navigation Static**
3. Console toont: `Marked X roots (+ children) as Navigation Static`

> Tip: bij twijfel royaal selecteren. Te veel kan geen kwaad. Muren en plafonds NIET nodig.

---

## STAP 3 — Bake NavMesh (3-5 min)

1. Menu: **Window → AI → Navigation**
2. Tabblad **Bake**
3. Klik **Bake** knop onderaan
4. Wacht (1-3 min voor de hele bank)
5. **Verificatie:** met Navigation panel open zie je in Scene view een **blauwe overlay** op alle walkable vloeren — inclusief trappen.

> ⚠️ Géén blauw op een trap? Trap is niet Navigation Static gemarkeerd → terug naar Stap 2.
>
> ⚠️ Trap-blauw heeft gaten in het midden? Agent step-height is te laag in Bake settings. Klik in Navigation panel → Agents tab → verhoog **Step Height** naar `0.5`. Re-bake.

---

## STAP 4 — Tag de muren (15-20 min)

De agent's ray sensor detecteert obstakels via de `Wall` tag. Untagged muren zijn onzichtbaar voor de agent.

1. Hierarchy → selecteer alle **muur-, kolom-, gevel-objecten** (Ctrl+klik). Parent als `Geometry/Walls` of `Architecture` volstaat.
2. Menu: **Heist Training → 3. Tag Selected Objects as Wall**
3. Console toont aantal getaggde objecten + hun child colliders.

> ⚠️ **Trappen NIET als Wall taggen** — anders ziet agent ze als obstakel en loopt er niet op.
>
> Tafels/meubels: alleen taggen als Wall als ze in de weg moeten staan voor agent.

---

## STAP 5 — Plaats de Training Rig (1 klik, 1 min)

Menu: **Heist Training → 1. Create Training Rig**

Wat dit automatisch doet:
- `HeistTrainingRig` (root parent)
  - `HeistController` (HeistEnvController script — beheert episodes)
  - `GuardSpawn` (markering — waar agent start)
  - `ThiefSpawns/ThiefSpawn_1..3` (3 mogelijke start-posities voor thief, random gekozen)
  - `DropOffZone` (markering, niet kritisch)
  - `Deposits` (parent, leeg — vul je in stap 7)
  - `DistractorPoints` (parent, leeg, optioneel voor sfeer)
  - `BankGuardAgent` (blauwe cube, alle ML-Agents componenten al wired)
  - `ScriptedThief` (rode capsule met NavMeshAgent + trigger collider)

Alle references tussen deze objects zijn automatisch geconfigureerd ✓. Agent Rigidbody Y is **NIET** gefrozen, dus hij kan trappen op/af lopen.

---

## STAP 6 — Positioneer de markers (10-15 min)

Verplaats deze GameObjects naar logische plekken in jouw bank:

| GameObject | Waar | Tip Y-coordinate |
|---|---|---|
| `GuardSpawn` | Centrale plek op grond verdieping, bv. receptie | Op de vloer (Y = vloer-niveau) |
| `BankGuardAgent` | Zelfde plek als GuardSpawn | Op vloer + 1 (collider valt anders door vloer in editor) |
| `ThiefSpawn_1` | Buitenplaats (waar VR speler echt zou spawnen) | Op vloer |
| `ThiefSpawn_2` | Eerste verdieping ergens centraal | Op vloer |
| `ThiefSpawn_3` | Basement, bv. bij vault | Op vloer |
| `ScriptedThief` | Zelfde plek als ThiefSpawn_1 | Op vloer |
| `DropOffZone` | Bij het busje buiten | Op vloer |

> **Belangrijk:** GuardSpawn en ThiefSpawns op de vloer plaatsen — script doet automatisch +1 offset voor agent zodat hij niet half in de vloer zit.
>
> **Move-tip:** klik object, druk W, sleep in scene view. Of vul Transform.Position direct in.

---

## STAP 7 — Stel randomization bounds in (3 min)

Klik op `HeistController` GameObject. Inspector → HeistEnvController → **Randomize Bounds**.

Stel `Center` en `Size` zo in dat de Bounds box (zichtbaar als gele gizmo in scene view bij geselecteerd object) **de hele speelbare bank omsluit** — courtyard tot vault.

Voorbeeld waarden:
- `Center`: midden van bank (X, Y_vloer, Z)
- `Size`: ruim, bv. (80, 10, 80) — `Y=10` covert basement tot upper floor

Visual check: scene view bij `HeistController` geselecteerd → gele draadbox moet alle bankvloeren overlappen.

> Hierbinnen worden deposits random gespawnd elke episode. Te klein = deposits altijd op 1 plek. Te groot = soms buiten NavMesh = deposit blijft op slot-positie.

---

## STAP 8 — Maak deposit slots (1 klik + verplaats, 5 min)

Menu: **Heist Training → 2. Add 8 Deposits at random NavMesh points**

Wat dit doet:
- Maakt 8 gouden cubes onder `Deposits/`
- Tag `Deposit` + isTrigger collider
- Random startpositie op NavMesh
- Wired automatisch in `HeistController.DepositSlots`

> Initiële posities maken nauwelijks uit — het script verplaatst ze ELKE EPISODE naar nieuwe random NavMesh punten binnen randomizeBounds.

Als er minder dan 8 zijn geplaatst (console waarschuwing): NavMesh is te klein of niet gebaked. Stap 2-3 controleren.

---

## STAP 9 — Sanity check (5 min)

### A) HeistController
Klik `HeistController` → Inspector → HeistEnvController:

| Veld | Verwachte waarde |
|---|---|
| Guard Spawn | GuardSpawn (Transform) ✓ |
| Thief Spawns | Size: 3 (drie ThiefSpawn objects) ✓ |
| Drop Off Zone | DropOffZone ✓ |
| Guard | BankGuardAgent ✓ |
| Thief | ScriptedThief ✓ |
| Deposit Slots | Size: 8 ✓ |
| Randomize Deposit Positions | ✅ (aangevinkt) |
| Randomize Bounds | jouw scene bounds ✓ |

### B) BankGuardAgent script
Klik `BankGuardAgent` → Inspector → BankGuardAgent:

| Veld | Verwachte waarde |
|---|---|
| Env | HeistController ✓ |

### C) Behavior Parameters
Klik `BankGuardAgent` → Inspector → Behavior Parameters:

| Veld | Verwachte waarde |
|---|---|
| Behavior Name | `BankGuardAgent` |
| Vector Observation Size | 13 |
| Continuous Actions | 2 |
| Model | (leeg — voor training) |
| Behavior Type | Default |

### D) Test-Play even (optioneel, 30s)
Druk **Play** in Unity. Verwacht:
- Geen rode Console errors
- Thief beweegt (NavMeshAgent loopt naar dichtstbijzijnde deposit)
- Agent doet random bewegingen (geen model → random policy)
- Deposits flashen actief

Druk **Stop** na 5 sec.

---

## STAP 10 — Save & Close (1 min)

1. **File → Save** (Ctrl+S)
2. **Sluit Unity Editor volledig** (kruisje rechtsboven). Anders kan ik geen headless build doen.

---

## STAP 11 — Laat het me weten

Schrijf "klaar". Ik:
1. Update training scripts om `kean_scene_AItraining` te targeten
2. Doe headless build
3. Smoketest 200k steps (~7 min) — verifieer reward signal werkt
4. Start full v5 training overnacht (~7u)
5. Morgenochtend inference test in originele `kean_scene` met getrainde ONNX

---

## Troubleshooting

**"Heist Training menu staat er niet"** → Unity is bezig met script-compile. Wacht 30 seconden. Anders: Console check op compile errors.

**"Geen blauwe NavMesh na bake"** → Walkable meshes niet Navigation Static. Stap 2 redo met meer selecties.

**"Agent valt door vloer in Play mode"** → Vloer mist Collider. Selecteer vloer → Inspector → Add Component → Mesh Collider.

**"Te weinig deposits geplaatst (3/8)"** → NavMesh dekt te klein gebied. Bake eerst, zorg dat trappen op de NavMesh staan.

**"Console errors over null references in HeistController"** → Een veld in Inspector is leeg. Stap 9 sanity check redo.

**"ScriptedThief beweegt niet in Play mode"** → Thief NavMeshAgent niet op NavMesh. Plaats hem hoger of dichter bij NavMesh.

**"randomizeBounds box zichtbaar in scene?"** → Selecteer HeistController GameObject, dan zie je de gele draadbox. Niet zichtbaar betekent niet zichtbaar in Inspector → check randomizeBounds veld.
