using UnityEngine;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Starts the game by itself. Unity calls this once after a scene loads, so pressing Play in any scene
    /// shows the main menu. If you later add a <see cref="GameFlow"/> to a scene by hand, this leaves it alone.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGameIfMissing()
        {
            if (Object.FindFirstObjectByType<GameFlow>() != null)
            {
                return;
            }

            // A CityGame placed in a scene by hand would never be started now; the menu creates it instead.
            CityGame stray = Object.FindFirstObjectByType<CityGame>();
            if (stray != null)
            {
                Debug.LogWarning("A CityGame was found in the scene. Cities are now started from the main menu, so it was removed.");
                Object.Destroy(stray.gameObject);
            }

            new GameObject("Urban Genesis").AddComponent<GameFlow>();
        }
    }
}
