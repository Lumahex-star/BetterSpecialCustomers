using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
#if MONO
using ScheduleOne;
using ScheduleOne.Clothing;
#elif IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.Clothing;
#endif

namespace BetterSpecialCustomers.Diagnostics
{
    // TEMPORARY: lists every clothing item in the game (id, name, slot and the avatar asset path) to the log,
    // so we can find the biker clothes. Press F9 in game. Remove once the biker outfit has been picked.
    internal static class ClothingDump
    {
        public static void Run()
        {
            var log = Melon<Core>.Logger;
            try
            {
                var items = Registry.Instance.GetAllItems();
                int count = 0;
                foreach (var item in items)
                {
#if MONO
                    var clothing = item as ClothingDefinition;
#else
                    var clothing = item.TryCast<ClothingDefinition>();
#endif
                    if (clothing == null) continue;
                    count++;
                    log.Msg($"[clothing] id='{clothing.ID}' name='{clothing.Name}' slot={clothing.Slot} {DescribeAvatarObject(clothing)}");
                }
                log.Msg($"[clothing] {count} clothing item(s) listed");
            }
            catch (Exception ex)
            {
                log.Error("[clothing] dump failed: " + ex);
            }
        }

        // TEMPORARY: lists EVERY item in the game (id, name, type), to pick quest rewards and check item ids. Press F10.
        public static void RunAllItems()
        {
            var log = Melon<Core>.Logger;
            try
            {
                int count = 0;
                foreach (var item in Registry.Instance.GetAllItems())
                {
                    if (item == null) continue;
                    count++;
                    log.Msg($"[item] id='{item.ID}' name='{item.Name}' type={item.GetType().Name}");
                }
                log.Msg($"[item] {count} item(s) listed");
            }
            catch (Exception ex)
            {
                log.Error("[item] dump failed: " + ex);
            }
        }

        // We don't know the avatar object's field names, so print every string it exposes (one is the asset path).
        private static string DescribeAvatarObject(ClothingDefinition clothing)
        {
            object avatarObject = clothing.ClothingAvatarObject;
            if (avatarObject == null) return "(no avatar object)";

            var parts = avatarObject.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
                .Select(p => $"{p.Name}='{SafeGet(() => p.GetValue(avatarObject))}'")
                .Concat(avatarObject.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType == typeof(string))
                    .Select(f => $"{f.Name}='{SafeGet(() => f.GetValue(avatarObject))}'"));
            return string.Join(" ", parts);
        }

        private static string SafeGet(Func<object> get)
        {
            try { return get()?.ToString() ?? string.Empty; } catch { return "?"; }
        }
    }
}
