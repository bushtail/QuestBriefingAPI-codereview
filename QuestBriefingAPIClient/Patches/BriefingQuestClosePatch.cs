using System.Reflection;
using EFT.UI;
using HarmonyLib;
using Manimal.QuestBriefingAPI.Briefings;
using SPT.Reflection.Patching;

namespace Manimal.QuestBriefingAPI.Patches;

public class BriefingQuestClosePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(QuestView), nameof(QuestView.Close));
    
    [PatchPrefix]
    private static void Prefix(NotesTaskDescription ____descriptionPanel)
    {
        if (____descriptionPanel)
        {
            ____descriptionPanel.GetComponent<BriefingPlayer>()?.Clear();
        }
    }
}