using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUI : MonoBehaviour
{

    public NetworkClient networkClient;



    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public TMP_Text feedbackText;
    public GameObject loginPanel;
    public GameObject loggedInPanel;


    // this function called when login button is click
    public void OnLoginClicked()
    {
        networkClient.SendLoginRequest(usernameField.text, passwordField.text);
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
