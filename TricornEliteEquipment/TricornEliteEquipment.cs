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
            IL.RoR2.EquipmentSlot.UpdateTargets += CheckForBossOrElite;
            IL.RoR2.EquipmentSlot.FireBossHunter += KillBossOrElite;
        }

        private void KillBossOrElite(ILContext il)
        {
            var c = new ILCursor(il);

            // The code does `pickup = deathRewards.bossDropTable.GeneratePickup(this.rng)`
            // and we want to convert it into
            // `pickup = deathRewards.bossDropTable ? deathRewards.bossDropTable.GeneratePickup() : GenerateOurPickup()`
            //
            // We can't just match the original code and put into an if-block, because mods
            // add their own EmitDelegate after GeneratePickup and we want to capture them too.
            // Therefore, the range of instructions that will go into the if-block, are
            // - from when we push deathRewards on the stack (first)
            // - until we load the next argument for CreatePickupDroplet (last)
            // Note: in vanilla these two sequences are next to each other
            if (!c.TryGotoNext(
                x => x.MatchLdloc(out _),
                x => x.MatchLdfld<DeathRewards>(nameof(DeathRewards.bossDropTable)),
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<EquipmentSlot>(nameof(EquipmentSlot.rng)),
                x => x.MatchCallOrCallvirt<PickupDropTable>(nameof(PickupDropTable.GeneratePickup))
            ))
            {
                Logger.LogError("Failed to patch EquipmentSlot.FireBossHunter #1");
                return;
            }
            var firstInstr = c.Next;

            if (!c.TryGotoNext(
                x => x.MatchLdloc(out _),
                x => x.MatchLdloc(out _),
                x => x.MatchLdcR4(out _),
                x => x.MatchCallOrCallvirt<Vector3>("op_Multiply"),
                x => x.MatchCallOrCallvirt<PickupDropletController>(nameof(PickupDropletController.CreatePickupDroplet))
            ))
            {
                Logger.LogError("Failed to patch EquipmentSlot.FireBossHunter #2");
                return;
            }
            var lastInstr = c.Next;

            // Inject the code that becomes the else-block
            c.Emit(OpCodes.Br_S, c.Next);
            c.Emit(OpCodes.Pop);
            var elseJump = c.Prev;
            c.Emit(OpCodes.Ldloc_1);
            c.EmitDelegate<Func<DeathRewards, UniquePickup>>(deathRewards =>
            {
                return new UniquePickup(PickupCatalog.FindPickupIndex(deathRewards.characterBody.inventory.currentEquipmentIndex));
            });

            // Add the if jump
            c.Goto(firstInstr);
            c.Index += 2;
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Brfalse, elseJump);
        }

        private void CheckForBossOrElite(ILContext il)
        {
            var c = new ILCursor(il);

            var hurtBoxVarIndex = -1;
            if (!c.TryGotoNext(
                x => x.MatchLdloc(out hurtBoxVarIndex),
                x => x.MatchLdfld<HurtBox>(nameof(HurtBox.healthComponent)),
                x => x.MatchCallOrCallvirt<UnityEngine.Object>("op_Implicit")))
            {
                Logger.LogError("Failed to patch EquipmentSlot.UpdateTargets #1");
                return;
            }

            if (!c.TryGotoNext(
                MoveType.After,
                x => x.MatchLdfld<DeathRewards>(nameof(DeathRewards.bossDropTable)),
                x => x.MatchCallOrCallvirt<UnityEngine.Object>("op_Implicit")))
            {
                Logger.LogError("Failed to patch EquipmentSlot.UpdateTargets #2");
                return;
            }

            c.Emit(OpCodes.Ldloc, hurtBoxVarIndex);
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