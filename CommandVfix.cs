using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class CommandVfix : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "vfix";
        public string Help => "Полный ремонт модулей и техники";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "vehiclemodules.vfix" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            InteractableVehicle vehicle = player.CurrentVehicle;

            if (vehicle == null)
            {
                UnturnedChat.Say(player, "Вы должны находиться внутри техники!", Color.yellow);
                return;
            }

            // 1. Полный ремонт через стандартный метод
            VehicleManager.repair(vehicle, 10000, 1);
            vehicle.askFillFuel(2000);

            // 2. Сброс состояния в датчике плагина
            if (VehicleModulesPlugin.Instance.TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
            {
                state.IsFuelTankBroken = false;
                state.IsTransmissionBroken = false;
                state.IsOnFire = false;
                state.IsSmoking = false;
                state.LastHealth = vehicle.health; // Критически важно обновить LastHealth!
                
                UnturnedChat.Say(player, "Техника и модули полностью восстановлены!", Color.green);
            }
        }
    }
}
