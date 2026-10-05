using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using S1API.Quests;
using S1API.Dialogues;
using MelonLoader;

namespace BetterSpecialCustomers.Quests
{
    public static class BikersQuest
    {
        // Call this once from Core.OnInitializeMelon().
        public static void Register()
        {
            DialogueInjector.Register(new DialogueInjection(
                npc: "diesel_rodd",
                container: "...",   // TODO: real container name
                from: "INTRO_NODE_GUID",        // TODO: real source node GUID
                to: "...",          // TODO: real destination node GUID
                label: "BSC_BIKERS_QUEST",
                text: "You got any jobs for me?",        // TODO: text shown to the player
                onConfirmed: () =>
                {
                    MelonLogger.Msg("Quest Button Clicked");
                }));
        }
    }
}