using System;
using BepInEx;
using BepInEx.Logging;

namespace Manimal.QuestBriefingAPI
{
    [BepInPlugin(Id, "Quest Briefing API", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "Manimal.QuestBriefingAPI";
        public const string Version = "1.0.0";
        internal static ManualLogSource LogSource;

        private void Awake()
        {
            LogSource = Logger;
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
