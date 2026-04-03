using HarmonyLib;
using SDG.Unturned;
using Steamworks;
using System;

namespace VehicleModulesSystem
{
    // Мы явно указываем типы параметров метода tellDrive, чтобы Harmony его нашел
    [HarmonyPatch(typeof(InteractableVehicle))]
    [HarmonyPatch("tellDrive")]
    [HarmonyPatch(new Type[] { typeof(CSteamID), typeof(byte), typeof(byte), typeof(ushort), typeof(ushort) })]
    public class TransmissionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(InteractableVehicle __instance, CSteamID steamID, byte x, byte y, ushort steering, ushort speed)
        {
            try
            {
                // Проверка на null самого плагина, чтобы не вылетало при запуске
                if (VehicleModulesPlugin.Instance == null) return true;

                var state = VehicleModulesPlugin.Instance.GetVehicleState(__instance);
                
                if (state != null && state.IsTransmissionBroken)
                {
                    // Если трансмиссия сломана — игнорируем ввод (машина не поедет)
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Если что-то пошло не так внутри патча, просто логируем и не вешаем сервер
                Rocket.Core.Logging.Logger.Log("Error in TransmissionPatch: " + ex.Message);
            }
            
            return true;
        }
    }
}
