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
using GameShroomVisualsSetter = ScheduleOne.Product.ShroomVisualsSetter;
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
using GameShroomVisualsSetter = Il2CppScheduleOne.Product.ShroomVisualsSetter;
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

        // The look, in the terms of the game's own ShroomAppearanceSettings. The game normally works these out from a
        // product's effects (ShroomDefinition.GetAppearanceSettings); with Cosmic as the only effect that would give
        // mostly pale lavender caps, so Krystal's are set here.
        private static readonly Color32 PrimaryColor = new Color32(74, 20, 140, 255);    // deep purple
        private static readonly Color32 SecondaryColor = new Color32(150, 90, 210, 255); // lighter violet
        private const bool HasSpots = true;
        private static readonly Color32 SpotsColor = new Color32(115, 230, 215, 255);    // cosmic teal

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
        private static GameObject _looseVisual;
        private static Material _mushroomMaterial; // loose shrooms
        private static Material _bulkMaterial;     // the brick's contents

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

        // The game's base shroom product, the one every shroom mix starts from.
        private static GameShroomDefinition DefaultShroom() => GameProductManager.Instance?.DefaultShroom;

        private static ProductDefinition FindShroomTemplate()
        {
            var defaultShroom = DefaultShroom();
            if (defaultShroom != null && ItemManager.GetDefinition(defaultShroom.ID) is ProductDefinition basic) return basic;

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
                .WithColors(productColor: PrimaryColor, labelColor: new Color(0.45f, 0.9f, 0.85f))
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

        // ---- Look ----
        // Shrooms are coloured by the game's own shroom shaders: a mushroom material (cap, stem, underside and spot
        // colours) on loose shrooms and a bulk material (primary, secondary and spot colours) inside bricks. The game's
        // ShroomVisualsSetter only colours products that are ShroomDefinitions, which Krystal's (a generic custom product)
        // is not, so we let it apply the regular shrooms' materials and then swap in copies with Krystal's colours.

        // Builds Krystal's two materials from the regular shrooms', with the colour recipe of the game's
        // ShroomDefinition.GenerateMaterials. False (logged) if the regular shrooms' materials can't be found.
        private static bool EnsureMaterials()
        {
            if (_mushroomMaterial != null && _bulkMaterial != null) return true;
            var shroom = DefaultShroom();
            if (shroom == null || shroom.ShroomMaterial == null || shroom.BulkMaterial == null)
            {
                Melon<Core>.Logger.Warning("[shrooms] the regular shrooms' materials weren't found; Krystal's keep the regular colours");
                return false;
            }

            Color primary = PrimaryColor, secondary = SecondaryColor;
            Color spots = HasSpots ? (Color)SpotsColor : Color.clear;

            _mushroomMaterial = new Material(shroom.ShroomMaterial) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            _mushroomMaterial.SetColor("_CapInnerColor", primary);
            _mushroomMaterial.SetColor("_CapOuterColor", Color.Lerp(primary, secondary, 0.7f));
            _mushroomMaterial.SetColor("_StemLowerColor", primary);
            _mushroomMaterial.SetColor("_StemUpperColor", Color.Lerp(secondary, Color.white, 0.3f));
            _mushroomMaterial.SetColor("_UnderColor", Color.Lerp(primary, new Color32(80, 80, 80, 255), 0.6f));
            _mushroomMaterial.SetColor("_SpotsColor", spots);

            _bulkMaterial = new Material(shroom.BulkMaterial) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            _bulkMaterial.SetColor("_PrimaryColor", primary);
            _bulkMaterial.SetColor("_SecondaryColor", secondary);
            _bulkMaterial.SetColor("_SpotsColor", spots);
            return true;
        }

        // Swaps the regular shrooms' materials on a renderer for Krystal's; anything else (like the brick's wrapping)
        // is left alone. True if anything changed.
        private static bool Recolor(Renderer renderer)
        {
            var shroom = DefaultShroom();
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;
                if (material == shroom.ShroomMaterial || material.HasProperty("_CapInnerColor"))
                    materials[i] = _mushroomMaterial;
                else if (material == shroom.BulkMaterial || (material.HasProperty("_PrimaryColor") && material.HasProperty("_SecondaryColor")))
                    materials[i] = _bulkMaterial;
                else
                    continue;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
            return changed;
        }

        // A brick has no reusable shell, so S1API needs its look registered: the game's own shroom brick, recoloured.
        private static void RegisterBrickVisual()
        {
            if (_brickVisualRegistered) return;
            var profile = new ProductPackagingContentProfileBuilder()
                .WithNativeFilledVisualScaffold(ProductPackagingVisualTemplate.Shrooms, ColorBrick)
                .Build();
            ProductPackagingContentProfileRegistry.Register(OwnerId, ProductId, BrickPackagingId, profile);
            _brickVisualRegistered = true;
        }

        private static void ColorBrick(GameObject clone)
        {
            if (!EnsureMaterials()) return;
            var shroom = DefaultShroom();
            foreach (var setter in clone.GetComponentsInChildren<GameShroomVisualsSetter>(true))
                setter.ApplyVisuals(shroom); // the regular shroom materials first, in case the scaffold has placeholders
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
                Recolor(renderer);
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

        // A hidden copy of the regular shrooms' model in Krystal's colours. Built once and reused; S1API clones it wherever
        // it is needed. Null (logged) if it can't be built, which leaves the borrowed look.
        private static GameObject BuildLooseVisual()
        {
            if (_looseVisual != null) return _looseVisual;
            var shroom = DefaultShroom();
            var stored = shroom?.StoredItem;
            if (stored == null || !EnsureMaterials())
            {
                Melon<Core>.Logger.Warning("[shrooms] no regular shroom model found; loose shrooms keep the regular look");
                return null;
            }

            // Clone the regular model under a hidden parent (so none of its scripts wake up) and let the game put its
            // shroom materials on it, then copy only the meshes, so none of its scripts come along.
            var scratch = new GameObject("KrystalsCosmicCaps_Scratch");
            scratch.SetActive(false);
            var root = new GameObject("KrystalsCosmicCaps_Loose");
            root.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(root);
            bool recolored = false;
            try
            {
                var model = UnityEngine.Object.Instantiate(stored.gameObject, scratch.transform);
                foreach (var setter in model.GetComponentsInChildren<GameShroomVisualsSetter>(true))
                    setter.ApplyVisuals(shroom);
                var visuals = model.GetComponent<GameProductStored>()?.Visuals;
                Transform container = visuals != null && visuals.VisualsContainer != null ? visuals.VisualsContainer : model.transform;

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
                    var copy = part.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = renderer.sharedMaterials;
                    recolored |= Recolor(copy);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(scratch);
            }

            if (root.transform.childCount == 0)
            {
                UnityEngine.Object.Destroy(root);
                Melon<Core>.Logger.Warning("[shrooms] the regular shroom model has no meshes; loose shrooms keep the regular look");
                return null;
            }
            if (!recolored) Melon<Core>.Logger.Warning("[shrooms] no shroom materials found on the loose model; it keeps the regular colours");

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
