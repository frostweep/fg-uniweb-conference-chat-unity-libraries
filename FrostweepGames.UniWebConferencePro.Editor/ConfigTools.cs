using UnityEditor;
using UnityEngine;
using FrostweepGames.UniWebConferencePro.Common;

namespace FrostweepGames.UniWebConferencePro.Editor
{
    internal class ConfigTools
    {
        static ConfigTools()
        {
            string path = "UniWebConferencePro/GeneralConfig";
            GeneralConfig config = GeneralConfig.Config;

            if (config == null)
            {
                Debug.LogError($"Uni Web Conference Pro General Config not found in {path} Resources folder. Will use default.");

                config = ScriptableObject.CreateInstance<GeneralConfig>();

                GenerateConfig(config, "Assets/FrostweepGames/UniWebConferencePro/Resources/UniWebConferencePro");
            }
        }

        internal static void GenerateConfig(Object config, string pathToFolder, string filename = "GeneralConfig.asset")
        {
            if (!System.IO.Directory.Exists(Application.dataPath + "/../" + pathToFolder))
            {
                System.IO.Directory.CreateDirectory(pathToFolder);
                AssetDatabase.ImportAsset(pathToFolder);
            }

            if (!System.IO.File.Exists(Application.dataPath + "/../" + pathToFolder + "/" + filename))
            {
                AssetDatabase.CreateAsset(config, $"{pathToFolder}/{filename}");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
