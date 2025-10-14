using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver networkDriver;
    private NativeList<NetworkConnection> networkConnections;

    NetworkPipeline reliableAndInOrderPipeline;
    NetworkPipeline nonReliableNotInOrderedPipeline;

    const ushort NetworkPort = 9002;

    const int MaxNumberOfClientConnections = 1000;

    [System.Serializable]
    public class LoginRequest
    {
        public string action;
        public string username;
        public string password;
    }

    [System.Serializable]
    public class ServerResponse
    {
        public string status;
        public string message;
    }

    void Start()
    {
        networkDriver = NetworkDriver.Create();
        reliableAndInOrderPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        nonReliableNotInOrderedPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4;
        endpoint.Port = NetworkPort;

        int error = networkDriver.Bind(endpoint);
        if (error != 0)
            Debug.Log("Failed to bind to port " + NetworkPort);
        else
            networkDriver.Listen();

        networkConnections = new NativeList<NetworkConnection>(MaxNumberOfClientConnections, Allocator.Persistent);
    }

    void OnDestroy()
    {
        networkDriver.Dispose();
        networkConnections.Dispose();
    }

    void Update()
    {
        #region Check Input and Send Msg

        if (Input.GetKeyDown(KeyCode.A))
        {
            for (int i = 0; i < networkConnections.Length; i++)
            {
                SendMessageToClient("Hello client's world, sincerely your network server", networkConnections[i]);
            }
        }

        #endregion

        networkDriver.ScheduleUpdate().Complete();

        #region Remove Unused Connections

        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
            {
                networkConnections.RemoveAtSwapBack(i);
                i--;
            }
        }

        #endregion

        #region Accept New Connections

        while (AcceptIncomingConnection())
        {
            Debug.Log("Accepted a client connection");
        }

        #endregion

        #region Manage Network Events

        DataStreamReader streamReader;
        NetworkPipeline pipelineUsedToSendEvent;
        NetworkEvent.Type networkEventType;

        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
                continue;

            while (PopNetworkEventAndCheckForData(networkConnections[i], out networkEventType, out streamReader, out pipelineUsedToSendEvent))
            {
                if (pipelineUsedToSendEvent == reliableAndInOrderPipeline)
                    Debug.Log("Network event from: reliableAndInOrderPipeline");
                else if (pipelineUsedToSendEvent == nonReliableNotInOrderedPipeline)
                    Debug.Log("Network event from: nonReliableNotInOrderedPipeline");

                switch (networkEventType)
                {
                    case NetworkEvent.Type.Data:
                        int sizeOfDataBuffer = streamReader.ReadInt();
                        NativeArray<byte> buffer = new NativeArray<byte>(sizeOfDataBuffer, Allocator.Persistent);
                        streamReader.ReadBytes(buffer);
                        byte[] byteBuffer = buffer.ToArray();
                        string msg = Encoding.Unicode.GetString(byteBuffer);
                        ProcessReceivedMsg(msg, networkConnections[i]);
                        buffer.Dispose();
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("Client has disconnected from server");
                        networkConnections[i] = default(NetworkConnection);
                        break;
                }
            }
        }

        #endregion
    }

    private bool AcceptIncomingConnection()
    {
        NetworkConnection connection = networkDriver.Accept();
        if (connection == default(NetworkConnection))
            return false;

        networkConnections.Add(connection);
        return true;
    }

    private bool PopNetworkEventAndCheckForData(NetworkConnection networkConnection, out NetworkEvent.Type networkEventType, out DataStreamReader streamReader, out NetworkPipeline pipelineUsedToSendEvent)
    {
        networkEventType = networkConnection.PopEvent(networkDriver, out streamReader, out pipelineUsedToSendEvent);

        if (networkEventType == NetworkEvent.Type.Empty)
            return false;
        return true;
    }

    private void ProcessReceivedMsg(string msg, NetworkConnection sender)
    {
        Debug.Log($" Received message: {msg}");

        try
        {
            LoginRequest request = JsonUtility.FromJson<LoginRequest>(msg);

            if (request == null || string.IsNullOrEmpty(request.action))
            {
                Debug.LogWarning(" Invalid message format");
                return;
            }

            ServerResponse response = new ServerResponse();

            if (request.action == "create")
            {
                Debug.Log($" Account created for username: {request.username}");
                response.status = "success";
                response.message = "Account created successfully!";
            }
            else if (request.action == "login")
            {
                Debug.Log($" Login successful for username: {request.username}");
                response.status = "success";
                response.message = "Login successful!";
            }
            else
            {
                Debug.LogWarning($" Unknown action: {request.action}");
                response.status = "error";
                response.message = "Unknown action.";
            }

            string jsonResponse = JsonUtility.ToJson(response);
            SendMessageToClient(jsonResponse, sender);
        }
        catch (System.Exception e)
        {
            Debug.LogError($" Error processing message: {e.Message}");
        }
    }

    public void SendMessageToClient(string msg, NetworkConnection networkConnection)
    {
        byte[] msgAsByteArray = Encoding.Unicode.GetBytes(msg);
        NativeArray<byte> buffer = new NativeArray<byte>(msgAsByteArray, Allocator.Persistent);


        //Driver.BeginSend(m_Connection, out var writer);
        DataStreamWriter streamWriter;
        //networkConnection.
        networkDriver.BeginSend(reliableAndInOrderPipeline, networkConnection, out streamWriter);
        streamWriter.WriteInt(buffer.Length);
        streamWriter.WriteBytes(buffer);
        networkDriver.EndSend(streamWriter);

        buffer.Dispose();
    }

}
