using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Wimme.Test;

namespace Wimme.EditorTools
{
    /// <summary>
    /// One-click Unity Editor helpers for wiring a training rig into an existing
    /// scene (e.g. kean_scene). Menu items appear under "Heist Training".
    /// </summary>
    public static class HeistTrainingSetup
    {
        // -----------------------------------------------------------------
        // 1. Create the training rig (Controller + Agent + Thief + spawns).
        // -----------------------------------------------------------------
        [MenuItem("Heist Training/1. Create Training Rig")]
        public static void CreateTrainingRig()
        {
            EnsureTagsExist();

            var root = new GameObject("HeistTrainingRig");

            // ---- HeistEnvController ----
            var ctrlGo = new GameObject("HeistController");
            ctrlGo.transform.SetParent(root.transform);
            var ctrl = ctrlGo.AddComponent<HeistEnvController>();

            // ---- GuardSpawn marker ----
            var guardSpawn = new GameObject("GuardSpawn");
            guardSpawn.transform.SetParent(root.transform);
            guardSpawn.transform.position = Vector3.zero;

            // ---- ThiefSpawns parent + 3 markers ----
            var thiefSpawnsRoot = new GameObject("ThiefSpawns");
            thiefSpawnsRoot.transform.SetParent(root.transform);
            var thiefSpawnList = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var t = new GameObject($"ThiefSpawn_{i + 1}");
                t.transform.SetParent(thiefSpawnsRoot.transform);
                t.transform.position = new Vector3((i - 1) * 3f, 0f, -3f);
                thiefSpawnList.Add(t.transform);
            }

            // ---- DropOff (van outside) ----
            var dropOff = new GameObject("DropOffZone");
            dropOff.transform.SetParent(root.transform);
            dropOff.transform.position = new Vector3(0f, 0f, -10f);

            // ---- Deposits parent (empty — user adds children via menu 2) ----
            var depositsRoot = new GameObject("Deposits");
            depositsRoot.transform.SetParent(root.transform);

            // ---- DistractorPoints parent (empty, optional) ----
            var distractors = new GameObject("DistractorPoints");
            distractors.transform.SetParent(root.transform);

            // ---- BankGuardAgent ----
            var guard = CreateBankGuardAgent(root.transform);
            // ---- ScriptedThief ----
            var thief = CreateScriptedThief(root.transform);

            // ---- Wire references via SerializedObject ----
            var soCtrl = new SerializedObject(ctrl);
            soCtrl.FindProperty("guardSpawn").objectReferenceValue = guardSpawn.transform;
            soCtrl.FindProperty("dropOffZone").objectReferenceValue = dropOff.transform;
            soCtrl.FindProperty("guard").objectReferenceValue = guard;
            soCtrl.FindProperty("thief").objectReferenceValue = thief;
            var thiefSpawnsProp = soCtrl.FindProperty("thiefSpawns");
            thiefSpawnsProp.arraySize = thiefSpawnList.Count;
            for (int i = 0; i < thiefSpawnList.Count; i++)
                thiefSpawnsProp.GetArrayElementAtIndex(i).objectReferenceValue = thiefSpawnList[i];
            soCtrl.ApplyModifiedPropertiesWithoutUndo();

