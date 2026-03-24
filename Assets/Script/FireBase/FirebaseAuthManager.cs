using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using UnityEngine;

public class FirebaseAuthManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private DatabaseReference databaseReference;

    private UserDataManager userDataManager;


    private void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
        databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        userDataManager = new UserDataManager();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            LoginUser(userDataManager.devieId + "@example.com", "password123");
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            userDataManager.RecordScore(30, 1, success => Debug.Log("Score recorded: " + success));
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            userDataManager.RegisterNickname("NewNickname", success => Debug.Log("Nickname registered: " + success));
        }
    }
    private void OnDestroy()
    {
        if (auth.CurrentUser != null)
        {
            LogoutUser();
        }
    }
    private void CreateUser(string email, string password)
    {
        auth
        .CreateUserWithEmailAndPasswordAsync(email, password)
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("User creation failed: " + task.Exception);
                return;
            }

            FirebaseUser loggedInUser = task.Result.User;
            HandleLoginSuccess(loggedInUser.UserId);
        });
    }
    private void LoginUser(string email, string password)
    {
        auth
        .SignInWithEmailAndPasswordAsync(email, password)
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Login failed: " + task.Exception);
                CreateUser(email, password); // 로그인 실패 시 사용자 생성 시도
                return;
            }
            
            FirebaseUser loggedInUser = task.Result.User;
            HandleLoginSuccess(loggedInUser.UserId);
        });
    }
    private void LogoutUser()
    {
        if(auth.CurrentUser == null) return; 

        // devieId 초기화
        string uid = auth.CurrentUser.UserId;
        // 로그아웃 시 DB의 deviceId를 지우는 작업은 '성공 여부와 상관없이' 결과적으로 로그아웃 시킵니다.
        databaseReference.Child(FirebasePaths.USERS).Child(uid).Child(FirebasePaths.DEVICE_ID).SetValueAsync(null)
            .ContinueWithOnMainThread(task => {
                auth.SignOut();
                Debug.Log("Logged out and device ID cleared.");
            });
    }
    private void HandleLoginSuccess(string userId)
    {
        CheckDeviceStatus(userId, success =>
        {
            if (success)
            {
                userDataManager.Initialize(auth, databaseReference);
                userDataManager.LoadUserData(isLoaded => {
                    if (isLoaded) Debug.Log("모든 데이터 로드 및 닉네임 설정 완료");
                });
            }
            else
            {
                Debug.LogError("기기 정보 불일치");
                auth.SignOut();
            }
        });
    }

    private void CheckDeviceStatus(string userId , Action<bool> onComplete = null)
    {
        var deviceRef = databaseReference.Child(FirebasePaths.USERS).Child(userId).Child(FirebasePaths.DEVICE_ID);

        deviceRef.GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted) { onComplete?.Invoke(false); return; }

            string dbDeviceId = task.Result.Value as string;
            string userDeviceId = userDataManager.devieId;
            // 기기가 비어있거나 내 기기와 일치하는 경우
            if (string.IsNullOrEmpty(dbDeviceId) || dbDeviceId == userDeviceId)
            {
                deviceRef.SetValueAsync(userDeviceId); // 내 기기로 갱신
                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        });
    }

}
