using Rocket.API;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class CommandBandage : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bandage";
        public string Help => "Использовать бинт внутри техники";
        public string Syntax => "/b";
        public List<string> Aliases => new List<string> { "b" };
        public List<string> Permissions => new List<string> { "vehiclemodules.bandage" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            if (player.Player.movement.getVehicle() == null)
            {
                UnturnedChat.Say(caller, "Команда доступна только внутри техники!", Color.red);
                return;
            }

            ushort bandageId = VehicleModulesPlugin.Instance.Configuration.Instance.BandageItemID;
            
            // Ищем бинт в инвентаре
            InventorySearch search = player.Inventory.has(bandageId);
            if (search == null)
            {
                UnturnedChat.Say(caller, "у вас нет бинтов!", Color.red);
                return;
            }

            UnturnedChat.Say(caller, "Перевязка... Ожидайте 4 сек.", Color.yellow);
            VehicleModulesPlugin.Instance.StartCoroutine(BandageRoutine(player, search));
        }

        private IEnumerator BandageRoutine(UnturnedPlayer p, InventorySearch s)
        {
            yield return new WaitForSeconds(4.0f);

            if (p != null && p.Player.movement.getVehicle() != null)
            {
                // Снова проверяем наличие предмета (чтобы не выкинул)
                if (p.Inventory.has(s.jar.item.id) != null)
                {
                    p.Inventory.removeItem(s.page, p.Inventory.getIndex(s.page, s.jar.x, s.jar.y));
                    p.Heal(25, true, true); // Лечим 25 ХП, стопаем кровоток и лечим кости
                    UnturnedChat.Say(p, "Раны перевязаны.", Color.green);
                }
            }
        }
    }
}
