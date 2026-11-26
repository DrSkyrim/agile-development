using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// The player's options, kept in Unity's PlayerPrefs (per user, outside the saved cities).
    /// Everything that depends on an option reads it from here when it needs it, so a change takes effect at once.
    /// </summary>
    public static class GameSettings
    {
        private const string EdgeScrollKey = "ug.edgeScrolling";
        private const string CameraSpeedKey = "ug.cameraSpeedIndex";
        private const string UiScaleKey = "ug.uiScaleIndex";

        public static readonly float[] CameraSpeeds = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
        public static readonly float[] UiScales = { 0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.5f };
        private const int DefaultIndex = 2;

        private static readonly List<PanelSettings> Panels = new List<PanelSettings>();
        private static int cameraSpeedIndex = -1;
        private static int uiScaleIndex = -1;
        private static int edgeScrolling = -1;

        /// <summary>Raised after any option changed.</summary>
        public static event Action Changed;

        public static bool EdgeScrolling
        {
            get
            {
                if (edgeScrolling < 0) { edgeScrolling = PlayerPrefs.GetInt(EdgeScrollKey, 1) != 0 ? 1 : 0; }
                return edgeScrolling == 1;
            }
            set
            {
                edgeScrolling = value ? 1 : 0;
                PlayerPrefs.SetInt(EdgeScrollKey, edgeScrolling);
                Commit();
            }
        }

        /// <summary>How fast the camera moves, as a multiple of the normal speed.</summary>
        public static float CameraSpeed { get { return CameraSpeeds[CameraSpeedIndex]; } }

        /// <summary>How big the interface is drawn, as a multiple of the normal size.</summary>
        public static float UiScale { get { return UiScales[UiScaleIndex]; } }

        private static int CameraSpeedIndex
        {
            get
            {
                if (cameraSpeedIndex < 0) { cameraSpeedIndex = Clamp(PlayerPrefs.GetInt(CameraSpeedKey, DefaultIndex), CameraSpeeds.Length); }
                return cameraSpeedIndex;
            }
        }

        private static int UiScaleIndex
        {
            get
            {
                if (uiScaleIndex < 0) { uiScaleIndex = Clamp(PlayerPrefs.GetInt(UiScaleKey, DefaultIndex), UiScales.Length); }
                return uiScaleIndex;
            }
        }

        /// <summary>Moves the camera speed one step up (+1) or down (-1), stopping at the ends.</summary>
        public static void StepCameraSpeed(int direction)
        {
            cameraSpeedIndex = Clamp(CameraSpeedIndex + direction, CameraSpeeds.Length);
            PlayerPrefs.SetInt(CameraSpeedKey, cameraSpeedIndex);
            Commit();
        }

        public static void StepUiScale(int direction)
        {
            uiScaleIndex = Clamp(UiScaleIndex + direction, UiScales.Length);
            PlayerPrefs.SetInt(UiScaleKey, uiScaleIndex);
            Commit();
        }

        public static void ResetToDefaults()
        {
            edgeScrolling = 1;
            cameraSpeedIndex = DefaultIndex;
            uiScaleIndex = DefaultIndex;
            PlayerPrefs.SetInt(EdgeScrollKey, 1);
            PlayerPrefs.SetInt(CameraSpeedKey, DefaultIndex);
            PlayerPrefs.SetInt(UiScaleKey, DefaultIndex);
            Commit();
        }

        public static bool CanStepCameraSpeed(int direction) { int i = CameraSpeedIndex + direction; return i >= 0 && i < CameraSpeeds.Length; }

        public static bool CanStepUiScale(int direction) { int i = UiScaleIndex + direction; return i >= 0 && i < UiScales.Length; }

        public static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }

        /// <summary>Makes a UI panel follow the interface-size option, now and whenever it changes.</summary>
        public static void Track(PanelSettings panel)
        {
            Panels.RemoveAll(p => p == null);
            Panels.Add(panel);
            Apply(panel);
        }

        private static void Apply(PanelSettings panel)
        {
            float scale = UiScale;
            panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(1440f / scale), Mathf.RoundToInt(900f / scale));
        }

        private static void Commit()
        {
            PlayerPrefs.Save();
            Panels.RemoveAll(p => p == null);
            foreach (PanelSettings panel in Panels) { Apply(panel); }
            Action changed = Changed;
            if (changed != null) { changed(); }
        }

        private static int Clamp(int index, int length)
        {
            return Mathf.Clamp(index, 0, length - 1);
        }
    }
}
