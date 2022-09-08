using ENet;
using Sankari.Netcode.Server;

namespace Sankari.Netcode;

public class CPacketLobby : APacketClient
{
    private LobbyOpcode LobbyOpcode { get; set; }

    // LobbyKick
    public byte Id { get; set; }

    // LobbyCreate
    public string LobbyName { get; set; }
    public string LobbyDescription { get; set; }

    // LobbyChatMessage
    public string Message { get; set; }

    // LobbyCountdownChange
    public bool CountdownRunning { get; set; }

    // LobbyJoin
    public string Username { get; set; }
    public bool DirectConnect { get; set; }

    // LobbyReady
    public bool Ready { get; set; }

    public CPacketLobby() { } // required because of ReflectionUtils

    public CPacketLobby(LobbyOpcode opcode)
    {
        LobbyOpcode = opcode;
    }

    public override void Write(PacketWriter writer)
    {
        writer.Write((byte)LobbyOpcode);

        switch (LobbyOpcode)
        {
            case LobbyOpcode.LobbyChatMessage:
                writer.Write(Message);
                break;

            case LobbyOpcode.LobbyCountdownChange:
                writer.Write(CountdownRunning);
                break;

            case LobbyOpcode.LobbyCreate:
                writer.Write(Username);
                writer.Write(LobbyName);
                writer.Write(LobbyDescription);
                break;

            case LobbyOpcode.LobbyJoin:
                writer.Write(Username);
                writer.Write(DirectConnect);
                break;

            case LobbyOpcode.LobbyKick:
                writer.Write(Id);
                break;

            case LobbyOpcode.LobbyReady:
                writer.Write(Ready);
                break;
        }
    }

    public override void Read(PacketReader reader)
    {
        LobbyOpcode = (LobbyOpcode)reader.ReadByte();

        switch (LobbyOpcode)
        {
            case LobbyOpcode.LobbyChatMessage:
                Message = reader.ReadString();
                break;

            case LobbyOpcode.LobbyCountdownChange:
                CountdownRunning = reader.ReadBool();
                break;

            case LobbyOpcode.LobbyCreate:
                Username = reader.ReadString();
                LobbyName = reader.ReadString();
                LobbyDescription = reader.ReadString();
                break;

            case LobbyOpcode.LobbyJoin:
                Username = reader.ReadString();
                DirectConnect = reader.ReadBool();
                break;

            case LobbyOpcode.LobbyKick:
                Id = reader.ReadByte();
                break;

            case LobbyOpcode.LobbyReady:
                Ready = reader.ReadBool();
                break;
        }
    }

    private GameServer server;

    public override void Handle(Peer peer)
    {
        server = GameManager.Net.Server;
        switch (LobbyOpcode)
        {
            case LobbyOpcode.LobbyCreate:
                HandleCreate(peer);
                break;

            case LobbyOpcode.LobbyJoin:
                HandleJoin(peer);
                break;

            case LobbyOpcode.LobbyChatMessage:
                HandleChatMessage(peer);
                break;

            case LobbyOpcode.LobbyKick:
                HandleKick(peer);
                break;

            case LobbyOpcode.LobbyReady:
                HandleReady(peer);
                break;

            case LobbyOpcode.LobbyCountdownChange:
                HandleCountdownChange(peer);
                break;

            case LobbyOpcode.LobbyGameStart:
                HandleGameStart(peer);
                break;
        }
    }

    private void HandleKick(Peer peer)
    {
        if (!server.Players[(byte)peer.ID].Host)
            return;

        server.Kick(Id, DisconnectOpcode.Kicked);
    }

    private void HandleCreate(Peer peer)
    {
        server.Lobby = new DataLobby
        {
            Name = LobbyName,
            Description = LobbyDescription,
            HostId = (byte)peer.ID
        };

        server.Players[(byte)peer.ID] = new DataPlayer
        {
            Username = Username,
            Ready = false,
            Host = true
        };

        server.Send(ServerPacketOpcode.Lobby, new SPacketLobby(LobbyOpcode.LobbyCreate)
        {
            Id = (byte)peer.ID
        }, peer);
    }

    private void HandleChatMessage(Peer peer)
    {
        server.SendToAllPlayers(ServerPacketOpcode.Lobby, new SPacketLobby(LobbyOpcode.LobbyChatMessage)
        {
            Id = (byte)peer.ID,
            Message = Message
        });
    }

    private void HandleCountdownChange(Peer peer)
    {
        if (!server.Players[(byte)peer.ID].Host)
            return;

        server.SendToOtherPlayers(peer.ID, ServerPacketOpcode.Lobby, new SPacketLobby(LobbyOpcode.LobbyCountdownChange)
        {
            CountdownRunning = CountdownRunning
        });
    }

    private void HandleGameStart(Peer peer)
    {
        if (!server.Players[(byte)peer.ID].Host)
            return;

        server.Lobby.AllowJoining = false;
        server.SendToAllPlayers(ServerPacketOpcode.Lobby, new SPacketLobby(LobbyOpcode.LobbyGameStart));
    }

    private void HandleJoin(Peer peer)
    {
        // Check if data.Username is appropriate username
        // TODO

        // Keep track of joining player server side
        if (server.Players.ContainsKey((byte)peer.ID))
        {
            server.Log($"Received LobbyJoin packet from peer with id {peer.ID}. Tried to add id {peer.ID} to Players but exists already");
            return;
        }

        if (!server.Lobby.AllowJoining)
        {
            server.Kick(peer.ID, DisconnectOpcode.Disconnected);
            server.Log($"Peer with id {peer.ID} tried to join lobby but game is running already");
            return;
        }

        server.Players[(byte)peer.ID] = new DataPlayer
        {
            Username = Username,
            Ready = false,
            Host = false
        };

        // tell joining player their Id and tell them about other players in lobby
        server.Send(ServerPacketOpcode.Lobby, new SPacketLobby(LobbyOpcode.LobbyInfo)
        {
            Id = (byte)peer.ID,
            Players = server.GetOtherPlayers((byte)peer.ID),
            DirectConnect = DirectConnect,
            LobbyName = server.Lobby.Name,
            LobbyDescription = server.Lobby.Description,
            LobbyHostId = server.Lobby.HostId,
            LobbyMaxPlayerCount = server.Lobby.MaxPlayerCount
        }, peer);

        // tell other players about new player that joined
        server.SendToOtherPlayers(peer.ID, ServerPacketOpcode.Lobby, new SPacketLobby(LobbyOpcode.LobbyJoin)
        {
            Id = (byte)peer.ID,
            Username = Username
        });
    }

    private void HandleReady(Peer peer)
    {
        var player = server.Players[(byte)peer.ID];
        player.Ready = Ready;

        server.SendToOtherPlayers(peer.ID, ServerPacketOpcode.Lobby, new SPacketLobby(LobbyOpcode.LobbyReady)
        {
            Id = (byte)peer.ID,
            Ready = Ready
        });
    }
}
