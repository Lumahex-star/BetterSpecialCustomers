using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using S1API.Quests;
using S1API.Dialogues;
using MelonLoader;
using S1API.Saveables;
using S1API.Internal.Abstraction;

namespace BetterSpecialCustomers.Quests
{
    public static class BikersQuest
    {

    }
    
    public class BikersQuestSave : Saveable
    {
        public static BikersQuestSave Instance { get; private set; }

        public BikersQuestSave() { Instance = this; }

        [SaveableField("bikers_quest_accepted")]
        public bool Accepted;
        protected override void OnLoaded() { Instance = this; }
    }
}