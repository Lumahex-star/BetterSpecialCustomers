using System;
using System.Collections.Generic;
using MelonLoader;
using S1API.Console;                // ConsoleItemAliases
using S1API.Entities;               // NPC
using S1API.Items;                  // ItemManager, LegalStatus
using S1API.Products;               // CustomProductItemCreator, ProductKindBuilder, Quality, ...
using S1API.Properties;             // EffectCreator, CustomEffect
using UnityEngine;
#if MONO
using GameRegistry = ScheduleOne.Registry;
using GameItemInstance = ScheduleOne.ItemFramework.ItemInstance;
using GameProductManager = ScheduleOne.Product.ProductManager;
using GameProductDefinition = ScheduleOne.Product.ProductDefinition;
using GameProductItemInstance = ScheduleOne.Product.ProductItemInstance;
using GameShroomDefinition = ScheduleOne.Product.ShroomDefinition;
using GamePackagingDefinition = ScheduleOne.Product.Packaging.PackagingDefinition;
using GameProductStored = ScheduleOne.Product.Product_Stored;
using GameQuality = ScheduleOne.ItemFramework.EQuality;
#elif IL2CPP
using GameRegistry = Il2CppScheduleOne.Registry;
using GameItemInstance = Il2CppScheduleOne.ItemFramework.ItemInstance;
using GameProductManager = Il2CppScheduleOne.Product.ProductManager;
using GameProductDefinition = Il2CppScheduleOne.Product.ProductDefinition;
using GameProductItemInstance = Il2CppScheduleOne.Product.ProductItemInstance;
using GameShroomDefinition = Il2CppScheduleOne.Product.ShroomDefinition;
using GamePackagingDefinition = Il2CppScheduleOne.Product.Packaging.PackagingDefinition;
using GameProductStored = Il2CppScheduleOne.Product.Product_Stored;
using GameQuality = Il2CppScheduleOne.ItemFramework.EQuality;
#endif

namespace BetterSpecialCustomers.Quests
{
    // Krystal's Cosmic Caps: the shrooms the hippies' package job carries. A mod-only product (nobody can grow, cook or
    // mix it), so a brick of it in the drop can only be the one Krystal left. It is registered through S1API's generic
    // custom product builder, which needs every player to run the mod and must happen before the save's items load.
    internal static class KrystalsShrooms
    {
        public const string OwnerId = "betterspecialcustomers";
        public const string ProductId = "betterspecialcustomers:krystals-cosmic-caps";
        private const string ProductKindId = "betterspecialcustomers:krystal-shrooms";
        private const string CosmicEffectId = "betterspecialcustomers_cosmic";
        public const string BrickPackagingId = "brick";

        // Regular shrooms are $65 a unit; Krystal's are 1.5x that ($97.50, which the game rounds to $98).
        // A brick holds 20 units, so one brick is worth about $1,960.
        private const float PricePerUnit = 98f;

        // How addictive the product is (0 to 1). Cosmic adds nothing on top, so this is the final value.
        private const float Addictiveness = 0.4f;

        // The deep purple the brick (and the Cosmic label's product colour) is tinted with.
        private static readonly Color DeepPurple = new Color(0.4f, 0.12f, 0.65f);

        // Cosmic: NPCs who take it move this much slower (0.9 = 10% slower).
        private const float CosmicNpcSpeedFactor = 0.9f;

        // Ids that may be the base shroom product; the first that exists lends its look. If none do, the game's product
        // list is searched for any shroom product.
        private static readonly string[] ShroomTemplateIds = { "shroom", "shrooms", "mushroom", "mushrooms" };

        private static ProductKind _kind;
        private static CustomEffect _cosmic;
        private static CustomProductDefinition _definition;
        private static bool _brickVisualRegistered;
        private static bool _looseVisualRegistered;
        private static string _templateId;
        private static GameObject _looseVisual;

        // NPCs currently slowed by Cosmic, so the slowdown is applied and removed once each.
        private static readonly HashSet<string> SlowedNpcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsRegistered => _definition != null;

