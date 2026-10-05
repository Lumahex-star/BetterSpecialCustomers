using HarmonyLib;
using MelonLoader;
#if MONO
using ScheduleOne.Dialogue;
using ScheduleOne.NPCs;
#elif IL2CPP
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.NPCs;
#endif

namespace BetterSpecialCustomers.Patches
{
    // TEMPORARY: prints the leader's dialogue containers and node GUIDs.
    [HarmonyPatch(typeof(SpecialCustomerLeader), nameof(SpecialCustomerLeader.SetCustomerData))]
    internal class LeaderDialogueDump
    {
        public static void Postfix(SpecialCustomerLeader __instance)
        {
            MelonLogger.Msg($"[dlg] leader set up: ID='{__instance.ID}'");

            var handler = __instance.DialogueHandler;
            if (handler == null) { MelonLogger.Msg("[dlg] DialogueHandler is null"); return; }

            var field = AccessTools.Field(typeof(DialogueHandler), "dialogueContainers");
            var property = field == null ? AccessTools.Property(typeof(DialogueHandler), "dialogueContainers") : null;
            MelonLogger.Msg($"[dlg] 'dialogueContainers' is a {(field != null ? "field" : property != null ? "property" : "NOT FOUND")}");

            object list = field != null ? field.GetValue(handler) : property?.GetValue(handler);
            if (list == null) { MelonLogger.Msg("[dlg] container list is null or unreadable"); return; }

            int count = (int)list.GetType().GetProperty("Count").GetValue(list);
            var getItem = list.GetType().GetMethod("get_Item");
            MelonLogger.Msg($"[dlg] {count} container(s)");

            for (int i = 0; i < count; i++)
            {
                var container = (Conversation)getItem.Invoke(list, new object[] { i });
                MelonLogger.Msg($"[dlg] container '{container.name}' ({container.DialogueNodeData.Count} nodes)");
                for (int n = 0; n < container.DialogueNodeData.Count; n++)
                {
                    var node = container.DialogueNodeData[n];
                    MelonLogger.Msg($"[dlg]   node guid={node.Guid} label='{node.DialogueNodeLabel}' text='{node.DialogueText}'");
                }
            }
        }
    }
}