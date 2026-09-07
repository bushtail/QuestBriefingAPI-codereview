using System;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using Manimal.QuestBriefingAPI.Briefings;
using SPT.Reflection.Patching;
using TMPro;

namespace Manimal.QuestBriefingAPI.Patches;

public class BriefingQuestShowPatch : ModulePatch
{
    private static readonly FieldInfo DescriptionText = AccessTools.Field(typeof(NotesTaskDescription), "_description");
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(QuestView), nameof(QuestView.Show));

    [PatchPostfix]
    private static void Postfix(EFT.Quests.Quest quest, NotesTaskDescription ____descriptionPanel)
    {
        try
        {
            var player = ____descriptionPanel.GetComponent<BriefingPlayer>();
            
            var recording = BriefingApi.Registry.Find(quest?.Id, quest?.Template?.TraderId);
            if (recording == null)
            {
                BriefingPlayer.StopActive();
                if (player) player.Clear();
                return;
            }
            
            if (!player)
            {
                var text = DescriptionText?.GetValue(____descriptionPanel) as TMP_Text;
                if (!text) throw new InvalidOperationException("Quest description text was not found.");
                player = ____descriptionPanel.gameObject.AddComponent<BriefingPlayer>();
                player.Initialize(text);
            }
            
            player.Select(quest!.Id, quest.QuestStatus != EFT.Quests.EQuestStatus.Locked, recording);
        }
        catch (Exception ex) { Plugin.LogSource.LogError($"[Briefings] Could not show player: {ex}"); }
    }
}