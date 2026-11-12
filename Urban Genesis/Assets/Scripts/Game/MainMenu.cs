using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UrbanGenesis.Game
{
    /// <summary>The title screen: the game's name, Continue (if there is a save), New city and Quit.</summary>
    public sealed class MainMenu : MonoBehaviour
    {
        private VisualElement root;

        public void Show(Action onNewCity, Action onContinue)
        {
            if (root == null)
            {
                root = MenuUi.CreateRoot(transform, "Main Menu UI", 30);
            }

            root.Clear();
            root.style.display = DisplayStyle.Flex;

            var background = new VisualElement();
            background.AddToClassList("ug-menu-bg");
            root.Add(background);

            var rule = new VisualElement();
            rule.AddToClassList("ug-menu-rule");
            background.Add(rule);

            var title = new VisualElement();
            title.style.flexDirection = FlexDirection.Row;
            title.Add(MenuUi.Text("Urban", "ug-menu-title", MenuUi.Weight.SemiBold));
            var genesis = MenuUi.Text("Genesis", "ug-menu-title ug-menu-title--accent", MenuUi.Weight.SemiBold);
            genesis.style.marginLeft = 18;
            title.Add(genesis);
            background.Add(title);
            background.Add(MenuUi.Text("CITY PLANNING SIMULATOR", "ug-menu-tagline", MenuUi.Weight.Medium));

            var buttons = new VisualElement();
            buttons.style.width = 340;

            string error;
            SaveGame save = SaveStore.Exists ? SaveStore.Read(out error) : null;
            buttons.Add(MenuUi.Button("Continue", "play", save != null, onContinue, save != null));
            if (save != null)
            {
                string detail = save.CityName + "  ·  " + save.Population.ToString("N0") + " residents  ·  saved " + save.SavedAt;
                buttons.Add(MenuUi.Text(detail, "ug-menu-note", MenuUi.Weight.Regular));
                var gap = new VisualElement();
                gap.style.height = 8;
                buttons.Add(gap);
            }

            buttons.Add(MenuUi.Button("New city", "plus", save == null, onNewCity));
            buttons.Add(MenuUi.Button("Quit", "minus", false, Quit));
            background.Add(buttons);

            var footer = MenuUi.Text("Prototype  ·  Milestone 1", "ug-text--muted ug-menu-footer", MenuUi.Weight.Regular);
            background.Add(footer);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
