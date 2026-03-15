using Rocket.API;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;

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

            // Получаем состояние танка из твоего главного плагина
            VehicleState state = VehicleModulesPlugin.Instance.GetVehicleState(v);
            if (state == null) return;

            if (state.IsRepairing)
            {
                UnturnedChat.Say(caller, "Техника уже находится в процессе починки!", Color.yellow);
                return;
            }

            // Значения ID баррикады и радиуса желательно вынести в твой Config
            ushort repairStationId = 287; // ЗАМЕНИ НА ID ТВОЕЙ БАРРИКАДЫ СТАНЦИИ
            float repairRadius = 15.0f; // Радиус ауры починки

            if (!ModuleDamageHandler.IsNearRepairStation(v.transform.position, repairStationId, repairRadius))
            {
                UnturnedChat.Say(caller, "Поблизости нет инженерной станции для починки!", Color.red);
                return;
            }

            // Запускаем корутину починки
            VehicleModulesPlugin.Instance.StartCoroutine(ModuleDamageHandler.RepairRoutine(v, state, repairStationId, repairRadius));
        }
    }
}
