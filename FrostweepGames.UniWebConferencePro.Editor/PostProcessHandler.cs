using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace FrostweepGames.UniWebConferencePro.Editor
{
    internal class PostProcessHandler
    {
        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target == BuildTarget.WebGL)
            {
                string indexPath = $"{pathToBuiltProject}/index.html";

                if (System.IO.File.Exists(indexPath))
                {
                    string indexData = System.IO.File.ReadAllText(indexPath);

                    string dependencies =
    @"    <!-- UNI WEB CONFERENCE PRO START -->
    <script src='https://cdn.archive.frostweepgames.com/uniwebconferencepro/1.0.0/socket.io@2.3.0/socket.io.js'></script>
    <script src='https://cdn.archive.frostweepgames.com/uniwebconferencepro/1.0.0/peerjs@1.4.5/peerjs.min.js'></script>
    <script src='https://cdn.archive.frostweepgames.com/uniwebconferencepro/1.0.0/unity-webgl-tools.js'></script>
    <script src='https://cdn.archive.frostweepgames.com/uniwebconferencepro/1.0.0/network-chat.js'></script>
    <!-- UNI WEB CONFERENCE PRO END -->";

                    indexData = indexData.Insert(indexData.IndexOf("</head>"), $"\n{dependencies}\n");

                    System.IO.File.WriteAllText(indexPath, indexData);
                }
                else
                {
                    Debug.LogError("Process of UNI WEB CONFERENCE PRO failed due to: index.html not found!");
                }
            }
        }
    }
}
