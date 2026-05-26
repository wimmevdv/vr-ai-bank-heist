using UnityEngine;

namespace Wimme.Test
{
    [RequireComponent(typeof(CapsuleCollider))]
    public class VRPlayerBridge : MonoBehaviour
    {
        [SerializeField] private HeistEnvController env;
        [SerializeField] private float noiseThreshold = 0.3f;
        [SerializeField] private float noiseCooldown = 0.5f;

        private Vector3 lastPosition;
        private float lastNoiseTime;

        void Start()
        {
            if (!gameObject.CompareTag("Player"))
                gameObject.tag = "Player";

            var col = GetComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.3f;

            lastPosition = transform.position;
        }

        void Update()
        {
            if (env == null) return;

            float speed = (transform.position - lastPosition).magnitude / Time.deltaTime;
            lastPosition = transform.position;

            if (speed > noiseThreshold && Time.time - lastNoiseTime > noiseCooldown)
            {
                float loudness = Mathf.Clamp01(speed / 3.5f);
                env.RegisterNoise(transform.position, loudness);
                lastNoiseTime = Time.time;
            }
        }
    }
}
