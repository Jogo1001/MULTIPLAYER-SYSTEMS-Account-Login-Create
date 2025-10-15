using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUI : MonoBehaviour
{

    public NetworkClient networkClient;


    [Header("Game Login UI")]
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public TMP_Text feedbackText;
    public GameObject loginPanel;
    public GameObject loggedInPanel;

    [Header("Game Room UI")]
    public GameObject gameRoomPanel;
    public TMP_InputField roomNameField;
    public TMP_Text roomStatusText;
    public GameObject waitingPanel;
    public GameObject playingPanel;
 
    // this function called when login button is click


    public void OnLoginClicked()
    {
        networkClient.SendLoginRequest(usernameField.text, passwordField.text);
        gameRoomPanel.SetActive(true);
    }

    // this function called when create account button is click
    public void OnCreateAccountClicked()
    {
        networkClient.SendCreateAccountRequest(usernameField.text, passwordField.text);
    }

    // updates feedback text
    public void SetFeedback(string message)
    {
        feedbackText.text = message;
    }

    //switch ui from loginscreen to logged screen
    //display welcome message
    public void SwitchToLoggedInUI()
    {
        loginPanel.SetActive(false);
        loggedInPanel.SetActive(true);
        SetFeedback("Welcome!");
    }
}