            // Guard.env reference
            var soGuard = new SerializedObject(guard);
            soGuard.FindProperty("env").objectReferenceValue = ctrl;
            soGuard.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[HeistTrainingSetup] Created HeistTrainingRig. " +
                      "Next: position GuardSpawn + ThiefSpawn_X in your scene, add Deposit cubes (menu 2), tag walls (menu 3), bake NavMesh.");
        }

        private static BankGuardAgent CreateBankGuardAgent(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BankGuardAgent";
            go.transform.SetParent(parent);
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            go.tag = "Bewaker";

            // Visible blue material so user can spot it
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/bleu.mat");

            // BoxCollider removed — CharacterController has its own built-in capsule collider.

            // CharacterController — built-in step climbing (stepOffset=0.75) handles
            // stairs automatically without ramp colliders. Replaces Rigidbody.
            Object.DestroyImmediate(go.GetComponent<BoxCollider>()); // CC has its own capsule
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1f, 0f); // feet at transform.position
            cc.stepOffset = 0.75f;
            cc.slopeLimit = 60f;
            cc.skinWidth = 0.08f;

            // BehaviorParameters
            var bp = go.AddComponent<BehaviorParameters>();
            bp.BehaviorName = "BankGuardAgent";
            bp.BrainParameters.VectorObservationSize = 13;
            bp.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeContinuous(2);
            bp.BehaviorType = BehaviorType.Default;
            bp.UseChildSensors = true;
            bp.UseChildActuators = true;

            // DecisionRequester
            var dr = go.AddComponent<Unity.MLAgents.DecisionRequester>();
            dr.DecisionPeriod = 5;
            dr.TakeActionsBetweenDecisions = true;

            // Ray Perception Sensor — Wall/Player/Deposit tags, 6 rays/dir
            var ray = go.AddComponent<RayPerceptionSensorComponent3D>();
            ray.SensorName = "RayPerceptionSensor";
            ray.DetectableTags = new List<string> { "Wall", "Player", "Deposit" };
            ray.RaysPerDirection = 6;
            ray.MaxRayDegrees = 60;
            ray.SphereCastRadius = 0.5f;
            ray.RayLength = 20;
            ray.RayLayerMask = ~0;

            // BankGuardAgent script
            var agent = go.AddComponent<BankGuardAgent>();
            return agent;
        }

        private static ScriptedThief CreateScriptedThief(Transform parent)
        {
            // Capsule primitive — auto-adds CapsuleCollider with the right shape.
            // Capsule visual + trigger collider matches the prefab the v3/v4 policies
            // were trained against: the agent's OnTriggerEnter fires on overlap.
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ScriptedThief";
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(0f, 0f, -3f);
            go.tag = "Player";

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/red.mat");

            var cc = go.GetComponent<CapsuleCollider>();
            cc.isTrigger = true;
            cc.radius = 0.5f;
            cc.height = 2f;
            cc.center = Vector3.zero;

            var nav = go.AddComponent<NavMeshAgent>();
            nav.radius = 0.4f;
            nav.height = 1.8f;
            nav.speed = 3.5f;
            nav.acceleration = 8f;
            nav.angularSpeed = 360f;

            var thief = go.AddComponent<ScriptedThief>();
            return thief;
        }

        // -----------------------------------------------------------------
        // 2. Place N deposit cubes at random NavMesh points.
        // -----------------------------------------------------------------
        [MenuItem("Heist Training/2. Add 8 Deposits at random NavMesh points")]
        public static void AddRandomDeposits()
        {
            var depositsRoot = GameObject.Find("HeistTrainingRig/Deposits");
            if (depositsRoot == null)
            {
                EditorUtility.DisplayDialog("Missing rig",
                    "Run 'Heist Training → 1. Create Training Rig' first.", "OK");
                return;
            }

            var goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/gold.mat");
            int placed = 0, attempts = 0;
            const int wanted = 8;

            // Sample within a 50m cube around scene origin — adjust if your scene
            // is offset elsewhere by moving the cubes in the scene afterwards.
            while (placed < wanted && attempts < 400)
            {
                attempts++;
                Vector3 sample = new Vector3(Random.Range(-25f, 25f), 1f, Random.Range(-25f, 25f));
                if (NavMesh.SamplePosition(sample, out var hit, 5f, NavMesh.AllAreas))
                {
                    var dep = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dep.name = $"Deposit_{placed + 1:00}";
                    dep.tag = "Deposit";
                    dep.transform.SetParent(depositsRoot.transform);
                    dep.transform.position = hit.position + Vector3.up * 0.25f;
                    dep.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                    var dc = dep.GetComponent<BoxCollider>();
                    dc.isTrigger = true;

                    if (goldMat) dep.GetComponent<MeshRenderer>().sharedMaterial = goldMat;
                    placed++;
                }
            }

            // Wire deposits into HeistEnvController
            var ctrl = GameObject.Find("HeistTrainingRig/HeistController").GetComponent<HeistEnvController>();
            var so = new SerializedObject(ctrl);
            var slotsProp = so.FindProperty("depositSlots");
            slotsProp.arraySize = placed;
            for (int i = 0; i < placed; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    depositsRoot.transform.GetChild(i);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HeistTrainingSetup] Placed {placed} deposits and wired them into HeistController. " +
                      "Move them around manually if they ended up in awkward spots.");

            if (placed < wanted)
                EditorUtility.DisplayDialog("Few NavMesh points",
                    $"Only placed {placed}/{wanted} deposits — your NavMesh is small or not baked yet. " +
                    "Bake NavMesh first, then run this again or move deposits manually.", "OK");
        }

        // -----------------------------------------------------------------
        // 3. Mass-tag selected GameObjects as Wall.
        // -----------------------------------------------------------------
        [MenuItem("Heist Training/3. Tag Selected Objects as Wall")]
        public static void TagSelectedAsWall()
        {
            EnsureTagsExist();
            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                go.tag = "Wall";
                count++;
                // Also tag every collider child so rays detect the actual mesh
                foreach (var col in go.GetComponentsInChildren<Collider>(true))
                    if (col.gameObject != go) { col.tag = "Wall"; count++; }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HeistTrainingSetup] Tagged {count} objects + child colliders as Wall.");
        }

        // -----------------------------------------------------------------
        // 4. Mark Selected (and children) as Navigation Static.
        // -----------------------------------------------------------------
        [MenuItem("Heist Training/4. Mark Selected as Navigation Static (for NavMesh bake)")]
        public static void MarkNavigationStatic()
        {
            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                MarkRecursive(go);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HeistTrainingSetup] Marked {count} roots (+ children) as Navigation Static. Now open Window → AI → Navigation → Bake.");
        }

        private static void MarkRecursive(GameObject go)
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            flags |= StaticEditorFlags.NavigationStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            foreach (Transform child in go.transform)
                MarkRecursive(child.gameObject);
        }

        // -----------------------------------------------------------------
        // Tag helpers
        // -----------------------------------------------------------------
        private static void EnsureTagsExist()
        {
            EnsureTag("Wall");
            EnsureTag("Deposit");
            EnsureTag("Bewaker");
        }

        private static void EnsureTag(string tag)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return;
            var so = new SerializedObject(asset[0]);
            var tagsProp = so.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
            tagsProp.arraySize++;
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            so.ApplyModifiedProperties();
        }
    }
}
