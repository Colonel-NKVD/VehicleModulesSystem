using Rocket.API;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using System.Collections.Generic;
using UnityEngine;
using SDG.Unturned;

namespace VehicleModulesSystem
{
    public class CommandVfix : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "vfix";
        public string Help => "Ремонт модулей танка";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "tank.repair" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            InteractableVehicle v = player.CurrentVehicle;

            if (v == null || !VehicleModulesPlugin.Instance.Configuration.Instance.TargetedVehicleIds.Contains(v.asset.id))
            {
                UnturnedChat.Say(player, "Вы должны находиться в танке!", Color.red);
                return;
            }

            // Проверка ремзоны
            bool isAtStation = false;
            float dist = VehicleModulesPlugin.Instance.Configuration.Instance.RepairStationRadius;
            ushort stationId = VehicleModulesPlugin.Instance.Configuration.Instance.RepairStationBarricadeId;

            // Ищем ближайшие баррикады через стандартный поиск (эффективнее Physics)
            List<Region3d> regions = new List<Region3d>();
            Regions.getRegionsInRadius(player.Position, dist, regions);

            foreach (var region in regions)
            {
                if (BarricadeManager.tryGetRegion(region.x, region.y, region.plant, out BarricadeRegion br))
                {
                    foreach (var drop in br.drops)
                    {
                        if (drop.asset.id == stationId && Vector3.Distance(drop.model.position, player.Position) <= dist)
                        {
                            isAtStation = true;
                            break;
                        }
                    }
                }
                if (isAtStation) break;
            }

            if (!isAtStation)
            {
                UnturnedChat.Say(player, "Ремонтная станция не найдена рядом!", Color.red);
                return;
            }

            UnturnedChat.Say(player, "Ремонт начат... Не покидайте машину 30 секунд.", Color.yellow);
            VehicleModulesPlugin.Instance.StartCoroutine(RepairTimer(player, v));
        }

        private IEnumerator<WaitForSeconds> RepairTimer(UnturnedPlayer p, InteractableVehicle v)
        {
            yield return new WaitForSeconds(30f);

            if (v != null && !v.isExploded && p.CurrentVehicle == v)
            {
                if (VehicleModulesPlugin.Instance.TrackedVehicles.TryGetValue(v.instanceID, out var state))
                {
                    state.IsFuelTankBroken = false;
                    state.IsGunBroken = false;
                    state.IsTransmissionBroken = false;
                    state.IsOnFire = false;
                    state.IsSmoking = false;
                    
                    v.askRepair(65000); // Полный ремонт ХП
                    v.askFillFuel(65000); // Заправка
                    v.batteryCharge = 100;
                    
                    UnturnedChat.Say(p, "ТЕХНИКА ПОЛНОСТЬЮ ВОССТАНОВЛЕНА", Color.green);
                }
            }
        }
    }
}
