using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;

public class NetworkClient : MonoBehaviour
{
    private NetworkDriver driver;
    private NetworkConnection connection;
    private NetworkPipeline reliablePipeline;
    private const ushort Port = 9002;
    [SerializeField] private string IP = "192.168.2.19";

    [System.Serializable] public class ServerResponse { public string status; public string message; }
    [System.Serializable] public class LoginRequest { public string action; public string username; public string password; }
    [System.Serializable] public class RoomRequest { public string action; public string roomName; }
    [System.Serializable] public class PlayMessage { public string action; public string content; }

    void Start()
    {
        driver = NetworkDriver.Create();
        reliablePipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        connection = driver.Connect(NetworkEndpoint.Parse(IP, Port, NetworkFamily.Ipv4));
        Debug.Log($"[Client] Connecting to {IP}:{Port}");
    }

    void OnDestroy()
    {
        if (connection.IsCreated)
            connection.Disconnect(driver);
        driver.Dispose();
    }

    void Update()
    {
        driver.ScheduleUpdate().Complete();
        if (!connection.IsCreated) return;

        NetworkEvent.Type evt;
        DataStreamReader reader;
        NetworkPipeline pipe;

        while ((evt = connection.PopEvent(driver, out reader, out pipe)) != NetworkEvent.Type.Empty)
        {
            switch (evt)
            {
                case NetworkEvent.Type.Connect:
                    Debug.Log("[Client] Connected to server.");
                    break;

                case NetworkEvent.Type.Data:
                    int size = reader.ReadInt();
                    var buffer = new NativeArray<byte>(size, Allocator.Temp);
                    reader.ReadBytes(buffer);
                    string msg = Encoding.Unicode.GetString(buffer.ToArray());
                    buffer.Dispose();
                    ProcessServerMessage(msg);
                    break;

                case NetworkEvent.Type.Disconnect:
                    Debug.Log("[Client] Disconnected from server.");
                    connection = default;
                    break;
            }
        }
    }

    private void ProcessServerMessage(string msg)
    {
        var response = JsonUtility.FromJson<ServerResponse>(msg);
        var ui = FindObjectOfType<LoginUI>();
        if (ui == null) return;

        Debug.Log($"[Client] Server: {response.status} - {response.message}");

        switch (response.status)
        {
            case "success":
                if (response.message.Contains("Login"))
                {
                    ui.SwitchToLoggedInUI();
                    ui.SetFeedback(response.message);
                }
                else if (response.message.Contains("Waiting"))
                {
                    ui.roomStatusText.text = "Waiting for an opponent...";
                    GameStateManager.Instance.ChangeState(GameStateManager.GameState.WaitingForOpponent);
                }
                else if (response.message.Contains("Joined"))
                {
                    ui.roomStatusText.text = "Opponent found! Start playing.";
                    GameStateManager.Instance.ChangeState(GameStateManager.GameState.Playing);
                }
                else
                {
                    ui.SetFeedback(response.message);
                }
                break;

            case "info":
                ui.SetFeedback(response.message);
                break;

            case "error":
                ui.SetFeedback("Error: " + response.message);
                break;
        }
    }

    private void SendJson(object obj)
    {
        string json = JsonUtility.ToJson(obj);
        byte[] bytes = Encoding.Unicode.GetBytes(json);
        var buffer = new NativeArray<byte>(bytes, Allocator.Temp);

        DataStreamWriter writer;
        driver.BeginSend(reliablePipeline, connection, out writer);
        writer.WriteInt(buffer.Length);
        writer.WriteBytes(buffer);
        driver.EndSend(writer);

        buffer.Dispose();
    }

    public void SendLogin(string user, string pass)
        => SendJson(new LoginRequest { action = "login", username = user, password = pass });

    public void SendCreate(string user, string pass)
        => SendJson(new LoginRequest { action = "create", username = user, password = pass });

    public void SendJoinRoom(string room)
        => SendJson(new RoomRequest { action = "joinOrCreateRoom", roomName = room });

    public void SendLeaveRoom()
        => SendJson(new RoomRequest { action = "leaveRoom" });

    public void SendPlay(string content)
        => SendJson(new PlayMessage { action = "playAction", content = content });
}
