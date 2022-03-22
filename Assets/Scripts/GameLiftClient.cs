using System;
using System.Threading.Tasks;
using UnityEngine;
using Amazon;
using Amazon.GameLift;
using Amazon.GameLift.Model;
using Amazon.CognitoIdentity;
using Newtonsoft.Json;

// Largely based on: 
// https://github.com/aws-samples/amazon-gamelift-unity/blob/master/Assets/Scripts/GameLift.cs
public class GameLiftClient
{
    private AmazonGameLiftClient _amazonGameLiftClient;
    private BADNetworkClient _badNetworkClient;
    private static string IsProdArg = "-isProd"; // command line arg that indicates production build if present
    private string _playerUuid;
    private string CognitoIdentityPool = "us-east-1:5264af2b-3f56-4d97-b597-1800c4e2a33b";
    private string FleetId = "fleet-329289e3-2b89-4593-9425-5209de76a033"; // TODO: probably don't hardcode this, use alias or something

    async private void CreatePlayerSession(GameSession gameSession)
    {
        PlayerSession playerSession = null;

        var maxRetryAttempts = 3;
        await RetryHelper.RetryOnExceptionAsync<Exception>
        (maxRetryAttempts, async () =>
        {
            playerSession = await CreatePlayerSessionAsync(gameSession);
        });

        if (playerSession != null)
        {
            // created a player session in there
            Debug.Log("Player session created.");
            Debug.Log($"CLIENT CONNECT INFO: {playerSession.IpAddress}, {playerSession.Port}, {playerSession.PlayerSessionId} ");

            // establish connection with server
            _badNetworkClient.ConnectToServer(playerSession.IpAddress, playerSession.Port, playerSession.PlayerSessionId);
        }

    }

    async private Task<PlayerSession> CreatePlayerSessionAsync(GameSession gameSession)
    {
        var createPlayerSessionRequest = new CreatePlayerSessionRequest();
        createPlayerSessionRequest.GameSessionId = gameSession.GameSessionId;
        createPlayerSessionRequest.PlayerId = _playerUuid;

        Task<CreatePlayerSessionResponse> createPlayerSessionResponseTask = _amazonGameLiftClient.CreatePlayerSessionAsync(createPlayerSessionRequest);
        CreatePlayerSessionResponse createPlayerSessionResponse = await createPlayerSessionResponseTask;

        string playerSessionId = createPlayerSessionResponse.PlayerSession != null ? createPlayerSessionResponse.PlayerSession.PlayerSessionId : "N/A";
        Debug.Log((int)createPlayerSessionResponse.HttpStatusCode + " PLAYER SESSION CREATED: " + playerSessionId);
        return createPlayerSessionResponse.PlayerSession;
    }

    async private Task<GameSession> CreateGameSessionAsync()
    {
        Debug.Log("CreateGameSessionAsync");
        var createGameSessionRequest = new Amazon.GameLift.Model.CreateGameSessionRequest();
        createGameSessionRequest.FleetId = FleetId; // can also use AliasId
        createGameSessionRequest.CreatorId = _playerUuid;
        createGameSessionRequest.MaximumPlayerSessionCount = 2; // search for two player game
        Debug.Log(JsonConvert.SerializeObject(createGameSessionRequest));
        Task<CreateGameSessionResponse> createGameSessionRequestTask = _amazonGameLiftClient.CreateGameSessionAsync(createGameSessionRequest);
        Debug.Log("after task createGameSessionRequestTask");
        CreateGameSessionResponse createGameSessionResponse = await createGameSessionRequestTask;
        Debug.Log("after createGameSessionRequestTask");
        //CreateGameSessionResponse createGameSessionResponse = _amazonGameLiftClient.CreateGameSession(createGameSessionRequest);
        string gameSessionId = createGameSessionResponse.GameSession != null ? createGameSessionResponse.GameSession.GameSessionId : "N/A";
        Debug.Log((int)createGameSessionResponse.HttpStatusCode + " GAME SESSION CREATED: " + gameSessionId);

        return createGameSessionResponse.GameSession;
    }

