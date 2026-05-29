using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wimme.EditorTools
{
    /// <summary>
    /// Editor-tool om 73+ losse Deposit-objecten te clusteren tot 8-12 logische
    /// groepen. Per cluster wordt een parent-GameObject aangemaakt met tag
    /// "Deposit" + trigger-BoxCollider voor AI-detectie. De originele children
    /// worden under-the-parent gezet en hun tag wordt verwijderd.
    ///
    /// Aanroepen: Tools > Bank Heist > Cluster Deposits
    ///
    /// Workflow:
    ///   1. Open de scene (of prefab) met je nieuwe bank-prefab
    ///   2. Tools > Bank Heist > Cluster Deposits
    ///   3. Pas radius aan (default 3.0m) — alles binnen die radius wordt 1 groep
    ///   4. Druk "Cluster Now"
    ///   5. Output: X groepen aangemaakt, Y deposits verplaatst
    ///   6. Daarna handmatig: per groep beslis welke children grijpbaar worden
    ///      (XRGrabInteractable + Rigidbody + LootItem)
    /// </summary>
    public class DepositClusterTool : EditorWindow
    {
        private float clusterRadius = 3.0f;
        private string parentPrefix = "Deposit_Group_";
        private bool sceneOnly = true;
        private Vector3 colliderPadding = new Vector3(0.5f, 0.5f, 0.5f);
        private string lastReport = "";

        [MenuItem("Tools/Bank Heist/Cluster Deposits")]
        public static void ShowWindow()
        {
            GetWindow<DepositClusterTool>("Deposit Clusterer");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Cluster Deposit-objecten op nabijheid", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Vindt alle GameObjects met tag 'Deposit', groepeert ze per nabijheid, en maakt een parent per groep aan. " +
                "De parent krijgt tag 'Deposit' + een trigger BoxCollider. Children verliezen hun Deposit-tag.",
                MessageType.Info);

            EditorGUILayout.Space();
            clusterRadius = EditorGUILayout.Slider("Cluster radius (m)", clusterRadius, 1f, 10f);
            colliderPadding = EditorGUILayout.Vector3Field("Collider padding", colliderPadding);
            parentPrefix = EditorGUILayout.TextField("Parent prefix", parentPrefix);
            sceneOnly = EditorGUILayout.Toggle(
                new GUIContent("Alleen actieve scene", "Indien uit: doorzoekt ook prefab-stage"),
                sceneOnly);

            EditorGUILayout.Space();

            if (GUILayout.Button("Voorbeeld telling (dry run)"))
            {
                DryRun();
            }

            using (new EditorGUI.DisabledScope(false))
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Cluster Now (maakt parents aan!)"))
                {
                    if (EditorUtility.DisplayDialog(
                        "Cluster Deposits",
                        "Dit wijzigt de scene. Maak eerst een backup of save. Doorgaan?",
                        "Ja, cluster",
                        "Annuleer"))
                    {
                        RunClustering();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Laatste resultaat:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(lastReport) ? "(nog niets uitgevoerd)" : lastReport, MessageType.None);
        }

        private GameObject[] FindAllDeposits()
        {
            var found = new List<GameObject>();
            var all = sceneOnly
                ? Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                : Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go == null) continue;
                if (!go.CompareTag("Deposit")) continue;
                // Skip already-clustered parents
                if (go.name.StartsWith(parentPrefix)) continue;
                found.Add(go);
            }
            return found.ToArray();
        }

        private void DryRun()
        {
            var deposits = FindAllDeposits();
            var clusters = BuildClusters(deposits);
            lastReport = $"Dry run:\n" +
                         $"• {deposits.Length} deposits gevonden\n" +
                         $"• {clusters.Count} clusters bij radius {clusterRadius:F1}m\n" +
                         $"• gem. {(deposits.Length > 0 ? (float)deposits.Length / clusters.Count : 0):F1} per cluster";
            Debug.Log("[DepositClusterTool] " + lastReport.Replace("\n", " | "));
        }

        private void RunClustering()
        {
            var deposits = FindAllDeposits();
            if (deposits.Length == 0)
            {
                lastReport = "Geen GameObjects met tag 'Deposit' gevonden.";
                return;
            }

            var clusters = BuildClusters(deposits);
            int created = 0;
            int reparented = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Cluster Deposits");

            for (int i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                if (cluster.Count == 0) continue;

                Vector3 center = Vector3.zero;
                Bounds combined = new Bounds(cluster[0].transform.position, Vector3.zero);
                foreach (var d in cluster)
                {
                    center += d.transform.position;
                    combined.Encapsulate(d.transform.position);
                    var rend = d.GetComponentInChildren<Renderer>();
                    if (rend != null) combined.Encapsulate(rend.bounds);
                }
                center /= cluster.Count;

                var parent = new GameObject($"{parentPrefix}{i + 1:00}");
                Undo.RegisterCreatedObjectUndo(parent, "Create deposit group");
                parent.transform.position = center;
                parent.tag = "Deposit";

                var col = parent.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = combined.center - center;
                col.size = combined.size + colliderPadding;

                foreach (var d in cluster)
                {
                    Undo.SetTransformParent(d.transform, parent.transform, "Reparent deposit");
                    Undo.RecordObject(d, "Untag deposit");
                    d.tag = "Untagged";
                    reparented++;
                }
                created++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            lastReport = $"Klaar:\n" +
                         $"• {created} groepen aangemaakt\n" +
                         $"• {reparented} deposits ondergebracht\n\n" +
                         $"Vervolg-stappen:\n" +
                         $"1. Per groep — beslis welke child grijpbaar wordt\n" +
                         $"2. Op die child: Rigidbody (mass ~0.5) + XRGrabInteractable + LootItem\n" +
                         $"3. Sleep de parent-objects in HeistEnvController.depositSlots[]\n" +
                         $"4. Stel HeistEnvController.maxDepositsActive in op 6-8";
            Debug.Log("[DepositClusterTool] " + lastReport.Replace("\n", " | "));
        }

        /// <summary>Single-pass nearest-neighbor clustering. Niet optimaal voor heel grote datasets, prima voor &lt;500 punten.</summary>
        private List<List<GameObject>> BuildClusters(GameObject[] deposits)
        {
            var clusters = new List<List<GameObject>>();
            var used = new bool[deposits.Length];
            float sqrRadius = clusterRadius * clusterRadius;

            for (int i = 0; i < deposits.Length; i++)
            {
                if (used[i]) continue;
                var current = new List<GameObject> { deposits[i] };
                used[i] = true;

                // Greedy expansion: add anything within radius of ANY current member
                bool added;
                do
                {
                    added = false;
                    for (int j = 0; j < deposits.Length; j++)
                    {
                        if (used[j]) continue;
                        foreach (var member in current)
                        {
                            if ((member.transform.position - deposits[j].transform.position).sqrMagnitude <= sqrRadius)
                            {
                                current.Add(deposits[j]);
                                used[j] = true;
                                added = true;
                                break;
                            }
                        }
                    }
                } while (added);

                clusters.Add(current);
            }
            return clusters;
        }
    }
}
