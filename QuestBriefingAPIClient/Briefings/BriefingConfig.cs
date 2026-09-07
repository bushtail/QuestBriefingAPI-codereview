using BepInEx.Configuration;

namespace Manimal.QuestBriefingAPI.Briefings;

internal static class BriefingSettings
{
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> AutoPlay;
    internal static ConfigEntry<float> Volume;
    internal static ConfigEntry<bool> RadioFilter;
    internal static ConfigEntry<float> RadioDistortion;
    internal static ConfigEntry<bool> RadioCues;

    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Briefing Audio", "Enable briefing audio", true,
            "Show the briefing player on registered quests. Turning this off immediately stops audio and hides the player.");
        
        AutoPlay = config.Bind("Briefing Audio", "Automatic playback", true,
            "Play the briefing when a registered quest is selected. Disable for manual playback. Turning this off also stops current audio.");
        
        Volume = config.Bind("Briefing Audio", "Volume", 0.8f,
            new ConfigDescription("Briefing volume, also affected by Tarkov's overall and interface volume.",
                new AcceptableValueRange<float>(0f, 1f)));
        
        RadioFilter = config.Bind("Briefing Audio", "Radio filter", true,
            "Apply a telephone/radio frequency range to briefing narration. Toggle during playback to compare with the original voice. Does not alter recording files.");
        
        RadioDistortion = config.Bind("Briefing Audio", "Radio distortion", 0.08f,
            new ConfigDescription("Adds a little grit when the radio filter is enabled. Set to 0 for a cleaner telephone sound. Changes apply during playback.",
                new AcceptableValueRange<float>(0f, 0.4f)));
        
        RadioCues = config.Bind("Briefing Audio", "Radio clicks and static", true,
            "Play a short radio connection sound before narration and a disconnect sound when it ends or you press Stop. Independent of the voice filter; uses briefing volume.");
    }
}