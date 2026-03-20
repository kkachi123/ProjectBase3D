using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

public class TestConnect : MonoBehaviour
{
    public class cPlayer
    {
        public string name;
        public int score;
        public int playerType;
        public int successCustomer;

        public cPlayer() { }

        public cPlayer(string name, int score, int playerType, int successCustomer)
        {
            this.name = name;
            this.score = score;
            this.playerType = playerType;
            this.successCustomer = successCustomer;
        }
    }
    // Firebase Init
    Firebase.FirebaseApp app;
    DatabaseReference reference;
    void Start()
    {
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                // Create and hold a reference to your FirebaseApp,
                // where app is a Firebase.FirebaseApp property of your application class.
                app = Firebase.FirebaseApp.DefaultInstance;

                // Set a flag here to indicate whether Firebase is ready to use by your app.
                reference = FirebaseDatabase.DefaultInstance.RootReference;
            }
            else
            {
                UnityEngine.Debug.LogError(System.String.Format(
                  "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                // Firebase Unity SDK is not safe to use here.
            }
        });
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            WriteNewPlayer("Player1", 100, 1, 10);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            WriteNewPlayer("Player2", 200, 2, 20);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            WriteNewPlayer("Player3", 300, 3, 30);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            DeletePlayer("Player1");
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            DeleteAllPlayer();
        }
    }

    void WriteNewPlayer(string name, int score, int playerType, int successCustomer)
    {
        cPlayer player = new cPlayer(name, score, playerType, successCustomer);
        string json = JsonUtility.ToJson(player);
        reference.Child(name).SetRawJsonValueAsync(json);
    }
    void DeletePlayer(string name)
    {
        reference.Child(name).RemoveValueAsync();
    }

    void DeleteAllPlayer()
    {
        reference.RemoveValueAsync();
    }
}
