using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UrbanGenesis.Game
{
    /// <summary>The title screen: the game's name, Continue (if there is a save), New city and Quit.</summary>
    public sealed class MainMenu : MonoBehaviour
    {
        private VisualElement root;

        private Action newCity;
        private Action<int> loadSlot;
        private VisualElement buttons;

        public void Show(Action onNewCity, Action<int> onLoadSlot)
        {
            newCity = onNewCity;
            loadSlot = onLoadSlot;
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

            buttons = new VisualElement();
            background.Add(buttons);
            ShowMain();

            var footer = MenuUi.Text("Prototype  ·  Milestone 1", "ug-text--muted ug-menu-footer", MenuUi.Weight.Regular);
            background.Add(footer);
        }

        private void ShowMain()
        {
            buttons.Clear();
            buttons.style.width = 340;

            int recent = SaveStore.MostRecentSlot();
            string error;
            SaveGame save = recent > 0 ? SaveStore.Read(recent, out error) : null;
            buttons.Add(MenuUi.Button("Continue", "play", save != null, () => loadSlot(recent), save != null));
            if (save != null)
            {
                string detail = save.CityName + "  ·  " + save.Population.ToString("N0") + " residents  ·  saved " + save.SavedAt;
                buttons.Add(MenuUi.Text(detail, "ug-menu-note", MenuUi.Weight.Regular));
                var gap = new VisualElement();
                gap.style.height = 8;
                buttons.Add(gap);
            }

            buttons.Add(MenuUi.Button("Load game", "save", false, ShowSlots, SaveStore.AnyExists));
            buttons.Add(MenuUi.Button("New city", "plus", save == null, newCity));
            buttons.Add(MenuUi.Button("Settings", "settings-2", false, () => SettingsPanel.Fill(buttons, ShowMain)));
            buttons.Add(MenuUi.Button("Quit", "minus", false, Quit));
        }

        private void ShowSlots()
        {
            SlotList.Fill(buttons, "Load game", false, slot => loadSlot(slot), ShowMain);
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