        // Called on every save load, before the save's items are restored. Safe to call again: each part is built once.
        public static void Register()
        {
            try
            {
                SlowedNpcs.Clear(); // the NPCs of the previous save are gone
                if (_definition != null) return;

                var template = FindShroomTemplate();
                var brick = ItemManager.GetDefinition(BrickPackagingId) as PackagingDefinition;
                if (template == null || brick == null)
                {
                    Melon<Core>.Logger.Error($"[shrooms] cannot register Krystal's shrooms (shroom template found: {template != null}, brick found: {brick != null})");
                    return;
                }

                _kind ??= new ProductKindBuilder(ProductKindId)
                    .WithCompatibilityDrugType(DrugType.Shrooms) // shroom customers treat it as shrooms
                    .Build();
                _cosmic ??= BuildCosmicEffect();
                _templateId = template.ID;
                RegisterBrickVisual();
                RegisterLooseVisual(); // must come before the definition is built

                // No WithNativeMixerMap and no ProductMixingProfile: S1API rejects any attempt to mix it.
                _definition = CustomProductItemCreator
                    .CreateBuilder(ProductId, _kind)
                    .WithName("Krystal's Cosmic Caps")
                    .WithDescription("Krystal Bird's own blend. The hippies would very much like it back.")
                    .WithProductPrice(PricePerUnit)
                    .WithProperty(_cosmic)
                    .WithLegalStatus(LegalStatus.Illegal)
                    .WithBaseAddictiveness(Addictiveness)
                    .WithDefaultQuality(Quality.Premium)
                    .WithRepresentationsFrom(template)
                    .WithValidPackaging(brick)
                    .Build();

                ConsoleItemAliases.Register("cosmiccaps", ProductId); // "give cosmiccaps" in the console, for testing
                Melon<Core>.Logger.Msg($"[shrooms] registered '{ProductId}' (look borrowed from '{template.ID}')");
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Error("[shrooms] registering Krystal's shrooms failed: " + ex);
            }
        }

        private static ProductDefinition FindShroomTemplate()
        {
            foreach (string id in ShroomTemplateIds)
                if (ItemManager.GetDefinition(id) is ProductDefinition found) return found;

            var all = GameProductManager.Instance?.AllProducts;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var product = all[i];
                if (product == null) continue;
#if MONO
                bool isShroom = product is GameShroomDefinition;
#else
                bool isShroom = product.TryCast<GameShroomDefinition>() != null;
#endif
                if (isShroom && ItemManager.GetDefinition(product.ID) is ProductDefinition found) return found;
            }
            return null;
        }

        // Cosmic: a label only Krystal's shrooms carry (S1API places a custom effect outside every mixing map, so no mix
        // can produce it), plus a gentle slowdown on NPCs who take it.
        private static CustomEffect BuildCosmicEffect()
        {
            return EffectCreator.CreateBuilder()
                .WithBasicInfo(CosmicEffectId, "Cosmic", "You can see the universe breathing.")
                .WithTier(3)
                .WithAddictiveness(0f) // the product's own addictiveness covers it
                .WithColors(productColor: DeepPurple, labelColor: new Color(0.45f, 0.9f, 0.85f))
                .WithNpcBehavior(SlowNpc)
                .WithNpcClearBehavior(RestoreNpc)
                .Build();
        }

        private static void SlowNpc(NPC npc)
        {
            try
            {
                if (npc == null || !SlowedNpcs.Add(npc.ID)) return;
                npc.Movement.SpeedMultiplier *= CosmicNpcSpeedFactor;
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Warning("[shrooms] Cosmic slowdown failed: " + ex.Message);
            }
        }

        // May be called more than once; only undoes our own slowdown.
        private static void RestoreNpc(NPC npc)
        {
            try
            {
                if (npc == null || !SlowedNpcs.Remove(npc.ID)) return;
                npc.Movement.SpeedMultiplier /= CosmicNpcSpeedFactor;
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Warning("[shrooms] Cosmic restore failed: " + ex.Message);
            }
        }

        // A brick has no reusable shell, so S1API needs its look registered: the game's own shroom brick, tinted deep purple.
        private static void RegisterBrickVisual()
        {
            if (_brickVisualRegistered) return;
            var profile = new ProductPackagingContentProfileBuilder()
                .WithNativeFilledVisualScaffold(ProductPackagingVisualTemplate.Shrooms, TintBrick)
                .Build();
            ProductPackagingContentProfileRegistry.Register(OwnerId, ProductId, BrickPackagingId, profile);
            _brickVisualRegistered = true;
        }

        private static void TintBrick(GameObject clone)
        {
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
                Tint(renderer.material); // a per-clone copy, so the game's shared brick material is untouched
        }

        // The colour multiplies the material's texture, so its detail stays visible.
        private static void Tint(Material material)
        {
            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (material.HasProperty(property))
                    material.SetColor(property, DeepPurple);
        }

