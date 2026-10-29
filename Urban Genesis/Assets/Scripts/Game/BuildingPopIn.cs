using UnityEngine;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Makes a new building rise out of the ground over a short time, then switches itself off.
    /// Purely cosmetic.
    /// </summary>
    public sealed class BuildingPopIn : MonoBehaviour
    {
        private const float Duration = 0.4f;

        private Vector3 groundCenter;
        private float footprint;
        private float height;
        private float elapsed;

        /// <summary>Sets where the cube ends up and starts the animation.</summary>
        public void Begin(Vector3 cellCenter, float footprintSize, float finalHeight)
        {
            groundCenter = cellCenter;
            footprint = footprintSize;
            height = finalHeight;
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
            float currentHeight = Mathf.Max(0.01f, height * eased);

            transform.position = groundCenter + new Vector3(0f, currentHeight * 0.5f, 0f);
            transform.localScale = new Vector3(footprint, currentHeight, footprint);
        }
    }
}
