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
            LoginUser(userDataManager.DeviceId + "@example.com", "password123"
                , success => 
                { 
                    if (!success) 
                        CreateUser(userDataManager.DeviceId + "@example.com", "password123"
                            , success => Debug.Log("User created and logged in: " + success));
                });
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            userDataManager.RegisterNickname("NewNickname2", success => Debug.Log("Nickname registered: " + success));
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            userDataManager.UpdateSeasonHighScore(30, 1, success => Debug.Log("Score recorded: " + success));
        }
    }
    private void OnDestroy()
    {
        if (auth.CurrentUser != null)
        {
            LogoutUser();
        }
    }
    private void CreateUser(string email, string password ,Action<bool> onComplete = null)
    {
        auth
        .CreateUserWithEmailAndPasswordAsync(email, password)
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                onComplete?.Invoke(false);
                return;
            }

            FirebaseUser loggedInUser = task.Result.User;
            HandleLoginSuccess(loggedInUser.UserId, success =>
            {
                if (!success) auth.SignOut();
                onComplete?.Invoke(success);
            });
        });
    }
    private void LoginUser(string email, string password ,Action<bool> onComplete = null)
    {
        auth
        .SignInWithEmailAndPasswordAsync(email, password)
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                onComplete?.Invoke(false);
                return;
            }
            
            FirebaseUser loggedInUser = task.Result.User;
            HandleLoginSuccess(loggedInUser.UserId, success =>
            {
                if (!success) auth.SignOut();
                onComplete?.Invoke(success);
            });
        });
    }
    private void LogoutUser()
    {
        if(auth.CurrentUser == null) return; 
        string uid = auth.CurrentUser.UserId;
        // 로그아웃 시 DB의 deviceId를 지우는 작업은 '성공 여부와 상관없이' 결과적으로 로그아웃 시킵니다.
        databaseReference
            .Child(DatabaseSchema.USERS).Child(uid).Child(DatabaseSchema.DEVICE_ID)
            .SetValueAsync(null)
            .ContinueWithOnMainThread(task => auth.SignOut());
    }

    #region Login Success Handling and Device Check
    private void HandleLoginSuccess(string userId , Action<bool> onComplete = null)
    {
        ValidateDeviceSession(userId, success =>
        {
            if (success)
            {
                string userId = auth.CurrentUser.UserId;
                userDataManager.Initialize(userId, databaseReference);
            }
            else onComplete?.Invoke(false);
        });
    }

    private void ValidateDeviceSession(string userId , Action<bool> onComplete = null)
    {
        var deviceRef = databaseReference.Child(DatabaseSchema.USERS).Child(userId).Child(DatabaseSchema.DEVICE_ID);

        deviceRef.GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted) { onComplete?.Invoke(false); return; }

            string dbDeviceId = task.Result.Value as string;
            string userDeviceId = userDataManager.DeviceId;
            if (string.IsNullOrEmpty(dbDeviceId) || dbDeviceId == userDeviceId)
            {
                deviceRef.SetValueAsync(userDeviceId); 
                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        });
    }
    #endregion
}
