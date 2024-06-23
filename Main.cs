// See https://aka.ms/new-console-template for more information
using AnyBaseLib;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;
using VipCoreApi;

public class VipDbChecker : BasePlugin
{
    public override string ModuleAuthor => "Nick Fox";
    public override string ModuleName => "VipDbChecker";
    public override string ModuleDescription => "Picks up groups from db and auto activate it";
    public override string ModuleVersion => "1.0";

    private IAnyBase db;
    private string server_id;

    private IVipCoreApi? _vip;
    private PluginCapability<IVipCoreApi> PluginVip { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _vip = PluginVip.Get();

        if (_vip == null)
            return;

        db = CAnyBase.Base("mysql");

        dynamic js = JsonConvert.DeserializeObject(File.ReadAllText($"{ModulePath}/../../../configs/plugins/VIPCore/vip_core.json"));

        //dynamic js = JsonConvert.DeserializeObject(File.ReadAllText($"{ModulePath}/../VIPCore/lang/vip_core.json"));

        var db_info = js.Connection;

        string db_host = db_info.Host.ToString();
        string db_name = db_info.Database.ToString();
        string db_user = db_info.User.ToString();
        string db_pass = db_info.Password.ToString();
                
        server_id = js.ServerId.ToString();

        db.Set(AnyBaseLib.Bases.CommitMode.NoCommit, db_name, db_host, db_user, db_pass);

        db.Init();
        
        AddTimer(10.0f, CheckAllPlayers, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
    }


    void CheckAllPlayers()
    {
        foreach(var player in Utilities.GetPlayers())
        {
            if(IsValidPlayer(player))
                CheckPlayer(player);
        }
    }

    void CheckPlayer(CCSPlayerController player)
    {        
        var account_id = player.AuthorizedSteamID.AccountId.ToString();
        db.QueryAsync("SELECT `group`, (`expires`- UNIX_TIMESTAMP()) as `time`, `expires` FROM `vip_users` WHERE `sid` = {ARG} AND `account_id` = {ARG} HAVING (`time` > 0 OR `expires` = 0)", [server_id, account_id], (data) => QueryCallback(data, player));
    }

    string GetExpireTime(int expires)
    {
        if (expires == 0)
            return Localizer["never"];
        else
            return DateTimeOffset.FromUnixTimeSeconds(expires).ToString("G");

    }

    void QueryCallback(List<List<string>> data, CCSPlayerController player)
    {
        var isVip = _vip.IsClientVip(player);
        Server.NextFrame(() =>
        {            
            if (data.Count > 0)
            {
                if (!isVip)
                {
                    var line = data[0];

                    var time = 0;
                    if (!line[2].Equals("0"))
                        time = int.Parse(line[1]);
                    //player.PrintToChat("Теперь вам доступны VIP возможности");
                    _vip.PrintToChat(player, String.Format(Localizer["given"], line[0]));
                    _vip.PrintToChat(player, String.Format(Localizer["expires"], GetExpireTime(int.Parse(line[2]))));
                    _vip.GiveClientTemporaryVip(player, line[0], time);

                }
            }
            else
                if (isVip)
                {
                _vip.PrintToChat(player, Localizer["taken"]);
                //player.PrintToChat("Вы лишились дополнительных VIP возможностей :(");
                    _vip.RemoveClientVip(player);
                }
        });
    }

    public static bool IsValidPlayer(CCSPlayerController player)
    {
        if (player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsBot) return true;
        else return false;
    }


}