        // Loose (unpacked) shrooms: S1API swaps the borrowed look for this visual everywhere a loose product shows
        // (stored, held, on stations, in packaging stations) and draws the inventory icon from it.
        private static void RegisterLooseVisual()
        {
            if (_looseVisualRegistered) return;
            var profile = new ProductPresentationProfileBuilder()
                .WithLooseVisual(BuildLooseVisual)
                .WithGeneratedIconFromLooseVisual()
                .Build();
            ProductPresentationProfileRegistry.RegisterForProduct(OwnerId, ProductId, profile);
            _looseVisualRegistered = true;
        }

        // Copies the regular shrooms' meshes into a hidden object with deep purple materials. Built once and reused; S1API
        // clones it wherever it is needed. Null (logged) if the regular shrooms' model can't be found, which leaves the
        // borrowed look.
        private static GameObject BuildLooseVisual()
        {
            if (_looseVisual != null) return _looseVisual;

            var definition = GameRegistry.GetItem(_templateId);
#if MONO
            var template = definition as GameProductDefinition;
#else
            var template = definition?.TryCast<GameProductDefinition>();
#endif
            var stored = template?.StoredItem;
            var productStored = stored != null ? stored.GetComponent<GameProductStored>() : null;
            Transform container = productStored?.Visuals?.VisualsContainer ?? stored?.transform;
            if (container == null)
            {
                Melon<Core>.Logger.Warning($"[shrooms] no model found on '{_templateId}'; loose shrooms keep the regular look");
                return null;
            }

            var root = new GameObject("KrystalsCosmicCaps_Loose");
            root.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(root);

            // Only meshes and materials are copied, so none of the game's scripts on the model come along.
            var tinted = new Dictionary<Material, Material>();
            foreach (var filter in container.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;

                var part = new GameObject(filter.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = container.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation = Quaternion.Inverse(container.rotation) * filter.transform.rotation;
                Vector3 scale = filter.transform.lossyScale, parentScale = container.lossyScale;
                part.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

                var sources = renderer.sharedMaterials;
                var materials = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (source == null) continue;
                    if (!tinted.TryGetValue(source, out var copy))
                    {
                        copy = new Material(source);
                        Tint(copy);
                        tinted[source] = copy;
                    }
                    materials[i] = copy;
                }
                part.AddComponent<MeshRenderer>().sharedMaterials = materials;
            }

            if (root.transform.childCount == 0)
            {
                UnityEngine.Object.Destroy(root);
                Melon<Core>.Logger.Warning($"[shrooms] '{_templateId}' has no meshes to copy; loose shrooms keep the regular look");
                return null;
            }

            _looseVisual = root;
            return root;
        }

        // ---- Used by the hippies' job ----

        // One brick of Krystal's shrooms, or null (logged) if the product isn't registered.
        public static GameItemInstance CreateBrick()
        {
            var definition = GameRegistry.GetItem(ProductId);
            var packaging = GameRegistry.GetItem(BrickPackagingId);
#if MONO
            var product = definition as GameProductDefinition;
            var brick = packaging as GamePackagingDefinition;
#else
            var product = definition?.TryCast<GameProductDefinition>();
            var brick = packaging?.TryCast<GamePackagingDefinition>();
#endif
            if (product == null || brick == null)
            {
                Melon<Core>.Logger.Warning($"[shrooms] cannot make a brick (product found: {product != null}, brick found: {brick != null})");
                return null;
            }
            return new GameProductItemInstance(product, 1, GameQuality.Premium, brick);
        }

        // True for a brick of Krystal's shrooms. Unpacked shrooms don't count: the brick itself has to arrive.
        public static bool IsBrick(GameItemInstance item)
        {
            if (item == null || item.Definition == null || !string.Equals(item.Definition.ID, ProductId, StringComparison.OrdinalIgnoreCase))
                return false;
#if MONO
            var product = item as GameProductItemInstance;
#else
            var product = item.TryCast<GameProductItemInstance>();
#endif
            var packaging = product?.AppliedPackaging;
            return packaging != null && string.Equals(packaging.ID, BrickPackagingId, StringComparison.OrdinalIgnoreCase);
        }

        // Puts the product in the player's Product Manager, so a stolen brick can be listed and sold.
        public static void Discover()
        {
            try { _definition?.Discover(); }
            catch (Exception ex) { Melon<Core>.Logger.Warning("[shrooms] discovering Krystal's shrooms failed: " + ex.Message); }
        }
    }
}
