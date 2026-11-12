using UnityEngine;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Runs the order of things: main menu, then a city (new or loaded), and back to the menu.
    /// There is only ever one city object alive; leaving a city destroys it and everything it created.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        private MainMenu menu;
        private CityGame city;

        private void Start()
        {
            menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            menu.transform.SetParent(transform, false);
            ShowMenu();
        }

        private void ShowMenu()
        {
            menu.Show(() => StartCity(null), ContinueSaved);
        }

        private void ContinueSaved()
        {
            string error;
            SaveGame save = SaveStore.Read(out error);
            if (save == null)
            {
                Debug.LogWarning(error);
                ShowMenu();
                return;
            }

            StartCity(save);
        }

        private void StartCity(SaveGame save)
        {
            menu.Hide();
            var go = new GameObject("Urban Genesis City");
            go.transform.SetParent(transform, false);
            city = go.AddComponent<CityGame>();
            city.MainMenuRequested += LeaveCity;
            city.Begin(save);
        }

        private void LeaveCity()
        {
            if (city != null)
            {
                city.MainMenuRequested -= LeaveCity;
                Destroy(city.gameObject);
                city = null;
            }

            ShowMenu();
        }
    }
}
