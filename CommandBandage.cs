using Rocket.API;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using UnityEngine;
using System.Collections.Generic;

namespace VehicleModulesSystem
{
    public class CommandBandage : IRocketCommand
    {
        public string Name => "bandage";
        public string Help => "Использовать бинт, не выходя из техники";
        public string Syntax => "/bandage";
        public List<string> Aliases => new List<string> { "b" };
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public List<string> Permissions => new List<string> { "vehiclemodules.bandage" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;

            if (!player.IsInVehicle)
            {
                UnturnedChat.Say(player, "Эту команду можно использовать только внутри техники!", Color.red);
                return;
            }

            ushort bandageId = VehicleModulesPlugin.Instance.Configuration.Instance.BandageItemId;
            
            var items = player.Inventory.search(bandageId, true, true);
            if (items.Count == 0)
            {
                UnturnedChat.Say(player, "У вас нет подходящего бинта в инвентаре!", Color.red);
                return;
            }

            UnturnedChat.Say(player, "Перевязка начата... Ждите.", Color.yellow);
            
            VehicleModulesPlugin.Instance.StartCoroutine(VehicleModulesPlugin.Instance.BandageRoutine(player, bandageId));
        }
    }
}
