// See https://aka.ms/new-console-template for more information
using AnyBaseLib;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Cvars;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;
using System.Numerics;
using VipCoreApi;

public class VipDbChecker : BasePlugin
{
    public override string ModuleAuthor => "Nick Fox";
    public override string ModuleName => "VipDbChecker";
    public override string ModuleDescription => "Picks up groups from db and auto activate it without reconnecting";
    public override string ModuleVersion => "1.4";

    private IAnyBase db;
    private string server_id;
    private bool timer_enabled = true;
    private bool timer_stopped = false;

    private IVipCoreApi? _vip;
    private PluginCapability<IVipCoreApi> PluginVip { get; } = new("vipcore:core");
    public readonly FakeConVar<bool> IsCoreEnableConVar = new("css_vip_checker_enable", "", true);

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

        //AddTimer(10.0f, CheckAllPlayers, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
        StartTimer();
    }

    void StartTimer()
    {
        Task.Run(() =>
        {
            int count = 0;
            while (timer_enabled)
            {
                Thread.Sleep(500);
                count++;

                if (count == 20)
                {
                    count = 0;
                    Server.NextFrameAsync(CheckAllPlayers);
                }
            }
            timer_stopped = true;
        });
    }

    public override void Unload(bool hotReload)
    {
        timer_enabled = false;
        while(!timer_stopped)
            Thread.Sleep(250);
    }

    void CheckAllPlayers()
    {
        if (!IsCoreEnableConVar.Value)
            return;
        var players = Utilities.GetPlayers();
        List<string> steamids = [];
        
        for(int i = 0; i < players.Count; i++)
        {
            if (!IsValidPlayer(players[i]))
            {
                players.RemoveAt(i);
                i--;                
            }
            else
                steamids.Add(players[i].AuthorizedSteamID.AccountId.ToString());
        }

        if (players.Count > 0)
            //db.QueryAsync("SELECT `account_id`, `group`, (`expires`- UNIX_TIMESTAMP()) as `time`, `expires` FROM `vip_users` WHERE `sid` = {ARG} HAVING (`time` > 0 OR `expires` = 0)", [server_id]);
            db.QueryAsync("SELECT `account_id`, `group`, CAST(`expires` as SIGNED) - UNIX_TIMESTAMP() as `time`, `expires` FROM `vip_users` WHERE `sid` = {ARG} HAVING (`time` > 0 OR `expires` = 0)", [server_id], (data) => QueryCallback(data, players, steamids));
    }

    string GetExpireTime(int expires)
    {
        if (expires == 0)
            return Localizer["never"];
        else
            return DateTimeOffset.FromUnixTimeSeconds(expires).ToString("G");

    }
        
    void GivePlayerVip(CCSPlayerController player, string group, int time, int expires)
    {
        Server.NextFrame(() =>
        {
            _vip.PrintToChat(player, String.Format(Localizer["given"], group));
            _vip.PrintToChat(player, String.Format(Localizer["expires"], GetExpireTime(expires)));
            _vip.GiveClientTemporaryVip(player, group, time);
        });
    }

    void TakePlayerVip(CCSPlayerController player)
    {
        Server.NextFrame(() =>
        {
            _vip.PrintToChat(player, Localizer["taken"]);
            if (IsValidPlayer(player)) _vip.RemoveClientVip(player);
        });
    }

    void QueryCallback(List<List<string>> data, List<CCSPlayerController> players, List<string> steamids)
    {
        Task.Run(() =>
        {
            bool given;
            bool isVip;
            int index = 0;
            foreach (var player in players)
            {
                
                isVip = _vip.IsClientVip(player);
                
                given = false;
                foreach (var line in data)
                    //if (line[0] == player.AuthorizedSteamID.AccountId.ToString())
                    if (line[0] == steamids[index])
                    {
                        given = true;

                        if (!isVip)
                        {
                            var time = 0;
                            if (!line[2].Equals("0"))
                                time = int.Parse(line[2]);
                            GivePlayerVip(player, line[1], time, int.Parse(line[3]));
                        }

                        break;
                    }
                if (!given && isVip)
                {
                    TakePlayerVip(player);
                }

                index++;

            }
        });
    }

    /*
    void QueryCallback2(List<List<string>> data, CCSPlayerController player)
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
    */

    public static bool IsValidPlayer(CCSPlayerController player)
    {
        return player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsBot;
    }


}