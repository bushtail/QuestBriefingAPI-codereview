using System;
using BepInEx;
using BepInEx.Logging;

namespace Manimal.QuestBriefingAPI
{
    [BepInPlugin(Id, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = BuildInfo.Guid;
        public const string Name = BuildInfo.Name;
        public const string Version = BuildInfo.Version;
        public const string SemanticVersion = BuildInfo.Version;
        internal static ManualLogSource LogSource;

        private void Awake()
        {
            LogSource = Logger;
            Logger.LogInfo($"Quest Briefing API {SemanticVersion}");
            BriefingSettings.Bind(Config);
            BriefingApi.Registry.Warning = message => Logger.LogWarning(message);
            int count = BriefingApi.Registry.LoadPacks(BepInEx.Paths.PluginPath);
            Logger.LogInfo($"Loaded {count} briefing pack(s), {BriefingApi.Registry.Count} quest recording(s).");
            try
            {
                BriefingForeground.Validate();
                new BriefingQuestMessagePatch().Enable();
                new BriefingQuestShowPatch().Enable();
                new BriefingQuestClosePatch().Enable();
            }
            catch (Exception ex) { Logger.LogError($"Briefing UI patches unavailable: {ex}"); }
        }
    }
}
