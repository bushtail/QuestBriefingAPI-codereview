using System.Reflection;
using EFT.UI;
using HarmonyLib;
using Manimal.QuestBriefingAPI.Briefings;
using SPT.Reflection.Patching;

namespace Manimal.QuestBriefingAPI.Patches;

public class BriefingQuestMessagePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.ShowDelayTypeWindow));
    }

    [PatchPrefix]
    private static void Prefix()
    {
        BriefingPlayer.SuspendActive();
    }
}