using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUI : MonoBehaviour
{
    public NetworkClient networkClient;
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public TMP_Text feedbackText;

    public void OnLoginClicked()
    {
        networkClient.SendLoginRequest(usernameField.text, passwordField.text);
    }

    public void OnCreateAccountClicked()
    {
        networkClient.SendCreateAccountRequest(usernameField.text, passwordField.text);
    }

   
    public void SetFeedback(string message)
    {
        feedbackText.text = message;
    }
}
