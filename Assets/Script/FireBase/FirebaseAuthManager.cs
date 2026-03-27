using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using UnityEngine;
using UniRx;

public class FirebaseAuthManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private DatabaseReference databaseReference;

    private UserProfileManager profileManager;
    private UserSessionManager sessionManager;
    private LeaderboardManager leaderboardManager;

    private void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
        databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        sessionManager = new UserSessionManager(databaseReference);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            LoginUser(sessionManager.DeviceId + "@example.com", "password123"
                , success => 
                { 
                    if (!success) 
                        CreateUser(sessionManager.DeviceId + "@example.com", "password123"
                            , success => Debug.Log("User created and logged in: " + success));
                });
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            profileManager.TryRegisterNickname("NewNickname2", success => Debug.Log("Nickname registered: " + success));
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            profileManager.TryUpdateHighScore(50,success => Debug.Log("Score recorded: " + success));
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
        sessionManager.ValidateDeviceSession(userId, success =>
        {
            if (!success)
            {
                auth.SignOut();
                onComplete?.Invoke(false);
                return;
            }

            leaderboardManager = new LeaderboardManager(userId, databaseReference);
            profileManager = new UserProfileManager(userId, databaseReference);
            

            profileManager.LoadUserData(success =>
            {
                if (success)
                {
                    profileManager.OnNicknameChanged
                    .Subscribe(newData =>
                    {
                        leaderboardManager.UpdateSeasonHighScore(newData);
                    })
                    .AddTo(this); // 컴포넌트가 파괴될 때 구독 해제
                    sessionManager.StartMonitoring(OnSessionTerminated);
                    onComplete?.Invoke(true);
                }
                else
                {
                    OnSessionTerminated();
                    onComplete?.Invoke(false);
                }
            });
        });
    }
    private void OnSessionTerminated()
    {
        if(auth.CurrentUser == null) return;
        sessionManager.ClearSession(() => auth.SignOut());
    }

    //시즌1 상위 랭커 10명 조회 예시
    public void ShowLeaderboard()
    {
        leaderboardManager.GetTopRankers(1, 10, (list) =>
        {
            foreach (var rank in list)
            {
                Debug.Log($"[{rank.nickName}] 점수: {rank.score}");
            }
        });
    }
}
