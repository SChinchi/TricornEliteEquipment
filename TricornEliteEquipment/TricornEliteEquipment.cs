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
        public const string PluginVersion = "1.0.0";

        public void Awake()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;
            IL.RoR2.EquipmentSlot.FireBossHunter += EquipmentSlot_FireBossHunter;
        }

        private void EquipmentSlot_FireBossHunter(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(
                x => x.MatchLdloc(1),
                x => x.MatchLdfld<DeathRewards>("bossDropTable"),
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<EquipmentSlot>("rng"),
                x => x.MatchCallvirt<PickupDropTable>("GenerateDrop"),
                x => x.MatchLdloc(2),
                x => x.MatchLdloc(3),
                x => x.MatchLdcR4(15f),
                x => x.MatchCall<Vector3>("op_Multiply"),
                x => x.MatchCall<PickupDropletController>("CreatePickupDroplet")
            ))
            {
                Logger.LogError("Failed to patch EquipmentSlot.FireBossHunter");
                return;
            }
            c.RemoveRange(10);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc_1);
            c.Emit(OpCodes.Ldloc_2);
            c.Emit(OpCodes.Ldloc_3);
            c.EmitDelegate<Action<EquipmentSlot, DeathRewards, Vector3, Vector3>>((equipmentSlot, deathRewards, vector, normalized) =>
            {
                PickupIndex pickupIndex;
                if (deathRewards.bossDropTable != null)
                {
                    pickupIndex = deathRewards.bossDropTable.GenerateDrop(equipmentSlot.rng);
                }
                else
                {
                    pickupIndex = PickupCatalog.FindPickupIndex(deathRewards.characterBody.inventory.currentEquipmentIndex);
                }
                if (pickupIndex != PickupIndex.none)
                {
                    PickupDropletController.CreatePickupDroplet(pickupIndex, vector, normalized * 15f);
                }
            });
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