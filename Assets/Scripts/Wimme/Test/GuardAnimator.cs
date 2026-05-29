using UnityEngine;

namespace Wimme.Test
{
    /// <summary>
    /// Driver voor de Animator op het visuele bewaker-model. Leest de horizontale
    /// snelheid van de parent-<see cref="Rigidbody"/> en zet die op de
    /// "Speed"-parameter, waar de Animator tussen idle, walk en run blendt.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class GuardAnimator : MonoBehaviour
    {
        [SerializeField] private float walkThreshold = 0.5f;
        [SerializeField] private float runThreshold = 4.0f;

        private Animator anim;
        private Rigidbody rb;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        void Start()
        {
            anim = GetComponent<Animator>();
            rb = GetComponentInParent<Rigidbody>();
        }

        void Update()
        {
            if (rb == null || anim == null) return;
            float speed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
            anim.SetFloat(SpeedHash, speed);
        }
    }
}
