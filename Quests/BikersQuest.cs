using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using S1API.Quests;
using S1API.Dialogues;

namespace BetterSpecialCustomers.Quests
{

    public class BikersQuest
    {
        DialogueInjector.Register(new DialogueInjection(
    npc: "diesel_rodd",
    container: "...",     // still needed, see below
    from: "...",
    to: "...",
    label: "YOUR_UNIQUE_LABEL",
    text: "...",
    onConfirmed: () => { ... }));
    }
}
