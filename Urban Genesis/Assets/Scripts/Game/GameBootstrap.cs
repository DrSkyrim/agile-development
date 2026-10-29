using UnityEngine;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Starts the game by itself. Unity calls this once after a scene loads, so pressing Play in any scene
    /// creates the city. If you later add a <see cref="CityGame"/> to a scene by hand, this leaves it alone.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGameIfMissing()
        {
            if (Object.FindFirstObjectByType<CityGame>() != null)
            {
                return;
            }

            new GameObject("Urban Genesis").AddComponent<CityGame>();
        }
    }
}
