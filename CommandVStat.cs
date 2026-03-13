public void Execute(IRocketPlayer caller, string[] command)
{
    UnturnedPlayer player = (UnturnedPlayer)caller;
    var tracker = player.CurrentVehicle?.gameObject.GetComponent<VehicleTracker>();

    if (tracker == null) return;

    UnturnedChat.Say(caller, "--- ТЕХНИЧЕСКИЙ ФОРМУЛЯР ---", Color.yellow);
    foreach (TankModule mod in Enum.GetValues(typeof(TankModule)))
    {
        string statusText = tracker.GetModuleStatus(mod);
        int percent = (int)tracker.ModuleHP[mod];
        
        Color c = percent > 70 ? Color.green : (percent > 30 ? Color.yellow : Color.red);
        UnturnedChat.Say(caller, $"{mod}: {statusText} ({percent}%)", c);
    }
}
