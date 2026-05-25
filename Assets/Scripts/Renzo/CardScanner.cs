using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class CardScanner : MonoBehaviour
{
    [Tooltip("Welke keycard ID accepteert deze scanner.")]
    public string acceptedKeyId = "vault";

    [Tooltip("Deur die opengaat als de juiste kaart wordt gescand.")]
    public VaultDoorController door;

    [Tooltip("Geluid bij geldige scan.")]
    public AudioSource scanSound;

    [Tooltip("Eenmalig scannen of telkens opnieuw activeren.")]
    public bool oneShot = true;

    public UnityEvent onAccepted;
    public UnityEvent onRejected;

    private bool hasBeenUsed = false;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (oneShot && hasBeenUsed) return;

        var card = other.GetComponent<KeyCard>();
        if (card == null) card = other.GetComponentInParent<KeyCard>();
        if (card == null) return;

        if (card.keyId == acceptedKeyId)
        {
            hasBeenUsed = true;
            if (scanSound != null) scanSound.Play();
            if (door != null) door.OpenAutomatically();
            onAccepted?.Invoke();
        }
        else
        {
            onRejected?.Invoke();
        }
    }
}
