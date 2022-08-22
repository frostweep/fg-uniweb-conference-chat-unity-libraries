using UnityEngine;

namespace FrostweepGames.UniWebConferencePro.Common
{
    //[CreateAssetMenu(fileName = "GeneralConfig", menuName = "FrostweepGames/UniWebConferencePro/GeneralConfig", order = 3)]
    public class GeneralConfig : ScriptableObject
    {
        private static GeneralConfig _Config;
        public static GeneralConfig Config
        {
            get
            {
                if (_Config == null)
                    _Config = GetConfig();
                return _Config;
            }
        }

        public bool showWelcomeDialogAtStartup = true;

        [Header("Connection Settings")]

        [Tooltip("Used for connecting to the server. Required")]
        public string AppKey = string.Empty;
        public bool autoConnect = true;

        [Header("Audio Settings")]

        public bool spatialAudioEnabled = false;
        [Range(0, 1000)]
        public float spatialAudioRadius = 10f;
        [Range(0, 1000)]
        public float spatialAudioMinimalHearRadius = 1f;
        public AnimationCurve spatialAudioCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private static GeneralConfig GetConfig()
        {
            var config = Resources.Load<GeneralConfig>("UniWebConferencePro/GeneralConfig");
            config.AppKey = config.AppKey.Replace(" ", string.Empty);
            return config;
        }
    }
}
