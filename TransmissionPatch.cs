using HarmonyLib;
using SDG.Unturned;
using Steamworks;
using System;

namespace VehicleModulesSystem
{
    // Явно указываем класс и метод, а также типы всех аргументов
    [HarmonyPatch(typeof(InteractableVehicle))]
    [HarmonyPatch("tellDrive")]
    [HarmonyPatch(new Type[] { typeof(CSteamID), typeof(byte), typeof(byte), typeof(ushort), typeof(ushort) })]
    public class TransmissionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(InteractableVehicle __instance, CSteamID steamID, byte x, byte y, ushort steering, ushort speed)
        {
            // Защита от ошибок при инициализации
            if (VehicleModulesPlugin.Instance == null) return true;

            try
            {
                var state = VehicleModulesPlugin.Instance.GetVehicleState(__instance);
                
                // Если трансмиссия сломана — блокируем выполнение оригинального метода (машина не поедет)
                if (state != null && state.IsTransmissionBroken)
                {
                    return false; 
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, если что-то внутри пошло не так, но не даем серверу упасть
                Rocket.Core.Logging.Logger.Log("Ошибка в патче Transmission: " + ex.Message);
            }
            
            return true;
        }
    }
}
