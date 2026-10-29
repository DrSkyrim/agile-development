using UnityEngine;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Makes a new building rise out of the ground over a short time by stretching it upwards from flat,
    /// then switches itself off. Purely cosmetic. Put it on the object that stands on the ground.
    /// </summary>
    public sealed class BuildingPopIn : MonoBehaviour
    {
        private const float Duration = 0.45f;

        private float elapsed;

        public void Begin()
        {
            elapsed = 0f;
            Apply(0f);
            enabled = true;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            Apply(t);

            if (t >= 1f)
            {
                enabled = false;
            }
        }

        private void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t); // ease out
            transform.localScale = new Vector3(1f, Mathf.Max(0.01f, eased), 1f);
        }
    }
}
