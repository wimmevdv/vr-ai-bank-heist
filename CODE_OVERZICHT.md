# Code-overzicht — *Heist and Seek*

Dit document beschrijft elk C#-script in het project: doel, belangrijkste
methodes, en hoe het in het geheel past. De scripts zijn gegroepeerd per
verantwoordelijkheidsdomein.

## Inhoud

- [Player-laag (gameplay)](#player-laag-gameplay)
- [AI-laag (Wimme)](#ai-laag-wimme)
- [Beveiligings-laag (Renzo)](#beveiligings-laag-renzo)
- [Test-/debug-laag (Kean)](#test--debug-laag-kean)
- [Editor-tools](#editor-tools)

---

## Player-laag (gameplay)

Deze klassen regelen de overval zelf: timer, score, extraction-flow,
loot-objecten en de HUD.

### `HeistManager.cs`

Singleton die de hele heist-state beheert. Houdt de 5-minuten-timer bij,
trackt het totaalbedrag, telt hoeveel loot-items er afgeleverd zijn en
vuurt één event af bij game-einde.

- **Events** — `OnGameEnded(HeistEndInfo)` — wordt geabonneerd door
  `GameUI`.
- **`Awake()`** — standaard singleton-init (één `Instance` per scène,
  duplicaten worden vernietigd).
- **`Start()` → `InitializeObjects()`** — telt alle `LootItem`-objecten
  in de scène om het totale doel te kennen.
- **`Update()` → `ProcessCountdown()`** — telt elke frame af; bij 0
  roept `LoseGame("TIME EXPIRED")` aan.
- **`SecureLootItem(int value)`** — door `DropZone` aangeroepen wanneer
  buit wordt afgeleverd; verhoogt score en teller.
- **`TryExecuteExtraction(bool inSafeZone, AudioSource err)`** —
  validatie-poort vanuit `ExtractionButton`: speler moet in de safe-zone
  staan om te winnen.
- **`LoseGame(reason)` / `LoseCaught()` / `WinGame()`** — drie eind-paden;
  zetten `IsGameActive` op false en triggeren `FireEnded`.
- **`FireEnded(GameResult)`** — bouwt de `HeistEndInfo`-struct, vuurt
  het event en schakelt de bewaker uit via `DisableGuard()`.
- **`DisableGuard()`** — zoekt de actieve `BankGuardAgent`, schakelt de
  component uit en zet zijn `Rigidbody`-velocity op nul (anders blijft
  hij doorglijden).
- **`RestartGame()`** — herlaadt de actieve scène; aangeroepen vanuit
  de Play Again-knop.

### `LootItem.cs`

Markeert een 3D-object als grijpbaar buitstuk. Vereist een `Renderer`
(voor highlight) en een `XRGrabInteractable`. Bewaart een naam en een
geldbedrag dat `DropZone` aan de score doorgeeft bij aflevering.

- **`OnEnable()` / `OnDisable()`** — abonneert op `firstHoverEntered` en
  `lastHoverExited` van de grab-interactable.
- **`OnHoverEnter` / `OnHoverExit`** — wisselt het materiaal voor een
  emissive highlight-materiaal en terug.

### `DropZone.cs`

Trigger-zone waar afgeleverde loot in de score telt. Detecteert een
binnenkomende `LootItem` (of zoekt 'm via `GetComponentInParent`),
registreert de waarde bij `HeistManager`, speelt eventueel een
kassa-geluid en vernietigt het object.

- **`OnTriggerEnter(Collider)`** — entry-punt.
- **`SecureLoot(LootItem)`** — annuleert eerst een actieve XR-grab
  (anders blijft de speler een vernietigd object vasthouden), speelt
  feedback-geluid en `Destroy()`.

### `SafeZone.cs`

Trigger-zone die bijhoudt of de speler binnen de extractie-cirkel staat.
Houdt een publieke `IsPlayerInZone`-vlag bij die `ExtractionButton` uitleest.

### `ExtractionButton.cs`

Knop op de muur in de safe-zone. Gebruikt
`XRSimpleInteractable.firstSelectEntered` om een druk te detecteren,
vraagt `SafeZone` om de huidige status en geeft die door aan
`HeistManager.TryExecuteExtraction`.

### `HeistHUD.cs`

Polsband-Canvas dat geld en resterende tijd toont. Fadet alleen in
wanneer de speler er recht naar kijkt en het horloge dichtbij genoeg is
(default 1 m), zodat het niet stoort tijdens normaal spelen.

- **`Update()` → `UpdateText()` + `UpdateVisibility()`** — leest
  `HeistManager.Instance`, formatteert `mm:ss` en `€-prefix`, berekent of
  het canvas in de kijkrichting van de hoofdcamera ligt en past de
  alpha van de `CanvasGroup` aan met `MoveTowards`.
- **`LateUpdate()`** — billboard-rotatie zodat de tekst altijd recht op
  de camera staat (kan uitgezet worden).

### `PlayerFootsteps.cs`

Voetstap-systeem op de XR-Origin. Berekent uit de horizontale beweging
van de Origin per stap een snelheid en speelt random uit een lijst
clips af. Voor lange multi-step-opnames is er een slice-modus
(`clipPlayDuration`, `useRandomClipOffset`, `clipFadeOut`). Per stap
roept hij `NoiseEmitter.Emit(loudness)` aan, waardoor de bewaker de
speler kan horen.

- **`Update()`** — kijkt naar de horizontale positieverandering, telt
  een afstands-accumulator op en triggert `PlayFootstep` wanneer
  `stepDistance` overschreden wordt.
- **`PlayFootstep(speed)`** — kiest tussen `PlayOneShot` (volledige
  clip) of een handmatige `Play()` met `time`-offset gevolgd door
  `StopAfter`-coroutine.
- **`StopAfter(seconds, vol)`** — fade-out van het audio-volume zodat
  korte slices niet klikken bij het stoppen.

---

## AI-laag (Wimme)

De ML-Agents-bewaker en alles wat de trainings-omgeving rond hem opbouwt.

### `BankGuardAgent.cs` (Wimme.Test)

Het hart van het systeem. Erft van `Unity.MLAgents.Agent` en implementeert
de override-methodes die de PPO-trainer verwacht.

- **`Initialize()`** — pakt de `Rigidbody`, freezet rotatie op X- en
  Z-as om kantelen te voorkomen, en zet `MaxStep` op
  `maxStepsPerEpisode` (3 000) tijdens training of 0 (oneindig) tijdens
  live VR-gameplay.
- **`OnEpisodeBegin()`** — leest curriculum-parameters via
  `EnvironmentParameters`, herinitialiseert smoothing- en idle-tellers,
  spawnt de bewaker op `guardSpawn` met willekeurige yaw en roept
  `HeistEnvController.BeginEpisode(...)` aan. Reset ook de
  `last-known-position`-status.
- **`ReadCurriculumParams()`** — haalt `num_deposits`, `audio_on`,
  `alarms_on`, `thief_on`, `shaping` en alle `w_*`-rewardgewichten op,
  met defaults uit het Inspector-paneel als fallback.
- **`CollectObservations(VectorSensor)`** — voegt 13 basis-observaties
  toe (yaw, velocity, laatste geluid, speler-detectie, tijd) en in
  v7-modus 4 extra (verticale Y van speler, last-known-position
  geldig-vlag en X/Z). Werkt tegelijk de interne
  `lastKnownPlayerPos`-status bij als de speler op dit moment zichtbaar
  is.
- **`OnActionReceived(ActionBuffers)`** — leest de twee continue acties
  (move, turn), kent tijdstraf toe, beweegt- en idle-rewards, progress-
  shaping richting prioritair doel, zicht-reward, thief-proximity-shaping
  (exponentieel) en de vangst-bonus van +100 (eindigt de episode).
  Behandelt ook geluid-onderzoek-reward en de v7
  last-known-position-bonus.
- **`FixedUpdate()`** — past beweging toe op de `Rigidbody` met
  smoothing (`Lerp` van `pendingMove`/`pendingTurn` naar
  `smoothedMove`/`smoothedTurn`), projecteert de richting op het
  oppervlak via een downward raycast en wisselt tussen `patrolSpeed` en
  `chaseSpeed` afhankelijk van zicht op de speler. Tekent debug-lijnen
  naar speler en last-known-position.
- **`DistanceToPriorityTarget()`** — bepaalt het huidige doel volgens
  een prioriteits-keten: zichtbare speler > recente last-known-pos >
  recent luid geluid > gealarmeerde deposit > niets.
- **`TrySeePlayer(out localPos, out dist)`** — zicht-test: afstand < 20 m,
  hoek tot voorwaarts < 75°, geen muur tussen. Output is in lokale
  ruimte voor observaties.
- **`OnCollisionStay(Collision)`** — wandcontact-straf (−0,01).
- **`OnTriggerEnter` / `OnTriggerStay` → `TryCatch(Collider)`** —
  detecteert dat de bewaker de speler raakt en eindigt de episode met
  `GuardOutcome.Caught`.
- **`OnEnvironmentEnded(GuardOutcome)`** — afhandeling van environment-
  eindes (TimeUp → bonus per niet-gestolen deposit, AllStolen → optioneel
  verlies-reward).
- **`Heuristic(in ActionBuffers)`** — handmatige W/A/S/D-besturing voor
  debugging.

### `HeistEnvController.cs` (Wimme.Test)

De controller die rond de bewaker de "wereld" beheert: deposits,
tijd, alarms, distractor-geluiden en de scripted-thief.

- **State** — `deposits` (lijst met `DepositState`: t, stolen, alarmed,
  alarmEndsAt), `lastNoise` (`NoiseEvent`: position, loudness,
  timeEmitted, valid), `timeLeft`, `episodeOver`.
- **`Awake()`** — bouwt de `deposits`-lijst uit `depositSlots[]` en
  maakt een leeg `NoiseEvent`-object aan.
- **`BeginEpisode(activeCount, thief, audio, alarms)`** — reset
  timer, activeert de eerste N deposits, kiest eventueel een
  willekeurige NavMesh-positie per deposit (domein-randomisatie),
  selecteert één willekeurig deposit als gealarmeerd en spawnt de
  `ScriptedThief` op een willekeurige NavMesh-positie.
- **`Update()`** — telt timer af, verloopt alarms, gooit kans-baseerde
  distractor-noises uit, checkt eindcondities (TimeUp of AllStolen) en
  beëindigt de episode via `EndEpisode`.
- **`EndEpisode(GuardOutcome)`** — markeert episode als over en seint
  de bewaker via `OnEnvironmentEnded`.
- **`RegisterNoise(worldPos, loudness)`** — universele entry-point voor
  alle geluidsbronnen (alarm, voetstappen, distractor); update
  `lastNoise` zodat de agent het bij zijn volgende observatie meeneemt.
- **`FindNearestUntouched(from)`** — utility voor `ScriptedThief`.
- **`MarkStolen(depositT)`** — markeert een deposit als gestolen en
  geeft het door aan de bewaker (`OnItemStolen`).
- **`TrySampleNavMeshPoint(out pos)`** — 20-pogingen-loop om een
  geldige NavMesh-positie binnen `randomizeBounds` te vinden.

### `ScriptedThief.cs` (Wimme.Test)

NavMesh-gestuurde "dief" die de speler vervangt tijdens training.
Doorloopt een state-machine: kies dichtstbijzijnde niet-gestolen
deposit → loop ernaartoe (`GoSteal`) → wacht `stealSeconds` (`Stealing`)
→ markeer als gestolen → ren naar drop-off met `runMoveSpeed`
(`GoDrop`) → wacht `dropSeconds` (`Dropping`) → herhaal.

- **`ResetAt(pos, env)`** — veilige spawn die altijd op het NavMesh
  belandt door `NavMesh.SamplePosition` te gebruiken; voorkomt de
  "Failed to create agent because there is no valid NavMesh"-fout.
- **`Update()`** — schakelt tussen de vier state-ticks (`TickGoSteal`,
  `TickStealing`, `TickGoDrop`, `TickDropping`) en emit voetstap-noise
  proportioneel aan snelheid.

### `VRPlayerBridge.cs` (Wimme.Test)

Maakt van de menselijke VR-speler een geldige `thiefTarget` voor de
agent. Zet de juiste tag (`Player`), past de `CapsuleCollider` aan op
spelergrootte (hoogte 1,8 m, radius 0,6 m) en stuurt voetstap-noise
naar `HeistEnvController` op basis van XR-Origin-beweging met een
cooldown en drempelwaarde.

### `NavMeshRuntimeBootstrap.cs` (Wimme.Test)

Statische klasse met `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`.
Alleen actief in standalone-builds, niet in de Editor. Bouwt
ontbrekende NavMeshes opnieuw op en re-toggelt
`NavMeshAgent.enabled` van agents die nog niet aan het mesh hangen.
Bedoeld als reddingsboei voor stale NavMesh-data in headless trainings-
builds; het happy-path slaat dit over om de mlagents-timeout te
vermijden.

### `GuardAnimator.cs` (Wimme.Test)

Eenvoudige animator-driver op de visuele bewaker. Leest de horizontale
snelheid van de parent-`Rigidbody` en schrijft die naar de
`Speed`-parameter van de `Animator`, die in `GuardController.controller`
tussen idle-, walk- en run-animaties blend.

### `NoiseEmitter.cs` (Wimme)

Universeel component dat een geluid-event naar de `HeistEnvController`
stuurt. Twee gebruiksmodi:

1. **Handmatig** — `Emit()` of `Emit(loudness)` aanroepen via code of
   `UnityEvent`.
2. **Auto** — gekoppelde `AudioSource` wordt gemonitord; elke keer dat
   `isPlaying` van false naar true gaat, wordt eenmalig een event
   gestuurd.

### `HeistEndBridge.cs` (Wimme)

Brug tussen de AI-laag en de gameplay-laag. Polt elke `Update`
`env.episodeOver`; bij overgang van false naar true en met outcome
`Caught` roept hij `HeistManager.Instance.LoseCaught()` aan. Daardoor
hoeft `HeistEnvController` geen directe referentie naar `HeistManager`
te kennen.

### `GameUI.cs` (Wimme)

UI-controller voor het eindscherm. Abonneert op
`HeistManager.OnGameEnded`, schakelt het `endPanel` aan, plaatst het
canvas vóór de speler, vult titel ("ESCAPE GELUKT" / "TIJD VOORBIJ" /
"BETRAPT") en samenvatting (verdiend bedrag, buit, resterende tijd),
en heeft een optionele `autoRestartSeconds` als VR-knop-fallback.

- **`SubscribeWhenReady()`** — coroutine die wacht tot
  `HeistManager.Instance` bestaat voordat het event-abonnement geplaatst
  wordt.
- **`OnPlayAgainClicked()`** — roept `HeistManager.RestartGame()` aan,
  met een scene-reload-fallback.
- **`SnapInFrontOfPlayer()`** — verplaatst en oriënteert het canvas
  voor de hoofdcamera bij het tonen.

---

## Beveiligings-laag (Renzo)

Deuren, keycards en VR-spawn-veiligheid.

### `DoorController.cs`

Eenvoudige scharnier-deur. Bewaart begin-rotatie, berekent doel-rotatie
(`+ openAngle` rond Y), en `Slerp`t elke `Update` naar de open-rotatie
zodra `OpenDoor()` is aangeroepen.

### `VaultDoorController.cs`

Geavanceerde kluisdeur met aanpasbaar lokaal scharnier (`hingeAnchor`,
`hingeAxis`) en open-vertraging. `OpenAutomatically()` start een
coroutine die de deur in graden-per-seconde stappen rond de wereld-as
roteert via `transform.RotateAround`. Behoudt de initiële `Rigidbody`-
en `HingeJoint`-instellingen zodat fysica geen tegenkracht oplevert.

### `KeyCard.cs`

XR-grijpbare kaart met identificatie via een `keyId`-string.

- **`Configure()`** — dwingt fysica-instellingen af (massa, gravity,
  continue collision-detectie) en grab-instellingen
  (`VelocityTracking`, `smoothPosition/Rotation`) zodat de kaart in VR
  voorspelbaar reageert.
- **`OnGrab(args)`** — schakelt optioneel de manipulation-input op de
  XR-interactor uit zolang de kaart vastzit (anders kan de speler hem
  per ongeluk wegduwen met de joystick).
- **`OnRelease(args)`** — herstelt de manipulation-input.
- **`FixedUpdate()`** — controleert of de fysica-positie van de kaart
  niet verder dan `maxHoldDistance` van de hand-attach afdwaalt (denk
  aan abrupt klemmen tegen geometrie); zo ja, klemt hij de positie
  terug.

### `CardScanner.cs`

Trigger-zone die luistert op een `KeyCard` die er doorheen geveegd
wordt. Vergelijkt `keyId` met `acceptedKeyId`; bij match speelt het
scan-geluid af, opent de gekoppelde `VaultDoorController` en vuurt het
`onAccepted`-UnityEvent. Anders `onRejected`. Met `oneShot = true`
werkt elke scanner één keer.

### `VRSpawnStabilizer.cs`

Beschermt tegen een veelvoorkomende VR-bug: de speler-`CharacterController`
valt door de vloer wanneer Unity start vóór de headset-tracking actief is.
Schakelt de `CharacterController` op frame 1 uit, wacht in een coroutine
tot de camera's lokale Y-positie boven 0,5 m komt (dus echt opgezet),
geeft de capsule nog 0,25 s tijd om naar de echte spelergrootte uit te
rekken en activeert pas dan de fysica weer.

---

## Test-/debug-laag (Kean)

Kleine utility-scripts voor desktop-testing.

### `PlayerMovement.cs`

Standaard WASD + muis-look-controller voor desktop-testing zonder
headset. Gebruikt `CharacterController` met sprint (LeftShift),
spring (Space) en zwaartekracht. Niet gebruikt in de finale VR-build.

### `CollisionLogger.cs`

Debug-helper die `Debug.Log` schrijft bij elke `OnCollisionEnter` en
`OnTriggerEnter`, met naam en tag van het andere object. Plaats op een
spelerobject om botsing/trigger-flow te volgen.

### `PlayerThiefController.cs` (Wimme.Test)

Een desktop-alternatief voor de VR-speler tijdens training-debugging:
WASD + muiskijk-controller die `ScriptedThief` en `NavMeshAgent`
uitschakelt, een eigen camera aanmaakt en de cursor lockt. Hiermee
kunnen ontwerpers de bewaker tegen een menselijk bestuurde tegenstander
testen zonder headset.

---

## Editor-tools

Wizards die in de Unity-Editor menu's verschijnen om herhalend
opzet-werk te automatiseren.

### `DepositClusterTool.cs` (Wimme.EditorTools)

Menu: **Tools → Bank Heist → Cluster Deposits**.

Vindt alle GameObjects met tag `Deposit` en groepeert ze met een
greedy nearest-neighbor-algoritme (`BuildClusters`) op basis van een
`clusterRadius` (default 3 m). Per cluster maakt het een parent-
GameObject met tag `Deposit` en een trigger-`BoxCollider` aangepast op
de gecombineerde bounds, plus configureerbare padding. Originele
deposits worden onder de parent gehangen en zelf van hun tag ontdaan.
Bevat een dry-run-knop voor preview zonder wijzigingen. Gebruikt
`Undo.IncrementCurrentGroup` zodat één Ctrl+Z het hele proces
terugdraait.

### `LootItemAssignerTool.cs` (Wimme.EditorTools)

Menu: **Tools → Bank Heist → Assign LootItems**.

Bulk-toevoegen van `BoxCollider` + `Rigidbody` + `XRGrabInteractable` +
`LootItem` aan kandidaat-objecten. Drie targeting-modi
(`ChildrenOfDepositGroups`, `AllSelectedGameObjects`,
`AllObjectsWithDepositTag`) en drie waarde-strategieën
(`RandomRange`, `BySize`, `ByNameKeyword`). De keyword-mode kent
fracties van het min-/max-bereik toe op basis van trefwoorden in de
naam (`statue`, `vault`, `gun`, `box`, …). Bevat een fix voor de
veelvoorkomende non-convex MeshCollider + Rigidbody-warning door
bestaande MeshColliders automatisch convex te zetten.

### `HeistTrainingSetup.cs` (Wimme.EditorTools)

Menu: **Heist Training → 1. Create Training Rig** (+ 2, 3, 4).

Een vier-staps-wizard om vanaf een lege scène een werkende
trainings-omgeving op te zetten:

1. **Create Training Rig** — bouwt het `HeistTrainingRig`-GameObject met
   `HeistEnvController`, `GuardSpawn`, drie `ThiefSpawn`s, een
   `DropOffZone`, lege `Deposits`- en `DistractorPoints`-parents. Maakt
   een `BankGuardAgent` (Cube-primitive met `BehaviorParameters`,
   `DecisionRequester`, `RayPerceptionSensorComponent3D` en de
   `BankGuardAgent`-script) en een `ScriptedThief` (Capsule met
   `NavMeshAgent`). Wired alle referenties via `SerializedObject` zodat
   de Inspector klopt zonder handwerk.
2. **Add 8 Deposits at random NavMesh points** — sampled 8 NavMesh-
   posities binnen ±25 m van de origin en plaatst Cube-deposits met
   de juiste tag en trigger-collider. Hangt ze automatisch onder
   `HeistEnvController.depositSlots[]`.
3. **Tag Selected Objects as Wall** — voegt de `Wall`-tag toe aan
   geselecteerde objecten en al hun child-colliders.
4. **Mark Selected as Navigation Static** — zet recursief de
   `NavigationStatic`-vlag, klaar voor NavMesh-bake.

### `HeadlessBuild.cs` (Wimme.EditorTools)

CLI-entrypoint dat vanuit de command line aangeroepen kan worden:

```text
Unity.exe -batchmode -quit -nographics ^
  -projectPath C:\VR ^
  -executeMethod Wimme.EditorTools.HeadlessBuild.BuildTraining
```

Maakt een Windows-64 standalone-build van de trainings-scène
(`kean_scene_Training2.unity`) naar `Builds/VR_project.exe`. Logt de
build-summary en sluit Unity af met exit-code 0 (succes) of 1 (fout)
zodat de aanroepende shell het resultaat kan controleren. Gebruikt
tijdens overnight-trainingen om een verse build te genereren voordat
de trainer start.

---

## Samenhang in één plaatje

> **[AFBEELDING: Diagram met `XR Origin` → `PlayerFootsteps` →
> `NoiseEmitter` → `HeistEnvController.lastNoise` → `BankGuardAgent.
> CollectObservations`. Parallel: `BankGuardAgent.OnTriggerEnter
> (Player)` → `HeistEnvController.EndEpisode(Caught)` →
> `HeistEndBridge` → `HeistManager.LoseCaught` → `OnGameEnded` →
> `GameUI`. Renzo's scanners/deuren als zijtak: `KeyCard` →
> `CardScanner` → `VaultDoorController`.]**

De drie lussen die het systeem definiëren zijn:

- **Speler → Loot → Score** — `LootItem` → `DropZone` →
  `HeistManager.SecureLootItem` → `HeistHUD`.
- **Speler → Geluid → AI** — `PlayerFootsteps`/`VRPlayerBridge` →
  `NoiseEmitter` → `HeistEnvController.RegisterNoise` →
  `BankGuardAgent.CollectObservations`.
- **AI → Vangst → Game-einde** — `BankGuardAgent.TryCatch` →
  `HeistEnvController.EndEpisode(Caught)` → `HeistEndBridge` →
  `HeistManager.LoseCaught` → `GameUI.ShowEndScreen`.
