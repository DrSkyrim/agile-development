using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UrbanGenesis.Game
{
    /// <summary>The list of save slots shared by the main menu (to load) and the pause menu (to save).</summary>
    public static class SlotList
    {
        public const int Width = 520;

        /// <summary>
        /// Fills <paramref name="container"/> with the slots. When <paramref name="saving"/> every slot can be chosen
        /// (a used one is overwritten); otherwise only used slots can. The small minus button deletes a slot after a second click.
        /// </summary>
        public static void Fill(VisualElement container, string title, bool saving, Action<int> choose, Action back, string status = "")
        {
            Render(container, title, saving, choose, back, status, 0);
        }

        private static void Render(VisualElement container, string title, bool saving, Action<int> choose, Action back, string status, int armed)
        {
            container.Clear();
            container.style.width = Width;
            container.Add(MenuUi.Text(title, "ug-text--title", MenuUi.Weight.SemiBold));
            var gap = new VisualElement();
            gap.style.height = 14;
            container.Add(gap);

            for (int slot = 1; slot <= SaveStore.SlotCount; slot++)
            {
                int thisSlot = slot;
                string error;
                SaveGame save = SaveStore.Exists(slot) ? SaveStore.Read(slot, out error) : null;
                bool damaged = save == null && SaveStore.Exists(slot);

                string label;
                if (save != null)
                {
                    label = slot + "   " + save.CityName + "  ·  " + save.Population.ToString("N0") + " residents  ·  " + save.SavedAt;
                }
                else
                {
                    label = slot + "   " + (damaged ? "Unreadable save" : "Empty");
                }

                bool enabled = saving || save != null;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                VisualElement button = MenuUi.Button(label, saving ? "save" : "play", false, () => choose(thisSlot), enabled);
                button.style.flexGrow = 1;
                row.Add(button);

                if (save != null || damaged)
                {
                    var delete = new VisualElement();
                    delete.AddToClassList("ug-menu-btn");
                    if (armed == slot) { delete.AddToClassList("ug-menu-btn--primary"); }
                    delete.style.width = 48;
                    delete.style.marginLeft = 8;
                    delete.style.paddingLeft = 0;
                    delete.style.paddingRight = 0;
                    delete.style.justifyContent = Justify.Center;
                    delete.Add(MenuUi.Icon("minus"));
                    delete.RegisterCallback<ClickEvent>(e =>
                    {
                        if (armed != thisSlot)
                        {
                            Render(container, title, saving, choose, back, "Click the minus again to delete slot " + thisSlot + " for good.", thisSlot);
                            return;
                        }

                        string problem = SaveStore.Delete(thisSlot);
                        Render(container, title, saving, choose, back, problem ?? "Slot " + thisSlot + " deleted.", 0);
                    });
                    row.Add(delete);
                }

                container.Add(row);
            }

            if (!string.IsNullOrEmpty(status))
            {
                container.Add(MenuUi.Text(status, "ug-menu-note", MenuUi.Weight.Regular));
            }

            var space = new VisualElement();
            space.style.height = 10;
            container.Add(space);
            container.Add(MenuUi.Button("Back", "menu", false, back));
        }
    }
}
