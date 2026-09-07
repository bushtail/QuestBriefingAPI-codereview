using System;
using System.Collections;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace Manimal.QuestBriefingAPI.Briefings;

internal static class BriefingForeground
{
    private static readonly FieldInfo InputChildren = AccessTools.Field(typeof(ItemUiContext), "_children");

    internal static void Validate()
    {
        if (InputChildren == null)
        {
            throw new MissingFieldException("ItemUiContext input window list was not found.");
        }
    }

    internal static bool HasForegroundWindow()
    {
        var context = ItemUiContext.Instance;
        if (!context) { return false; }
        
        if (InputChildren?.GetValue(context) is not IList children) { return true; }
        
        foreach (var childObject in children)
        {
            var child = childObject as Component;
            if (!child || !child.gameObject.activeInHierarchy) { continue; }
            
            for (var type = child.GetType(); type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Window<>))
                {
                    return true;
                }
            }
        }
        return false;
    }
}