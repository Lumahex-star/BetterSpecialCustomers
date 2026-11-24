using System.Collections.Generic;
using MelonLoader;
using S1API.Entities;                              // NPC, NPCPrefabBuilder, Player
using S1API.Entities.Appearances.AccessoryFields;  // Chest, Feet, Bottom
using S1API.Entities.Equippables;                  // Weapon (weapon paths)
using S1API.Leveling;                              // LevelManager, FullRank, Rank
using UnityEngine;

namespace BetterSpecialCustomers.Quests
{
    // A rival club member for Diesel's Club Brawl job. Like the informant, S1API creates these when the world loads
    // and there's no way to make more later, so they're "parked" far away until a brawl needs them.
    //
    // This class is ABSTRACT: a blueprint for other blueprints. S1API skips abstract classes, so it only creates the
    // small RivalBiker1, RivalBiker2... classes at the bottom of this file, one NPC each.
    public abstract class RivalBiker : NPC
    {
        // Every rival that exists in the world right now. Each one adds itself when it's created.
        public static readonly List<RivalBiker> All = new List<RivalBiker>();

        // Each rival class fills this in with its own id (see the bottom of the file).
        protected abstract string NpcId { get; }

        public override bool IsPhysical => true;

        // True when he's out of the fight: dead or knocked out.
        public bool IsDown => IsDead || !IsConscious;

        public bool IsFighting
        {
            get
            {
                var combat = QuestNpcUtil.FindGameNpc(NpcId)?.Behaviour?.CombatBehaviour;
                return combat != null && combat.Active;
            }
        }

        // Below this rank they fight with knives, from it with guns (the same rule as the informant).
        private static readonly FullRank GunRank = new FullRank(Rank.Hustler, 3);

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            builder.WithIdentity(NpcId, "Rival", "Biker")
                   .WithSpawnPosition(QuestNpcUtil.ParkedPosition);
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Appearance.Build();

            // Forget rivals from a save we've left (their game objects are gone), then add this one.
            All.RemoveAll(r => r == null || r.gameObject == null);
            if (!All.Contains(this)) All.Add(this);

            Park(); // out of sight until a brawl needs him
            Melon<Core>.Logger.Msg($"[quest] rival biker '{NpcId}' was created");
        }

        // Brings him into the world at a position, alive, dressed and armed.
        public void Deploy(Vector3 position)
        {
            QuestNpcUtil.Revive(this);   // back to full health, and killable
            ApplyLook();
            Aggressiveness = 1f;
            CombatBehaviour.DefaultWeaponAssetPath =
                LevelManager.Exists && LevelManager.CurrentRank >= GunRank ? Weapon.M1911 : Weapon.Knife;
            Position = position;         // move him to the ambush point
        }

        // Sends him after a player.
        public void Attack(Player player) => CombatBehaviour.SetAndAttackTarget(player);

        // Takes him out of the world: invincible and far away.
        public void Park() => QuestNpcUtil.Park(this, NpcId);

        // His biker look.
        private void ApplyLook()
        {
            AppearanceUtil.ClearLayers(Appearance);

            // A simple starter look, so you can see it works.
            // TODO: copy the rest of Informant.ApplyBikerLook() in here (hair, skin, face, tattoos, extras),
            // so the rivals look as varied as the informant.
            Color leather = new Color(0.08f, 0.08f, 0.08f);
            AppearanceUtil.Wear<Chest>(Appearance, new Color(0.15f, 0.15f, 0.15f), "tshirt");
            AppearanceUtil.Wear<Bottom>(Appearance, new Color(0.2f, 0.25f, 0.4f), "jeans");
            AppearanceUtil.Wear<Chest>(Appearance, leather, "bikerjacket", "openvest");
            AppearanceUtil.Wear<Feet>(Appearance, new Color(0.1f, 0.1f, 0.1f), "combatboots");

            AppearanceUtil.ApplyNow(Appearance);
        }
    }

    // One tiny class per rival. Want bigger brawls? Copy a line and bump the number!
    public sealed class RivalBiker1 : RivalBiker { protected override string NpcId => "brawl_rival_1"; }
    public sealed class RivalBiker2 : RivalBiker { protected override string NpcId => "brawl_rival_2"; }
    public sealed class RivalBiker3 : RivalBiker { protected override string NpcId => "brawl_rival_3"; }
    public sealed class RivalBiker4 : RivalBiker { protected override string NpcId => "brawl_rival_4"; }
    public sealed class RivalBiker5 : RivalBiker { protected override string NpcId => "brawl_rival_5"; }
    public sealed class RivalBiker6 : RivalBiker { protected override string NpcId => "brawl_rival_6"; }
}
