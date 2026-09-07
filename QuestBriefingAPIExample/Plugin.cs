// ReSharper disable RedundantArgumentDefaultValue

using System.Reflection;
using Manimal.QuestBriefingAPI;
using BepInEx;

namespace Manimal.QuestBriefingAPIExample;

[BepInPlugin("com.yourname.questbriefingexample", "QuestBriefingAPI Example", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        var assembly = Assembly.GetExecutingAssembly();

        BriefingApi.Register(
            "questbriefingexample",
            "657315df034d76585f032e01",
            assembly, 
            "qbe.ogg",
            null,
            null
        );
    }
}