    async private Task<GameSession> SearchGameSessionsAsync()
    {
        //return null;
        Debug.Log("SearchGameSessions");
        var describeGameSessionsRequest = new DescribeGameSessionsRequest();
        describeGameSessionsRequest.FleetId = FleetId; // can also use AliasId

        Task<DescribeGameSessionsResponse> DescribeGameSessionsResponseTask = _amazonGameLiftClient.DescribeGameSessionsAsync(describeGameSessionsRequest);
        DescribeGameSessionsResponse describeGameSessionsResponse = await DescribeGameSessionsResponseTask;

        int gameSessionCount = describeGameSessionsResponse.GameSessions.Count;
        Debug.Log($"GameSessionCount:  {gameSessionCount}");

        if (gameSessionCount > 0)
        {
            Debug.Log("We have game sessions!");
            int i = 0;
            for (i = 0; i < gameSessionCount; i++)
            {
                // Check player count
                if (describeGameSessionsResponse.GameSessions[i].CurrentPlayerSessionCount >= describeGameSessionsResponse.GameSessions[i].MaximumPlayerSessionCount)
                {
                    Debug.Log("Session " + i + " is full");
                    continue;
                }
                // Check status
                string status = describeGameSessionsResponse.GameSessions[i].Status;
                Debug.Log("Session " + i + " status=" + status);
                if (status == "TERMINATED" || status == "TERMINATING")
                {
                    Debug.Log("Skipping " + status + " session " + i);
                    continue;
                }
                Debug.Log("Using session " + i + ": " + describeGameSessionsResponse.GameSessions[i].GameSessionId);
                return describeGameSessionsResponse.GameSessions[i];
            }
            if (i == gameSessionCount)
            {
                Debug.Log("All game sessions are full or terminated!");
            }
        }
        return null;
    }

    async private void setup()
    {
        Debug.Log("setup");

        _badNetworkClient = GameObject.FindObjectOfType<BADNetworkClient>();

        CreateGameLiftClient();

        // Mock game session queries aren't implemented for local GameLift server testing, so just return null to create new one
        GameSession gameSession = IsArgFlagPresent(IsProdArg) ? await SearchGameSessionsAsync() : null;

        if (gameSession == null)
        {
            // create one game session
            var maxRetryAttempts = 3;
            await RetryHelper.RetryOnExceptionAsync<Exception>
            (maxRetryAttempts, async () =>
            {
                gameSession = await CreateGameSessionAsync();
            });

            if (gameSession != null)
            {
                Debug.Log("Game session created.");
                CreatePlayerSession(gameSession);
            }
            else
            {
                Debug.LogWarning("FAILED to create new game session.");
            }
        }
        else
        {
            Debug.Log("Game session found.");

            // game session found, create player session and connect to server
            CreatePlayerSession(gameSession);
        }
    }

    private void CreateGameLiftClient()
    {
        Debug.Log("CreateGameLiftClient");

        CognitoAWSCredentials credentials = new CognitoAWSCredentials(
           CognitoIdentityPool,
           RegionEndpoint.USEast1
        );

        if (IsArgFlagPresent(IsProdArg))
        {
            _amazonGameLiftClient = new AmazonGameLiftClient(credentials, RegionEndpoint.USEast1);
        }
        else
        {
            // local testing
            // guide: https://docs.aws.amazon.com/gamelift/latest/developerguide/integration-testing-local.html
            AmazonGameLiftConfig amazonGameLiftConfig = new AmazonGameLiftConfig()
            {
                ServiceURL = "http://localhost:9080"
            };
            _amazonGameLiftClient = new AmazonGameLiftClient("asdfasdf", "asdf", amazonGameLiftConfig);
        }
    }

    public GameLiftClient()
    {
        Debug.Log("GameLiftClient created");

        // for this demo just create a randomly generated user id.  Eventually the ID may be tied to a user account.
        _playerUuid = Guid.NewGuid().ToString();

        setup();
    }

    // Helper function for getting the command line arguments
    // src: https://stackoverflow.com/a/45578115/1956540
    public static bool IsArgFlagPresent(string name)
    {
        // FIXME: Force prod
        if (name == GameLiftClient.IsProdArg) return true;
 
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            // Debug.Log("Arg: " + args[i]);
            if (args[i] == name)
            {
                return true;
            }
        }
        return false;
    }
}