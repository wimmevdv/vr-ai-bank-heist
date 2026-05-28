using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Wimme.EditorTools
{
    /// <summary>
    /// Bulk-assigner voor LootItem-componenten. Voor elke kandidaat (object met
    /// Renderer op zichzelf):
    ///   1. Add BoxCollider (size = mesh.bounds) als geen collider bestaat
    ///   2. Convert non-convex MeshCollider naar convex (anders Rigidbody-warning)
    ///   3. Add Rigidbody (mass + useGravity)
    ///   4. Add XRGrabInteractable
    ///   5. Add LootItem met naam + waarde
    /// Slaat objecten zonder Renderer of met bestaande LootItem over.
    ///
    /// Aanroepen: Tools > Bank Heist > Assign LootItems
    /// </summary>
    public class LootItemAssignerTool : EditorWindow
    {
        public enum TargetMode
        {
            ChildrenOfDepositGroups,  // alle children onder Deposit_Group_* parents
            AllSelectedGameObjects,   // alleen wat je in Hierarchy hebt geselecteerd
            AllObjectsWithDepositTag  // alles met tag Deposit (zonder clustering)
        }

        public enum ValueMode
        {
            RandomRange,
            BySize,           // groter object = hogere waarde
            ByNameKeyword     // mapping op trefwoorden in naam
        }

        private TargetMode targetMode = TargetMode.ChildrenOfDepositGroups;
        private ValueMode valueMode = ValueMode.ByNameKeyword;
        private int minValue = 500;
        private int maxValue = 5000;
        private bool skipExisting = true;
        private bool addColliderIfMissing = true;
        private float rigidbodyMass = 0.5f;
        private string parentNamePrefix = "Deposit_Group_";
        private Material highlightMaterial;
        private string lastReport = "";

        [MenuItem("Tools/Bank Heist/Assign LootItems")]
        public static void ShowWindow()
        {
            GetWindow<LootItemAssignerTool>("LootItem Assigner");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Bulk LootItem-component toevoegen", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Voegt Collider (als ontbreekt) + Rigidbody + XRGrabInteractable + LootItem toe.\n" +
                "Veilig om meermaals te runnen — bestaande componenten worden niet overschreven.",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            targetMode = (TargetMode)EditorGUILayout.EnumPopup("Welke objecten?", targetMode);
            if (targetMode == TargetMode.ChildrenOfDepositGroups)
                parentNamePrefix = EditorGUILayout.TextField("Group name prefix", parentNamePrefix);

            skipExisting = EditorGUILayout.Toggle(
                new GUIContent("Bestaande LootItems overslaan", "Niet overschrijven."),
                skipExisting);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            addColliderIfMissing = EditorGUILayout.Toggle(
                new GUIContent("BoxCollider toevoegen als geen Collider bestaat",
                    "Auto-sized op mesh.bounds. Nodig voor grab + AI-detectie."),
                addColliderIfMissing);
            rigidbodyMass = EditorGUILayout.Slider("Rigidbody mass", rigidbodyMass, 0.1f, 5f);
            highlightMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Highlight material", "Emissive mat voor hover-feedback. Leeg = geen highlight."),
                highlightMaterial, typeof(Material), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waarde-bepaling", EditorStyles.boldLabel);
            valueMode = (ValueMode)EditorGUILayout.EnumPopup("Strategy", valueMode);
            EditorGUILayout.BeginHorizontal();
            minValue = EditorGUILayout.IntField("Min waarde €", minValue);
            maxValue = EditorGUILayout.IntField("Max waarde €", maxValue);
            EditorGUILayout.EndHorizontal();

            if (valueMode == ValueMode.ByNameKeyword)
            {
                EditorGUILayout.HelpBox(
                    "Naam-trefwoorden → fractie van [min..max]:\n" +
                    "• statue/lisa/monet/painting/david → 100% (kunst)\n" +
                    "• trezor/safe/vault → 80% (kluizen)\n" +
                    "• rifle/gun/weapon → 60%\n" +
                    "• box/case/sci-fi → 40%\n" +
                    "• anders → random 0-50%",
                    MessageType.None);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Voorbeeld telling (dry run)"))
                DryRun();

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Assign Now"))
            {
                if (EditorUtility.DisplayDialog(
                    "Assign LootItems",
                    "Dit voegt componenten toe aan kandidaten. Doorgaan?",
                    "Ja, assign",
                    "Annuleer"))
                {
                    Run();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Laatste resultaat:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(lastReport) ? "(nog niets uitgevoerd)" : lastReport, MessageType.None);
        }

        private GameObject[] FindCandidates()
        {
            var result = new List<GameObject>();

            switch (targetMode)
            {
                case TargetMode.AllSelectedGameObjects:
                    result.AddRange(Selection.gameObjects);
                    break;

                case TargetMode.AllObjectsWithDepositTag:
                {
                    var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    foreach (var go in all)
                        if (go != null && go.CompareTag("Deposit"))
                            result.Add(go);
                    break;
                }

                case TargetMode.ChildrenOfDepositGroups:
                {
                    var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    foreach (var go in all)
                    {
                        if (go == null) continue;
                        if (!go.name.StartsWith(parentNamePrefix)) continue;
                        foreach (Transform child in go.transform)
                            result.Add(child.gameObject);
                    }
                    break;
                }
            }

            // Hard filter: LootItem.RequireComponent(Renderer) — Renderer MOET op same GameObject staan
            var filtered = new List<GameObject>();
            foreach (var go in result)
            {
                if (go == null) continue;
                if (skipExisting && go.GetComponent<LootItem>() != null) continue;
                if (go.GetComponent<Renderer>() == null) continue;
                filtered.Add(go);
            }
            return filtered.ToArray();
        }

        private void DryRun()
        {
            var candidates = FindCandidates();
            int withCollider = 0;
            int withRigidbody = 0;
            foreach (var c in candidates)
            {
                if (c.GetComponent<Collider>() != null) withCollider++;
                if (c.GetComponent<Rigidbody>() != null) withRigidbody++;
            }
            lastReport = $"Dry run:\n" +
                         $"• {candidates.Length} kandidaten (met Renderer, zonder bestaande LootItem)\n" +
                         $"• {withCollider} hebben al een Collider\n" +
                         $"• {withRigidbody} hebben al een Rigidbody\n" +
                         $"• min €{minValue} — max €{maxValue} (mode: {valueMode})";
            Debug.Log("[LootItemAssignerTool] " + lastReport.Replace("\n", " | "));
        }

        private void Run()
        {
            var candidates = FindCandidates();
            if (candidates.Length == 0)
            {
                lastReport = "Geen kandidaten gevonden. Check target-mode + zorg dat objecten een Renderer hebben.";
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Assign LootItems");

            int assigned = 0;
            int totalValue = 0;
            int colliderAdded = 0;
            int meshColliderFixed = 0;

            foreach (var go in candidates)
            {
                if (go == null) continue;

                // 1. Collider — handle 3 cases
                var existingCol = go.GetComponent<Collider>();
                if (existingCol == null)
                {
                    if (addColliderIfMissing) AddBoxColliderFromRenderer(go, out _);
                    colliderAdded++;
                }
                else if (existingCol is MeshCollider mc && !mc.convex)
                {
                    // Non-convex MeshCollider + Rigidbody is verboden → fix
                    Undo.RecordObject(mc, "Make MeshCollider convex");
                    mc.convex = true;
                    meshColliderFixed++;
                }

                // 2. Rigidbody
                if (!go.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb = Undo.AddComponent<Rigidbody>(go);
                    rb.mass = rigidbodyMass;
                    rb.useGravity = true;
                }

                // 3. XRGrabInteractable
                if (!go.TryGetComponent<XRGrabInteractable>(out var _))
                {
                    Undo.AddComponent<XRGrabInteractable>(go);
                }

                // 4. LootItem
                var loot = Undo.AddComponent<LootItem>(go);
                int value = ComputeValue(go);
                loot.itemName = PrettifyName(go.name);
                loot.monetaryValue = value;

                if (highlightMaterial != null)
                {
                    var so = new SerializedObject(loot);
                    var prop = so.FindProperty("highlightMaterial");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = highlightMaterial;
                        so.ApplyModifiedProperties();
                    }
                }

                assigned++;
                totalValue += value;
                EditorUtility.SetDirty(go);
            }

            Undo.CollapseUndoOperations(undoGroup);

            lastReport = $"Klaar:\n" +
                         $"• {assigned} LootItems aangemaakt\n" +
                         $"• {colliderAdded} BoxCollider toegevoegd (waren zonder collider)\n" +
                         $"• {meshColliderFixed} MeshCollider → convex (warning-fix)\n" +
                         $"• Totale waarde: €{totalValue:N0}\n" +
                         $"• Gemiddeld: €{(assigned > 0 ? totalValue / assigned : 0):N0}";
            Debug.Log("[LootItemAssignerTool] " + lastReport.Replace("\n", " | "));
        }

        private void AddBoxColliderFromRenderer(GameObject go, out BoxCollider col)
        {
            col = Undo.AddComponent<BoxCollider>(go);

            // Voorkeur: gebruik MeshFilter.sharedMesh.bounds (lokale ruimte, exact)
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                col.center = mf.sharedMesh.bounds.center;
                col.size = mf.sharedMesh.bounds.size;
                return;
            }

            // Fallback: gebruik Renderer.bounds (wereld) en converteer naar lokaal
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Bounds wb = rend.bounds;
                Vector3 localCenter = go.transform.InverseTransformPoint(wb.center);
                Vector3 lossy = go.transform.lossyScale;
                Vector3 localSize = new Vector3(
                    wb.size.x / Mathf.Max(0.0001f, lossy.x),
                    wb.size.y / Mathf.Max(0.0001f, lossy.y),
                    wb.size.z / Mathf.Max(0.0001f, lossy.z));
                col.center = localCenter;
                col.size = localSize;
            }
        }

        private int ComputeValue(GameObject go)
        {
            switch (valueMode)
            {
                case ValueMode.BySize:
                {
                    var rend = go.GetComponentInChildren<Renderer>();
                    float size = rend != null ? rend.bounds.size.magnitude : 1f;
                    float t = Mathf.Clamp01(size / 3f);
                    return RoundTo50(Mathf.Lerp(minValue, maxValue, t));
                }

                case ValueMode.ByNameKeyword:
                {
                    string n = go.name.ToLowerInvariant();
                    float t;
                    if (n.Contains("statue") || n.Contains("lisa") || n.Contains("monet") || n.Contains("painting") || n.Contains("david")) t = 1f;
                    else if (n.Contains("trezor") || n.Contains("safe") || n.Contains("vault")) t = 0.8f;
                    else if (n.Contains("rifle") || n.Contains("gun") || n.Contains("weapon")) t = 0.6f;
                    else if (n.Contains("box") || n.Contains("case") || n.Contains("sci-fi")) t = 0.4f;
                    else t = Random.Range(0f, 0.5f);
                    return RoundTo50(Mathf.Lerp(minValue, maxValue, t));
                }

                case ValueMode.RandomRange:
                default:
                    return RoundTo50(Random.Range(minValue, maxValue + 1));
            }
        }

        private static int RoundTo50(float v) => Mathf.RoundToInt(v / 50f) * 50;

        private static string PrettifyName(string raw)
        {
            string s = raw;
            int underscore = s.IndexOf('_');
            if (underscore > 0) s = s.Substring(0, underscore);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            string result = sb.ToString();
            if (result.Length > 0) result = char.ToUpper(result[0]) + result.Substring(1);
            return result;
        }
    }
}
