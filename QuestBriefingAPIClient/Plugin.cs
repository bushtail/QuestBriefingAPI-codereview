using System;
using BepInEx;
using BepInEx.Logging;
using Manimal.QuestBriefingAPI.Briefings;
using Manimal.QuestBriefingAPI.Patches;

namespace Manimal.QuestBriefingAPI;

[BepInPlugin(Id, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string Id = BuildInfo.Guid;
    private const string Name = BuildInfo.Name;
    private const string Version = BuildInfo.Version;
    private const string SemanticVersion = BuildInfo.Version;
    
    internal static ManualLogSource LogSource;

    private void Awake()
    {
        LogSource = Logger;
        Logger.LogInfo($"Quest Briefing API {SemanticVersion}");
        BriefingSettings.Bind(Config);
        BriefingApi.Registry.Warning = message => Logger.LogWarning(message);
        var count = BriefingApi.Registry.LoadPacks(BepInEx.Paths.PluginPath);
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