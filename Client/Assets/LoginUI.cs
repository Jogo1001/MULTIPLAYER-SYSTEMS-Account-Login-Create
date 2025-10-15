using UnityEngine;
using TMPro;

public class LoginUI : MonoBehaviour
{
    public NetworkClient networkClient;

    [Header("Login UI")]
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public TMP_Text feedbackText;
    public GameObject loginPanel;
    public GameObject loggedInPanel;

    [Header("Room UI")]
    public GameObject gameRoomPanel;
    public TMP_InputField roomNameField;
    public TMP_Text roomStatusText;
    public GameObject waitingPanel;
    public GameObject playingPanel;

    void Start() => GameStateManager.Instance.OnStateChanged += HandleStateChange;

    public void OnLoginClicked() => networkClient.SendLogin(usernameField.text, passwordField.text);
    public void OnCreateAccountClicked() => networkClient.SendCreate(usernameField.text, passwordField.text);

    public void SetFeedback(string message) => feedbackText.text = message;

    public void SwitchToLoggedInUI()
    {
        loginPanel.SetActive(false);
        loggedInPanel.SetActive(true);
        gameRoomPanel.SetActive(true);
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Lobby);
    }

    private void HandleStateChange(GameStateManager.GameState state)
    {
        loginPanel.SetActive(state == GameStateManager.GameState.Login);
        loggedInPanel.SetActive(state == GameStateManager.GameState.Lobby);
        waitingPanel.SetActive(state == GameStateManager.GameState.WaitingForOpponent);
        playingPanel.SetActive(state == GameStateManager.GameState.Playing);
    }

    public void OnCreateRoomClicked()
    {
        if (string.IsNullOrEmpty(roomNameField.text)) return;
        networkClient.SendJoinRoom(roomNameField.text);
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.WaitingForOpponent);
        waitingPanel.SetActive(true);
        gameRoomPanel.SetActive(false);
    }

    public void OnBackFromWaitingClicked()
    {
        networkClient.SendLeaveRoom();
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Lobby);
        waitingPanel.SetActive(false);
        gameRoomPanel.SetActive(true);
    }

    public void OnLeaveMatchClicked()
    {
        networkClient.SendLeaveRoom();
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Lobby);
        loginPanel.SetActive(true);
    }

    public void OnSendPlayMessageClicked()
    {
        networkClient.SendPlay("Player pressed the play button!");
    }
}
