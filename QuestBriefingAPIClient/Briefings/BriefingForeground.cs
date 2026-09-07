using System;
using System.Collections;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace Manimal.QuestBriefingAPI
{
    internal static class BriefingForeground
    {
        // ItemUiContext registers open windows in its input children, including
        // the DelayTypeWindow used for quest completion and queued quest messages.
        private static readonly FieldInfo InputChildren = AccessTools.Field(typeof(ItemUiContext), "_children");

        internal static void Validate()
        {
            if (InputChildren == null)
                throw new MissingFieldException("ItemUiContext input window list was not found.");
        }

        internal static bool HasForegroundWindow()
        {
            var context = ItemUiContext.Instance;
            if (context == null) return false;
            if (!(InputChildren?.GetValue(context) is IList children)) return true;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i] as Component;
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                for (var type = child.GetType(); type != null; type = type.BaseType)
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Window<>))
                        return true;
            }
            return false;
        }
    }
}
