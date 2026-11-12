using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// The in-game menu: Resume, Save game, Main menu. Open it with Esc (when no tool is active) or by clicking the city name.
    /// While it is open the simulation is paused and the camera and tools ignore input.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private CityGame game;
        private ToolController tools;
        private VisualElement scrim;
        private VisualElement card;
        private string status = "";
        private bool cameFromHud;

        public bool IsOpen { get { return game != null && game.MenuOpen; } }

        public void Init(CityGame cityGame, ToolController toolController)
        {
            game = cityGame;
            tools = toolController;
            scrim = MenuUi.CreateRoot(transform, "Pause Menu UI", 20);
            scrim.pickingMode = PickingMode.Ignore;
            scrim.style.display = DisplayStyle.None;

            var backdrop = new VisualElement();
            backdrop.AddToClassList("ug-menu-scrim");
            scrim.Add(backdrop);

            card = new VisualElement();
            card.AddToClassList("ug-menu-card");
            backdrop.Add(card);
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            status = "";
            cameFromHud = false;
            game.MenuOpen = true;
            ShowMain();
            scrim.style.display = DisplayStyle.Flex;
        }

        /// <summary>Opens straight on the "back to the main menu?" question.</summary>
        public void OpenLeaveConfirm()
        {
            if (IsOpen)
            {
                ShowLeaveConfirm();
                return;
            }

            status = "";
            cameFromHud = true;
            game.MenuOpen = true;
            ShowLeaveConfirm();
            scrim.style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            game.MenuOpen = false;
            scrim.style.display = DisplayStyle.None;
        }

        private void LateUpdate()
        {
            if (game == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (IsOpen)
            {
                Close();
            }
            else if (tools == null || tools.EscapeUsedFrame != Time.frameCount)
            {
                Open();
            }
        }

        private void ShowMain()
        {
            card.Clear();
            card.Add(MenuUi.Text("Paused", "ug-text--title", MenuUi.Weight.SemiBold));
            card.Add(MenuUi.Text(game.CityName, "ug-text--muted ug-text--soft", MenuUi.Weight.Regular));
            var gap = new VisualElement();
            gap.style.height = 18;
            card.Add(gap);

            card.Add(MenuUi.Button("Resume", "play", true, Close));
            card.Add(MenuUi.Button("Save game", "save", false, Save));
            card.Add(MenuUi.Button("Main menu", "menu", false, ShowLeaveConfirm));

            if (!string.IsNullOrEmpty(status))
            {
                card.Add(MenuUi.Text(status, "ug-menu-note", MenuUi.Weight.Regular));
            }
        }

        private void ShowLeaveConfirm()
        {
            card.Clear();
            card.Add(MenuUi.Text("Back to the main menu?", "ug-text--title", MenuUi.Weight.SemiBold));
            card.Add(MenuUi.Text("Anything you have not saved will be lost.", "ug-menu-note", MenuUi.Weight.Regular));
            var gap = new VisualElement();
            gap.style.height = 18;
            card.Add(gap);

            card.Add(MenuUi.Button("Save and exit", "save", true, () =>
            {
                string error = game.SaveToDisk();
                if (error != null)
                {
                    status = error;
                    ShowMain();
                    return;
                }

                game.RequestMainMenu();
            }));
            card.Add(MenuUi.Button("Exit without saving", "menu", false, game.RequestMainMenu));
            card.Add(MenuUi.Button("Back", "play", false, () => { if (cameFromHud) { Close(); } else { ShowMain(); } }));
        }

        private void Save()
        {
            string error = game.SaveToDisk();
            status = error ?? "Game saved.";
            ShowMain();
        }
    }
}
