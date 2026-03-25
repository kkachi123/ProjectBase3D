using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using UnityEngine;

public class FirebaseAuthManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private DatabaseReference databaseReference;

    private UserProfileManager userProfileManager;
    private UserSessionManager userSessionManager;
    private LeaderboardManager leaderboardManager;

    private void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
        databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        userSessionManager = new UserSessionManager(databaseReference);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            LoginUser(userSessionManager.DeviceId + "@example.com", "password123"
                , success => 
                { 
                    if (!success) 
                        CreateUser(userSessionManager.DeviceId + "@example.com", "password123"
                            , success => Debug.Log("User created and logged in: " + success));
                });
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            userProfileManager.RegisterNickname("NewNickname2", success => Debug.Log("Nickname registered: " + success));
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            leaderboardManager.UpdateSeasonHighScore(30, "NewNickname2", success => Debug.Log("Score recorded: " + success));
        }
    }
    private void OnDestroy()
    {
        if (auth.CurrentUser != null)
        {
            OnSessionTerminated();
        }
    }
    private void CreateUser(string email, string password ,Action<bool> onComplete = null)
    {
        auth
        .CreateUserWithEmailAndPasswordAsync(email, password)
        .ContinueWithOnMainThread(task => HandleAuthCompletion(task, onComplete));
    }
    private void LoginUser(string email, string password ,Action<bool> onComplete = null)
    {
        auth
        .SignInWithEmailAndPasswordAsync(email, password)
        .ContinueWithOnMainThread(task => HandleAuthCompletion(task, onComplete));
    }
    private void HandleAuthCompletion(System.Threading.Tasks.Task<AuthResult> task , Action<bool> onComplete = null)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            onComplete?.Invoke(false);
            return;
        }

        string userId = task.Result.User.UserId;
        userSessionManager.ValidateDeviceSession(userId, success =>
        {
            if (!success) auth.SignOut();
            else
            {
                leaderboardManager = new LeaderboardManager(userId, databaseReference);
                userProfileManager = new UserProfileManager(userId, databaseReference);
                userProfileManager.LoadUserData(success =>
                {
                    if (success)
                    {
                        userSessionManager.StartSession(() => userSessionManager.StartMonitoring(OnSessionTerminated));
                    }
                    else
                    {
                        OnSessionTerminated();
                    }
                    onComplete?.Invoke(success);
                });
            }
            onComplete?.Invoke(success);
        });
    }

    /*
    // 예시: 랭킹 보기 버튼 눌렀을 때
    public void ShowLeaderboard()
    {
        leaderboardManager.GetTopRankers(1, 10, (list) => {
            foreach(var rank in list)
            {
                Debug.Log($"[{rank.nickName}] 점수: {rank.score}");
            }
        });
    }
     */

    private void OnSessionTerminated()
    {
        if(auth.CurrentUser == null) return;
        userSessionManager.ClearSession(() => auth.SignOut());
    }
}
