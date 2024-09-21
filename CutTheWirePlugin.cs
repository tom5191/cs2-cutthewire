using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Menu;
using System.Numerics;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CutTheWirePlugin.Modules;

namespace CutTheWirePlugin;

[MinimumApiVersion(220)]
public class CutTheWirePlugin : BasePlugin
{
    private const string Version = "1.0.0";
    
    public override string ModuleName => "Cut The Wire Plugin";
    public override string ModuleVersion => Version;
    public override string ModuleAuthor => "B3none";
    public override string ModuleDescription => "https://github.com/B3none/cs2-instadefuse";

    public static readonly string LogPrefix = $"[Cut The Wire {Version}] ";
    public static string MessagePrefix = $"[{ChatColors.Green}Retakes{ChatColors.White}] ";
    private static string[] WireOptions = ["Red", "Blue", "Green", "Yellow", "Random"];


    private float _bombPlantedTime = float.NaN;
    private bool _bombTicking;
    private int _molotovThreat;
    private int _heThreat;
    private int _bombWireColor;

    private List<int> _infernoThreat = new();

    private Translator _translator;
    
    public CutTheWirePlugin()
    {
        _translator = new Translator(Localizer);
    }
    
    public override void Load(bool hotReload)
    {
        _translator = new Translator(Localizer);
        
        Console.WriteLine($"{LogPrefix}Plugin loaded!");
        
        MessagePrefix = _translator["cutthewire.prefix"];
    }

    [GameEventHandler]
    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnGrenadeThrown: {@event.Weapon} - isBot: {@event.Userid?.IsBot}");

        var weapon = @event.Weapon;

