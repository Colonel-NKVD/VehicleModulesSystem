using HarmonyLib;
using SDG.Unturned;
using Steamworks;

namespace VehicleModulesSystem
{
    /// <summary>
    /// Патч для блокировки движения техники при сломанной трансмиссии.
    /// Перехватывает ввод игрока (W, A, S, D).
    /// </summary>
    [HarmonyPatch(typeof(InteractableVehicle), "tellDrive")]
    public class TransmissionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(InteractableVehicle __instance, CSteamID steamID, byte x, byte y, ushort steering, ushort speed)
        {
            // Получаем состояние текущей машины через главный класс плагина
            var state = VehicleModulesPlugin.Instance.GetVehicleState(__instance);
            
            if (state != null && state.IsTransmissionBroken)
            {
                // Если трансмиссия сломана, возвращаем false.
                // Это заставляет сервер игнорировать пакет управления от игрока.
                // Машина будет стоять на месте, даже если игрок жмет "W".
                return false;
            }
            
            // Если всё в порядке, разрешаем стандартное выполнение метода
            return true;
        }
    }
}
