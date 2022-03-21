using UnityEngine;

public class ClientStartup : MonoBehaviour
{
    public static string GameStatus = "LOADING";

    void Start()
    {
        GameLiftClient gameLiftClient = new GameLiftClient();
    }
}