        if (weapon == "hegrenade")
        {
            _heThreat++;
        }
        else if (weapon == "incgrenade" || weapon == "molotov")
        {
            _molotovThreat++;
        }
        else
        {
            return HookResult.Continue;
        }

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnInfernoStartBurn(EventInfernoStartburn @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnInfernoStartBurn");
        
        var infernoPosVector = new Vector3(@event.X, @event.Y, @event.Z);

        var plantedBomb = FindPlantedBomb();
        if (plantedBomb == null)
        {
            return HookResult.Continue;
        }

        var plantedBombVector = plantedBomb.CBodyComponent?.SceneNode?.AbsOrigin ?? null;
        if (plantedBombVector == null)
        {
            return HookResult.Continue;
        }

        var plantedBombVector3 = new Vector3(plantedBombVector.X, plantedBombVector.Y, plantedBombVector.Z);

        var distance = Vector3.Distance(infernoPosVector, plantedBombVector3);

        Console.WriteLine($"Inferno Distance to bomb: {distance}");

        if (distance > 250) 
        {
            return HookResult.Continue;
        }

        _infernoThreat.Add(@event.Entityid);

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnInfernoExtinguish(EventInfernoExtinguish @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnInfernoExtinguish");
        
        _infernoThreat.Remove(@event.Entityid);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnInfernoExpire(EventInfernoExpire @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnInfernoExpire");
        
        _infernoThreat.Remove(@event.Entityid);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnHeGrenadeDetonate");
        
        if (_heThreat > 0)
        {
            _heThreat--;
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnMolotovDetonate(EventMolotovDetonate @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnMolotovDetonate");
        
        if (_molotovThreat > 0)
        {
            _molotovThreat--;
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnRoundStart");
        
        _bombPlantedTime = float.NaN;
        _bombTicking = false;

        _heThreat = 0;
        _molotovThreat = 0;
        _infernoThreat = new List<int>();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnBombPlanted");
        
        _bombPlantedTime = Server.CurrentTime;
        _bombTicking = true;

        setBombWireColor();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombBeginDefuse(EventBombBegindefuse @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnBombBeginDefuse");

        var player = @event.Userid;

        if (player != null && player.IsValid && player.PawnIsAlive)
        {
            OpenWireCutDefuseMenu(player);
        }

        return HookResult.Continue;
    }

    private static string setBombWireColor() {
        Console.WriteLine($"{LogPrefix}Setting bomb wire color ...");

        Random rnd = new Random();
        var index = rnd.Next(1, 4);

        _bombWireColor = WireOptions[index]; 

        Console.WriteLine($"{LogPrefix}bomb wire color set");
    }

    private static void GetDefuseOptions(ChatMenu defuseMenu) {
        for(var i = 0; i < WireOptions.length; i++ ) {
            defuseMenu.AddMenuOption(WireOptions[i], AttemptToDefuse);
        }
    }

    private void OpenWireCutDefuseMenu(CSSPlayerController defuser) {
        Console.WriteLine($"{LogPrefix}Attempting to open menu...");

        var defuseMenu = new ChatMenu("Cut The Wire");
        ChatMenus.OpenMenu(defuser, defuseMenu);
    }

    private void AttemptToDefuse(CCSPlayerController defuser, ChatMenuOption option)
    {
        Console.WriteLine($"{LogPrefix}Attempting instadefuse...");
        Console.WriteLine($"{LogPrefix} PULL THE LEVER KRONK...");


        if (!_bombTicking)
        {
            Console.WriteLine($"{LogPrefix}Bomb is not planted!");
            return;
        }

        var plantedBomb = FindPlantedBomb();
        if (plantedBomb == null)
        {
            Console.WriteLine($"{LogPrefix}Planted bomb is null!");
            return;
        }

        if (plantedBomb.CannotBeDefused)
        {
            Console.WriteLine($"{LogPrefix}Planted bomb can not be defused!");
            return;
        }
        
        PrintThreatLevel();

        if (_heThreat > 0 || _molotovThreat > 0 || _infernoThreat.Any())
        {
            Console.WriteLine($"{LogPrefix}Instant Defuse not possible because a grenade threat is active!");
            Server.PrintToChatAll(MessagePrefix + _translator["cutthewire.not_possible"]);
            return;
        }

        var bombTimeUntilDetonation = plantedBomb.TimerLength - (Server.CurrentTime - _bombPlantedTime);

        var defuseLength = plantedBomb.DefuseLength;
        if (defuseLength != 5 && defuseLength != 10)
        {
            defuseLength = defuser.PawnHasDefuser ? 5.0f : 10.0f;
        }
        Console.WriteLine($"{LogPrefix}DefuseLength: {defuseLength}");
        
        if(option == _bombWireColor) {
            Console.WriteLine($"{LogPrefix} Selected wire options matches set wire color");
        } else {
            Console.WriteLine($"{LogPrefix} WRONG LEVER");
        }

        // Server.NextFrame(() =>
        // {
        //     // We get the planted bomb again as it was sometimes crashing.
        //     plantedBomb = FindPlantedBomb();
            
        //     if (plantedBomb == null)
        //     {
        //         Console.WriteLine($"{LogPrefix}Planted bomb is null!");
        //         return;
        //     }
            
        //     plantedBomb.DefuseCountDown = 0;

        //     Server.PrintToChatAll(MessagePrefix + _translator["cutthewire.successful", defuser.PlayerName, $"{Math.Abs(bombTimeUntilDetonation):n3}"]);
        // });
    }

    private static bool TeamHasAlivePlayers(CsTeam team)
    {
        var players = Utilities.GetPlayers();

        foreach (var player in players)
        {
            if (!player.IsValid) continue;
            if (player.Team != team) continue;
            if (!player.PawnIsAlive) continue;
            
            return true;
        }

        return false;
    }

    private static CPlantedC4? FindPlantedBomb()
    {
        var plantedBombList = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4").ToList();

        if (plantedBombList.Any())
        {
            return plantedBombList.FirstOrDefault();
        }
        
        Console.WriteLine($"{LogPrefix}No planted bomb entities have been found!");
        return null;
    }

    private void PrintThreatLevel()
    {
        Console.WriteLine($"{LogPrefix}Threat-Levels: HE [{_heThreat}], Molotov [{_molotovThreat}], Inferno [{_infernoThreat.Count}]");
    }
}
