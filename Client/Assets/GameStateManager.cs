using UnityEngine;
using System;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance;

    public enum GameState
    {
        Login,
        Lobby,
        WaitingForOpponent,
        Playing
    }

    public GameState CurrentState { get; private set; }

    public event Action<GameState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ChangeState(GameState.Login);
    }

    public void ChangeState(GameState newState)
    {
        if (newState == CurrentState) return;

        CurrentState = newState;
        Debug.Log($"[GameStateManager] Changed to {newState}");
        OnStateChanged?.Invoke(newState);
    }
}
