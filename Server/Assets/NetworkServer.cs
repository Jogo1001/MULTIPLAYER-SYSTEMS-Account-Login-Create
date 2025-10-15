using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;
using System.Collections.Generic;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver networkDriver;
    private NativeList<NetworkConnection> connections;

    private NetworkPipeline reliablePipeline;
    private NetworkPipeline unreliablePipeline;

    private const ushort Port = 9002;
    private const int MaxConnections = 1000;

    private Dictionary<string, List<NetworkConnection>> gameRooms = new();

    
    [System.Serializable] public class BaseMessage { public string action; }
    [System.Serializable] public class LoginRequest : BaseMessage { public string username; public string password; }
    [System.Serializable] public class RoomRequest : BaseMessage { public string roomName; }
    [System.Serializable] public class PlayMessage : BaseMessage { public string content; }
    [System.Serializable] public class ServerResponse { public string status; public string message; }
   

    void Start()
    {
        networkDriver = NetworkDriver.Create();
        reliablePipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        unreliablePipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));

        var endpoint = NetworkEndpoint.AnyIpv4;
        endpoint.Port = Port;

        if (networkDriver.Bind(endpoint) != 0)
            Debug.LogError($"Failed to bind to port {Port}");
        else
            networkDriver.Listen();

        connections = new NativeList<NetworkConnection>(MaxConnections, Allocator.Persistent);
        Debug.Log($"[Server] Listening on port {Port}");
    }

    void OnDestroy()
    {
        networkDriver.Dispose();
        connections.Dispose();
    }

    void Update()
    {
        networkDriver.ScheduleUpdate().Complete();

        CleanConnections();
        AcceptConnections();
        ProcessNetworkEvents();
    }

    private void CleanConnections()
    {
        for (int i = 0; i < connections.Length; i++)
        {
            if (!connections[i].IsCreated)
            {
                connections.RemoveAtSwapBack(i);
                i--;
            }
        }
    }

    private void AcceptConnections()
    {
        NetworkConnection conn;
        while ((conn = networkDriver.Accept()) != default)
        {
            connections.Add(conn);
            Debug.Log($"[Server] Accepted new connection. Total: {connections.Length}");
        }
    }

    private void ProcessNetworkEvents()
    {
        for (int i = 0; i < connections.Length; i++)
        {
            var conn = connections[i];
            if (!conn.IsCreated) continue;

            NetworkEvent.Type eventType;
            DataStreamReader reader;
            NetworkPipeline pipeline;

            while ((eventType = conn.PopEvent(networkDriver, out reader, out pipeline)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    case NetworkEvent.Type.Data:
                        HandleDataEvent(conn, reader);
                        break;

                    case NetworkEvent.Type.Disconnect:
                        HandleDisconnect(conn);
                        connections[i] = default;
                        break;
                }
            }
        }
    }

    private void HandleDataEvent(NetworkConnection sender, DataStreamReader reader)
    {
        int size = reader.ReadInt();
        var buffer = new NativeArray<byte>(size, Allocator.Temp);
        reader.ReadBytes(buffer);
        string msg = Encoding.Unicode.GetString(buffer.ToArray());
        buffer.Dispose();

        Debug.Log($"[Server] Received: {msg}");
        ProcessMessage(msg, sender);
    }

    private void HandleDisconnect(NetworkConnection conn)
    {
        Debug.Log($"[Server] Client disconnected.");
        RemoveFromAllRooms(conn);
    }

    private void ProcessMessage(string json, NetworkConnection sender)
    {
        try
        {
            var baseMsg = JsonUtility.FromJson<BaseMessage>(json);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.action))
            {
                Debug.LogWarning("Invalid message received.");
                return;
            }

            switch (baseMsg.action)
            {
                case "login":
                case "create":
                    HandleLogin(JsonUtility.FromJson<LoginRequest>(json), sender);
                    break;

                case "joinOrCreateRoom":
                    HandleJoinOrCreateRoom(JsonUtility.FromJson<RoomRequest>(json), sender);
                    break;

                case "leaveRoom":
                    HandleLeaveRoom(sender);
                    break;

                case "playAction":
                    HandlePlay(JsonUtility.FromJson<PlayMessage>(json), sender);
                    break;

                default:
                    Debug.LogWarning($"Unknown action: {baseMsg.action}");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
        }
    }

    private void HandleLogin(LoginRequest req, NetworkConnection sender)
    {
        var response = new ServerResponse
        {
            status = "success",
            message = req.action == "create"
                ? "Account created successfully!"
                : "Login successful!"
        };
        SendJson(sender, response);
    }

    private void HandleJoinOrCreateRoom(RoomRequest req, NetworkConnection sender)
    {
        if (!gameRooms.ContainsKey(req.roomName))
            gameRooms[req.roomName] = new List<NetworkConnection>();

        var room = gameRooms[req.roomName];
        if (room.Contains(sender)) return;

        room.Add(sender);
        var response = new ServerResponse { status = "success" };

        if (room.Count == 1)
        {
            response.message = "Waiting for opponent...";
            SendJson(sender, response);
        }
        else if (room.Count == 2)
        {
            response.message = "Joined room - start playing!";
            foreach (var conn in room)
                SendJson(conn, response);
        }
        else
        {
            response.status = "error";
            response.message = "Room full.";
            SendJson(sender, response);
        }
    }

    private void HandleLeaveRoom(NetworkConnection sender)
    {
        RemoveFromAllRooms(sender);
        SendJson(sender, new ServerResponse
        {
            status = "info",
            message = "You have left the room."
        });
    }

    private void HandlePlay(PlayMessage msg, NetworkConnection sender)
    {
        foreach (var room in gameRooms)
        {
            if (room.Value.Contains(sender))
            {
                foreach (var conn in room.Value)
                {
                    if (conn != sender)
                        SendJson(conn, new ServerResponse
                        {
                            status = "success",
                            message = $"Opponent says: {msg.content}"
                        });
                }
                break;
            }
        }
    }

    private void RemoveFromAllRooms(NetworkConnection sender)
    {
        foreach (var key in new List<string>(gameRooms.Keys))
        {
            var room = gameRooms[key];
            if (room.Remove(sender) && room.Count == 0)
                gameRooms.Remove(key);
        }
    }

    private void SendJson<T>(NetworkConnection conn, T obj)
    {
        string json = JsonUtility.ToJson(obj);
        byte[] data = Encoding.Unicode.GetBytes(json);
        var buffer = new NativeArray<byte>(data, Allocator.Temp);

        DataStreamWriter writer;
        networkDriver.BeginSend(reliablePipeline, conn, out writer);
        writer.WriteInt(buffer.Length);
        writer.WriteBytes(buffer);
        networkDriver.EndSend(writer);

        buffer.Dispose();
    }
}
