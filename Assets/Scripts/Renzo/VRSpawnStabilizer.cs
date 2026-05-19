using UnityEngine;
using System.Collections;

/// <summary>
/// Wacht actief totdat de speler de VR-headset daadwerkelijk opzet.
/// Zodra de headset-tracking een realistische hoogte doorgeeft,
/// worden de physics veilig geactiveerd om te voorkomen dat de speler door de vloer valt.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class VRSpawnStabilizer : MonoBehaviour
{
    [Header("Tracking Reference")]
    [Tooltip("Sleep hier de Main Camera (jouw headset) in vanuit de Hierarchy.")]
    public Transform mainCamera;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // CRUCIAAL: Zet de fysieke botsingen uit op frame 1.
        characterController.enabled = false;
    }

    private IEnumerator Start()
    {
        // Fail-safe: als je de camera vergeet te koppelen, waarschuw dan.
        if (mainCamera == null)
        {
            Debug.LogError("[VR STABILIZER] Main Camera mist! Koppel deze in de Inspector.");
            yield break;
        }

        // SLIMME WACHTRIJ: Blijf oneindig wachten totdat de headset fysiek wordt opgetild.
        // (Wanneer de headset op je bureau ligt of in slaapstand staat, is de localPosition rond de 0).
        while (mainCamera.localPosition.y < 0.5f)
        {
            yield return null; // Wacht 1 frame en check opnieuw
        }

        // De headset is opgezet! Geef de Character Controller nog een kwart seconde 
        // om de capsule netjes uit te rekken naar jouw exacte lichaamslengte.
        yield return new WaitForSeconds(0.25f);

        // Zet de zwaartekracht en botsingen weer aan.
        characterController.enabled = true;
        Debug.Log("[VR STABILIZER] Speler heeft de headset opgezet. Physics veilig geactiveerd.");
    }
}