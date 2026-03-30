using HarmonyLib;
using SDG.Unturned;
using Steamworks;

namespace VehicleModulesSystem
{
    // Перехватываем метод запуска двигателя
    [HarmonyPatch(typeof(InteractableVehicle), "askEngine")]
    public class TransmissionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(InteractableVehicle __instance, CSteamID steamID, ref bool state)
        {
            // Разрешаем заглушить двигатель в любом случае
            if (!state) return true; 

            var vState = VehicleModulesPlugin.Instance.GetVehicleState(__instance);
            
            // Если трансмиссия сломана — блокируем попытку завестись
            if (vState != null && vState.IsTransmissionBroken)
            {
                return false; 
            }
            
            return true;
        }
    }
}
