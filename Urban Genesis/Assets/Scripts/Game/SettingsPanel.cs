using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UrbanGenesis.Game
{
    /// <summary>The options screen shared by the main menu and the pause menu.</summary>
    public static class SettingsPanel
    {
        public const int Width = 440;

        public static void Fill(VisualElement container, Action back)
        {
            container.Clear();
            container.style.width = Width;
            container.Add(MenuUi.Text("Settings", "ug-text--title", MenuUi.Weight.SemiBold));
            var gap = new VisualElement();
            gap.style.height = 14;
            container.Add(gap);

            Action refresh = () => Fill(container, back);

            container.Add(Row("Edge scrolling", "Move the camera by pushing the mouse against the window edge",
                Chip(GameSettings.EdgeScrolling ? "On" : "Off", true, () => { GameSettings.EdgeScrolling = !GameSettings.EdgeScrolling; refresh(); })));

            container.Add(Row("Camera speed", "How fast the keyboard and the edges move the view",
                Chip("−", GameSettings.CanStepCameraSpeed(-1), () => { GameSettings.StepCameraSpeed(-1); refresh(); }),
                Value(GameSettings.Percent(GameSettings.CameraSpeed)),
                Chip("+", GameSettings.CanStepCameraSpeed(1), () => { GameSettings.StepCameraSpeed(1); refresh(); })));

            container.Add(Row("Interface size", "Makes the toolbar and menus bigger or smaller",
                Chip("−", GameSettings.CanStepUiScale(-1), () => { GameSettings.StepUiScale(-1); refresh(); }),
                Value(GameSettings.Percent(GameSettings.UiScale)),
                Chip("+", GameSettings.CanStepUiScale(1), () => { GameSettings.StepUiScale(1); refresh(); })));

            var space = new VisualElement();
            space.style.height = 10;
            container.Add(space);
            container.Add(MenuUi.Button("Reset to defaults", "minus", false, () => { GameSettings.ResetToDefaults(); refresh(); }));
            container.Add(MenuUi.Button("Back", "menu", true, back));
        }

        private static VisualElement Row(string title, string hint, params VisualElement[] controls)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 14;

            var text = new VisualElement();
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            text.Add(MenuUi.Text(title, "", MenuUi.Weight.Medium));
            text.Add(MenuUi.Text(hint, "ug-menu-note", MenuUi.Weight.Regular));
            row.Add(text);

            foreach (VisualElement control in controls) { row.Add(control); }
            return row;
        }

        private static VisualElement Chip(string text, bool enabled, Action onClick)
        {
            var chip = new VisualElement();
            chip.AddToClassList("ug-btn");
            chip.AddToClassList("ug-btn--text");
            chip.AddToClassList("ug-btn--28");
            chip.style.minWidth = 36;
            chip.style.backgroundColor = new Color(0.19f, 0.24f, 0.29f, enabled ? 1f : 0.5f);
            chip.style.marginLeft = 4;
            chip.Add(MenuUi.Text(text, "", MenuUi.Weight.Medium));
            if (enabled) { chip.RegisterCallback<ClickEvent>(e => onClick()); }
            return chip;
        }

        private static VisualElement Value(string text)
        {
            Label label = MenuUi.Text(text, "", MenuUi.Weight.Medium);
            label.style.minWidth = 52;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }
    }
}
