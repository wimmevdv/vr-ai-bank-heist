# Kean Scene → AI Training Setup Checklist

Doel: kean_scene voorbereiden zodat ik daar v5 in kan trainen. Geschatte tijd: **60-90 min handmatig werk**.

> **Voor je begint:** ik heb een editor wizard toegevoegd onder menu **"Heist Training"** (verschijnt in Unity menu bar bovenaan). Die doet 80% van het werk in 4 klikken.

---

## STAP 1 — Maak een training-kopie van de scene (5 min)

> ⚠️ NOOIT direct in kean_scene werken. Maak een kopie.

1. In Unity Project window → klap `Assets/Scenes` open
2. Rechtsklik op `kean_scene.unity` → **Duplicate** (of Ctrl+D)
3. Rename de kopie naar `kean_scene_AItraining`
4. Dubbelklik om die te openen
5. Je werkt vanaf hier in `kean_scene_AItraining`. Originele kean_scene blijft onaangetast.

---

## STAP 2 — Mark walkable surfaces as Navigation Static (10-15 min)

NavMesh kan alleen baken over objecten die "Navigation Static" zijn.

1. In Hierarchy → selecteer alle **vloeren, trappen, gangen** van de bank (gebruik Ctrl+klik voor multi-select). Tip: als alle vloeren een parent hebben zoals "Floors" of "Geometry", selecteer dan alleen die parent — recursive.
2. Menu boven: **Heist Training → 4. Mark Selected as Navigation Static**
3. Console toont "Marked X roots as Navigation Static"

> Twijfel? Selecteer royaal — meer marken kan geen kwaad. Walls hoeven NIET static gemarkeerd, ze worden automatisch obstakel.

---

## STAP 3 — Bake NavMesh (3 min)

