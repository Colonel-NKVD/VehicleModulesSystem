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
        public string Help => "Полный ремонт модулей";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "vehicle.repair" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            InteractableVehicle v = player.CurrentVehicle;

            if (v == null || !VehicleModulesPlugin.Instance.Configuration.Instance.TargetedVehicleIds.Contains(v.asset.id))
            {
                UnturnedChat.Say(player, "Вы должны быть внутри целевой техники!", Color.red);
                return;
            }

            bool isAtStation = false;
            float radius = VehicleModulesPlugin.Instance.Configuration.Instance.RepairStationRadius;
            ushort stationId = VehicleModulesPlugin.Instance.Configuration.Instance.RepairStationBarricadeId;

            // Профессиональный поиск объектов в радиусе без тяжелой физики
            List<Region3d> regions = new List<Region3d>();
            Regions.getRegionsInRadius(player.Position, radius, regions);
            foreach (var region in regions)
            {
                if (BarricadeManager.tryGetRegion(region.x, region.y, region.plant, out BarricadeRegion br))
                {
                    foreach (var drop in br.drops)
                    {
                        if (drop.asset.id == stationId && Vector3.Distance(drop.model.position, player.Position) <= radius)
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
                UnturnedChat.Say(player, "Ремонтная станция не обнаружена!", Color.red);
                return;
            }

            UnturnedChat.Say(player, "Начат капитальный ремонт (30 сек.)...", Color.yellow);
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
                    
                    v.askRepair(65000);
                    v.askFillFuel(65000);
                    v.batteryCharge = 100;
                    
                    UnturnedChat.Say(p, "ТЕХНИКА ПОЛНОСТЬЮ ВОССТАНОВЛЕНА", Color.green);
                }
            }
        }
    }
}
