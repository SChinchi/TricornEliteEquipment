using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Security.Permissions;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace TricornEliteEquipment
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class TricornEliteEquipment : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "GChinchi";
        public const string PluginName = "TricornEliteEquipment";
        public const string PluginVersion = "1.0.2";

        public void Awake()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;
            IL.RoR2.EquipmentSlot.FireBossHunter += EquipmentSlot_FireBossHunter;
        }

        private void EquipmentSlot_FireBossHunter(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(
                x => x.MatchLdloc(2),
                x => x.MatchLdloc(3),
                x => x.MatchLdcR4(15f),
                x => x.MatchCall<Vector3>("op_Multiply"),
                x => x.MatchCall<PickupDropletController>("CreatePickupDroplet")
            ))
            {
                Logger.LogError("Failed to patch EquipmentSlot.FireBossHunter #1");
                return;
            }
            // We want to modify `deathRewards.bossDropTable.GenerateDrop()`
            // but ItemBlacklist is also doing that, so to coexist we add our
            // new custom behaviour as an else block.
            c.Emit(OpCodes.Br_S, c.Next);
            c.Emit(OpCodes.Pop);
            var elseJump = c.Prev;
            c.Emit(OpCodes.Ldloc_1);
            c.EmitDelegate<Func<DeathRewards, PickupIndex>>(deathRewards =>
            {
                return PickupCatalog.FindPickupIndex(deathRewards.characterBody.inventory.currentEquipmentIndex);
            });
            if (!c.TryGotoPrev(
                MoveType.Before,
                x => x.MatchLdloc(1),
                x => x.MatchLdfld<DeathRewards>("bossDropTable"),
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<EquipmentSlot>("rng"),
                x => x.MatchCallvirt<PickupDropTable>("GenerateDrop")
            ))
            {
                Logger.LogError("Failed to patch EquipmentSlot.FireBossHunter #2");
                return;
            }
            // Turning the original code into an if block
            c.Index += 2;
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Brfalse, elseJump);
        }

        private void EquipmentSlot_UpdateTargets(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(x => x.MatchLdfld<DeathRewards>("bossDropTable")))
            {
                Logger.LogError("Failed to patch EquipmentSlot.UpdateTargets");
                return;
            }
            c.Index += 2;
            c.Emit(OpCodes.Ldloc_S, (byte)8);
            c.EmitDelegate<Func<bool, HurtBox, bool>>((hasBossDropTable, hurtBox) =>
            {
                if (hasBossDropTable)
                {
                    return true;
                }
                var inventory = hurtBox.healthComponent.body.inventory;
                if (!inventory)
                {
                    return false;
                }
                var equipment = EquipmentCatalog.GetEquipmentDef(inventory.currentEquipmentIndex);
                if (!equipment)
                {
                    return false;
                }
                return equipment.dropOnDeathChance > 0f;
            });
        }
    }
}