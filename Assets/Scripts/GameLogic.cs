using UnityEngine;

public class GameLogic : MonoBehaviour
{
    public BADNetworkClient _BADNetworkClient;

    void Update()
    {
        if (ClientStartup.GameStatus == "STARTED" && Input.GetKeyDown("w"))
        {
            Debug.Log("w key was pressed");
            _BADNetworkClient.WPressed();
        }
    }
}