1. Menu: **Window → AI → Navigation**
2. Tabblad **Bake** (rechtsboven in het Navigation panel)
3. Klik **Bake** knop onderaan
4. Wacht (kan 1-2 min duren voor zo'n groot model)
5. Controle: na bake zie je een blauwe overlay op alle walkable vloeren (in Scene view, met Navigation panel open)

> ⚠️ Géén blauw te zien? Dan zijn er geen Navigation Static objecten of de bake heeft geen vlakke surfaces gevonden. Re-stap 2 en probeer opnieuw.

---

## STAP 4 — Tag de muren (15-20 min)

De ray sensor van de agent detecteert muren via de "Wall" tag. Zonder tags ziet de agent leegte.

1. Selecteer alle **muur-, kolom-, en gevel-objecten** in Hierarchy (Ctrl+klik). Tip: als je een parent hebt zoals "Walls" of "Architecture" → die volstaat.
2. Menu: **Heist Training → 3. Tag Selected Objects as Wall**
3. Console toont aantal getaggde objecten.

> Trappen → tag NIET als Wall, anders ziet agent ze als obstakel. Trap-meshes mogen "Untagged" blijven.
>
> Tafels/meubels: tag als Wall als je niet wilt dat de agent erover kan. Anders Untagged laten.

---

## STAP 5 — Plaats de Training Rig (1 klik, 1 min)

Menu: **Heist Training → 1. Create Training Rig**

Dit maakt automatisch:
- `HeistTrainingRig` (lege root parent)
  - `HeistController` (met HeistEnvController script)
  - `GuardSpawn` (markering)
  - `ThiefSpawns/ThiefSpawn_1..3`
  - `DropOffZone`
  - `Deposits` (parent, leeg)
  - `DistractorPoints` (parent, leeg, optioneel)
  - `BankGuardAgent` (blauwe cube met alle ML-Agents componenten)
  - `ScriptedThief` (rode cube met NavMeshAgent)

Alle references tussen deze GameObjects zijn al gewired ✓

---

## STAP 6 — Positioneer de markers (10 min)

Verplaats deze GameObjects naar logische plekken in jouw bank:

| GameObject | Waar | Tip |
|---|---|---|
| `GuardSpawn` | Waar de bewaker start, bv. receptie of upper floor center | Ergens centraal/strategisch |
| `BankGuardAgent` | Zelfde plek als GuardSpawn (visueel) | Wordt automatisch hergeplaatst per episode |
| `ThiefSpawn_1/2/3` | 3 verschillende plekken: bv. buitenplaats, eerste verdieping, basement | Per episode wordt random 1 gekozen |
| `ScriptedThief` | Zelfde plek als ThiefSpawn_1 (visueel) | Wordt herpositioneerd per episode |
| `DropOffZone` | Bij het busje buiten | Niet kritisch, kan ergens neutraals |

**Stappen om te verplaatsen:**
1. Klik object in Hierarchy
2. Inspector → Transform → Position → vul X/Y/Z in
3. OF gebruik scene view: klik object, druk W voor move tool, sleep

> Y-coordinate: zorg dat alle markers OP de vloer zitten (Y ≈ vloer-hoogte). Niet door de vloer.

---

## STAP 7 — Plaats deposits (1 klik + handmatig verplaatsen, 10-15 min)

Menu: **Heist Training → 2. Add 8 Deposits at random NavMesh points**

Dit dropt 8 gouden cubes op random NavMesh-punten. Ze hebben automatisch:
- Tag `Deposit`
- BoxCollider met `isTrigger=true`
- Gouden materiaal

**Daarna handmatig:**
- Loop door de Hierarchy onder `Deposits/`
- Verplaats elke deposit naar een logische plek (vault, kluis, kassa, kantoor, etc.)
- Let op: blijf op vloeren waar NavMesh ligt (blauwe overlay)
- Spreid ze over: buitenplaats(0-1), grond verdieping (2-3), eerste verdieping (2-3), basement vault (2)

---

## STAP 8 — Sanity checks (5 min)

Klik op `HeistController` GameObject. Inspector → HeistEnvController. Controleer:

| Veld | Moet zijn |
|---|---|
| Guard Spawn | GuardSpawn (Transform) |
| Thief Spawns | Size 3, met 3 ThiefSpawn objects |
| Drop Off Zone | DropOffZone |
| Guard | BankGuardAgent |
| Thief | ScriptedThief |
| Deposit Slots | Size 8 (of hoeveel je hebt) |

Klik op `BankGuardAgent` GameObject. Inspector → BankGuardAgent script. Controleer:
| Veld | Moet zijn |
|---|---|
| Env | HeistController (van HeistEnvController script) |

Klik op `BankGuardAgent` GameObject. Inspector → Behavior Parameters. Controleer:
| Veld | Moet zijn |
|---|---|
| Behavior Name | BankGuardAgent |
| Vector Observation Size | 13 |
| Model | (leeg — voor training) |
| Behavior Type | Default |

---

## STAP 9 — Save (1 min)

**File → Save** (Ctrl+S). Sluit Unity Editor daarna volledig (anders kan ik niet builden).

---

## STAP 10 — Laat het me weten

Als alle 9 stappen klaar zijn, schrijf "klaar". Ik:
1. Update training scripts om `kean_scene_AItraining` te targeten
2. Doe een headless rebuild
3. Smoketest 100k steps (5 min) — verifieer dat thief beweegt en alles werkt
4. Start full v5 training overnacht

---

## Troubleshooting

**"Heist Training menu staat er niet"** → Unity is bezig met compileren. Wacht 30s op script reload.

**"Geen NavMesh blauw zichtbaar na bake"** → walkable objects niet Navigation Static gemarkeerd. Stap 2 opnieuw met meer selecties.

**"Agent valt door vloer bij play"** → vloer mist Collider. Selecteer vloer → Add Component → MeshCollider.

**"Te weinig deposits geplaatst (3 van 8)"** → NavMesh is klein. Bake eerst de hele bank.

**Console errors over missing references** → ergens in HeistController is een veld leeg. Loop stap 8 nog eens door.
