using Rocket.API;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace VehicleModulesSystem
{
    public class CommandRepairTank : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "repairtank";
        public string Help => "Инициировать починку техники на спец. станции";
        public string Syntax => "/repairtank";
        public List<string> Aliases => new List<string> { "rt" };
        public List<string> Permissions => new List<string> { "vehiclemodules.repair" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            InteractableVehicle v = player.Player.movement.getVehicle();

            if (v == null)
            {
                UnturnedChat.Say(caller, "Вы должны находиться внутри техники для начала ремонта!", Color.red);
                return;
            }

            VehicleState state = VehicleModulesPlugin.Instance.GetVehicleState(v);
            if (state == null) return;

            if (state.IsRepairing)
            {
                UnturnedChat.Say(caller, "Техника уже находится в процессе починки!", Color.yellow);
                return;
            }

            ushort repairStationId = VehicleModulesPlugin.Instance.Configuration.Instance.RepairStationId;
            float repairRadius = 15.0f; 

            if (!IsNearRepairStation(v.transform.position, repairStationId, repairRadius))
            {
                UnturnedChat.Say(caller, "Поблизости нет инженерной станции для починки!", Color.red);
                return;
            }

            VehicleModulesPlugin.Instance.StartCoroutine(RepairRoutine(v, state, repairStationId, repairRadius));
        }

        private IEnumerator RepairRoutine(InteractableVehicle v, VehicleState s, ushort stationId, float radius)
        {
            s.IsRepairing = true;
            ModuleDamageHandler.SendChat(v, ">> ИНИЦИИРОВАН ПОЛЕВОЙ РЕМОНТ. НЕ ПОКИДАЙТЕ ЗОНУ 35 СЕКУНД <<", Color.cyan);
            
            int repairTime = 35;
            
            for (int i = 0; i < repairTime; i++)
            {
                if (v == null || v.isExploded) 
                {
                    if (s != null) s.IsRepairing = false;
                    yield break;
                }

                if (!IsNearRepairStation(v.transform.position, stationId, radius))
                {
                    ModuleDamageHandler.SendChat(v, "!!! РЕМОНТ ПРЕРВАН: Техника покинула зону обслуживания !!!", Color.red);
                    s.IsRepairing = false;
                    yield break;
                }

                if (i > 0 && i % 10 == 0) 
                {
                    ModuleDamageHandler.SendChat(v, $"... Восстановление систем: осталось {repairTime - i} сек ...", Color.gray);
                }

                yield return new WaitForSeconds(1.0f);
            }

            v.askRepair(v.asset.health); 
            v.batteryCharge = 10000; // Восстанавливаем аккумулятор, чтобы починенная трансмиссия заработала
            VehicleManager.sendVehicleHealth(v, v.health); 
            VehicleManager.sendVehicleFuel(v, v.fuel);
            
            s.IsFuelTankBroken = false;
            s.IsTransmissionBroken = false; // Починка трансмиссии
            s.IsGunBroken = false;
            s.IsOnFire = false;
            s.IsSmoking = false;
            s.IsStunned = false;
            s.IsRepairing = false;
            
            ModuleDamageHandler.SendChat(v, ">> ТЕХНИКА ПОЛНОСТЬЮ ВОССТАНОВЛЕНА. ГОТОВНОСТЬ К БОЮ 100% <<", Color.green);
        }

        private bool IsNearRepairStation(Vector3 position, ushort targetId, float radius)
        {
            float sqrRadius = radius * radius;
            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
            {
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    if (BarricadeManager.regions[x, y] != null)
                    {
                        foreach (BarricadeDrop drop in BarricadeManager.regions[x, y].drops)
                        {
                            if (drop.asset.id == targetId && (drop.model.position - position).sqrMagnitude <= sqrRadius)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
