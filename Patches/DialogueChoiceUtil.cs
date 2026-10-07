using System;
using UnityEngine.Events;
#if MONO
using ScheduleOne.Dialogue;
#elif IL2CPP
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.Dialogue;
#endif

namespace BetterSpecialCustomers.Patches
{
    internal static class DialogueChoiceUtil
    {
        // Adds one option to an NPC's dialogue menu. "shouldShow" is asked every time the menu opens (the option is
        // hidden while it returns false); "onChosen" runs when the player picks it.
        //
        // On Mono the game's delegate types are normal C# delegates, so lambdas assign directly. On IL2CPP the game's
        // delegates (DialogueChoice.ShouldShowCheck, UnityAction) are a different, native kind, so our C# delegates have
        // to be converted first with DelegateSupport.ConvertDelegate.
        public static void Add(DialogueController controller, string text, Func<bool> shouldShow, Action onChosen)
        {
            var choice = new DialogueController.DialogueChoice();
            choice.ChoiceText = text;
            choice.Enabled = true;

#if MONO
            choice.shouldShowCheck = enabled => enabled && shouldShow();
            choice.onChoosen.AddListener(() => onChosen());
#elif IL2CPP
            Func<bool, bool> check = enabled => enabled && shouldShow();
            choice.shouldShowCheck = DelegateSupport.ConvertDelegate<DialogueController.DialogueChoice.ShouldShowCheck>(check);
            choice.onChoosen.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(onChosen));
#endif

            controller.AddDialogueChoice(choice);
        }
    }
}
