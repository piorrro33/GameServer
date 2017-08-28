using System.Numerics;
using System.Threading.Tasks;
using ENet;
using LeagueSandbox.GameServer.Core.Logic;
using LeagueSandbox.GameServer.Logic.GameObjects.Other.Pathfinder;
using LeagueSandbox.GameServer.Logic.Players;
using static LeagueSandbox.GameServer.Logic.Chatbox.ChatCommandManager;

namespace LeagueSandbox.GameServer.Logic.Chatbox.Commands
{
    public class HelpCommand : ChatCommandBase
    {
        private const string COMMAND_PREFIX = "<font color =\"#E175FF\"><b>";
        private const string COMMAND_SUFFIX = "</b></font>, ";
        private readonly Game _game;

        public override string Command => "help";
        public override string Syntax => $"{Command}";

        public HelpCommand(ChatCommandManager chatCommandManager, Game game) : base(chatCommandManager)
        {
            _game = game;
        }

        public override void Execute(Peer peer, bool hasReceivedArguments, string arguments = "")
        {
            if (!_game.Config.ChatCheatsEnabled)
            {
                var msg = "Chat commands are disabled in this game.";
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, msg);
                return;
            }

            var commands = ChatCommandManager.GetCommandsStrings();
            var commandsString = "";
            foreach (var command in commands)
            {
                commandsString = $"{commandsString}{COMMAND_PREFIX}" +
                                 $"{ChatCommandManager.CommandStarterCharacter}{command}" +
                                 $"{COMMAND_SUFFIX}";
            }

            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, "List of available commands: ");
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, commandsString);
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, "There are " + commands.Count + " commands");
            var pm = Program.ResolveDependency<PlayerManager>();
            new Task(() =>
            {
                var wps = Pathfinder.FindPath(pm.GetPeerInfo(peer).Champion.GetPosition(), new Vector2(4527, 10171));
                pm.GetPeerInfo(peer).Champion.SetWaypoints(wps);
            }).Start();
        }
    }
}
