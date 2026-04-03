using HarmonyLib;
using SDG.Unturned;
using System;

namespace VehicleModulesSystem
{
    public class TransmissionPatch
    {
        // Используем универсальный префикс без жесткой привязки к аргументам через __args
        public static bool Prefix(InteractableVehicle __instance)
        {
            try
            {
                if (VehicleModulesPlugin.Instance == null) return true;

                var state = VehicleModulesPlugin.Instance.GetVehicleState(__instance);
                
                // Если трансмиссия выбита — блокируем передачу пакета движения
                if (state != null && state.IsTransmissionBroken)
                {
                    return false; 
                }
            }
            catch (Exception)
            {
                // Тихая ошибка, чтобы не спамить в консоль каждый кадр
            }
            
            return true;
        }
    }
}
