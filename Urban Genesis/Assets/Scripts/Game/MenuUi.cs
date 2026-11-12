using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Shared building blocks for the main menu and the pause menu, so they look like the in-game HUD:
    /// same style sheet, fonts and icons.
    /// </summary>
    public static class MenuUi
    {
        public enum Weight { Regular, Medium, SemiBold }

        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();

        /// <summary>Creates a full-screen UI document under <paramref name="parent"/> and returns its root element.</summary>
        public static VisualElement CreateRoot(Transform parent, string objectName, int sortingOrder)
        {
            EnsureEventSystem();

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1440, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = sortingOrder;
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();

            // The document starts inactive so the panel settings are assigned before it initialises.
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            go.SetActive(true);

            VisualElement root = document.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("UI/UrbanGenesis");
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            root.AddToClassList("ug-root");
            return root;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static Label Text(string text, string classes, Weight weight)
        {
            var label = new Label(text);
            label.AddToClassList("ug-text");
            if (!string.IsNullOrEmpty(classes))
            {
                foreach (string c in classes.Split(' '))
                {
                    label.AddToClassList(c);
                }
            }

            string fontName = weight == Weight.SemiBold ? "Inter-SemiBold" : weight == Weight.Medium ? "Inter-Medium" : "Inter-Regular";
            Font font = Resources.Load<Font>("UI/Fonts/" + fontName);
            if (font != null)
            {
                label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            }

            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        public static VisualElement Icon(string iconName)
        {
            var icon = new VisualElement();
            icon.AddToClassList("ug-icon");
            Texture2D tex;
            if (!Icons.TryGetValue(iconName, out tex) || tex == null)
            {
                tex = Resources.Load<Texture2D>("UI/Icons/" + iconName);
                Icons[iconName] = tex;
            }

            if (tex != null)
            {
                icon.style.backgroundImage = new StyleBackground(tex);
            }

            icon.pickingMode = PickingMode.Ignore;
            return icon;
        }

        /// <summary>A wide menu button with an icon and a label. Disabled buttons ignore clicks.</summary>
        public static VisualElement Button(string label, string iconName, bool primary, Action onClick, bool enabled = true)
        {
            var button = new VisualElement();
            button.AddToClassList("ug-menu-btn");
            if (primary) { button.AddToClassList("ug-menu-btn--primary"); }
            if (!enabled) { button.AddToClassList("ug-menu-btn--disabled"); }
            button.Add(Icon(iconName));
            button.Add(Text(label, null, Weight.Medium));
            if (enabled && onClick != null)
            {
                button.RegisterCallback<ClickEvent>(e => onClick());
            }

            return button;
        }
    }
}
