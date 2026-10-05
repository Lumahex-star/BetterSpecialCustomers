using S1API.Internal.Abstraction;   // Saveable
using S1API.Saveables;              // SaveableField

namespace BetterSpecialCustomers.Quests
{
    public class BikersQuestSave : Saveable
    {
        public static BikersQuestSave Instance { get; private set; }

        public BikersQuestSave() { Instance = this; }

        [SaveableField("bikers_quest_accepted")]
        public bool Accepted;

        protected override void OnLoaded() { Instance = this; }
    }
}
