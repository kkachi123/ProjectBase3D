using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UserData
{
    public string nickName;

    public UserData(string nickName)
    {
        this.nickName = nickName;
    }
}
#region wrapper Class
public class UserAccountEntity
{
    public UserData data;
    public string deviceId; // 중복 로그인 방지
}
#endregion

public class UserDataManager
{
    public UserData userData { get; private set; }

    private string userId; 
    private DatabaseReference databaseReference;
    public string DeviceId => SystemInfo.deviceUniqueIdentifier; // 현재 디바이스 ID 반환
    public void Initialize(string userId, DatabaseReference dbReference)
    {
        this.userId = userId;
        databaseReference = dbReference;
        LoadUserData();
    }
    #region Load User Data
    public void LoadUserData(Action<bool> onComplete = null)
    {
        var userRef = databaseReference.Child(DatabaseSchema.USERS).Child(userId);

        userRef
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted) { onComplete?.Invoke(false); return; }

                DataSnapshot snapshot = task.Result;
                bool needsNickname = false;

                if (snapshot.Exists && snapshot.HasChild(DatabaseSchema.DATA))
                {
                    string json = snapshot.Child(DatabaseSchema.DATA).GetRawJsonValue();
                    userData = JsonUtility.FromJson<UserData>(json);

                    if (string.IsNullOrEmpty(userData.nickName)) needsNickname = true;
                }
                else
                {
                    userData = new UserData("");
                    needsNickname = true;
                }

                if (needsNickname) GenerateUniqueNickname(userId, onComplete);
                else onComplete?.Invoke(true);
            });
    }

    private const int MAX_NICKNAME_RETRIES = 5;
    private void GenerateUniqueNickname(string userId, Action<bool> onComplete = null, int retryCount = MAX_NICKNAME_RETRIES)
    {
        if (retryCount <= 0)
        {
            Debug.LogError("닉네임 생성 실패: 최대 재시도 횟수를 초과했습니다.");
            onComplete?.Invoke(false);
            return;
        }

        string newNickname = "User_" + userId.Substring(0, 4) + UnityEngine.Random.Range(100, 9999);

        var nickNameRef = databaseReference.Child(DatabaseSchema.NICKNAMES).Child(newNickname);
        nickNameRef.RunTransaction(mutableData =>
        {
            if (mutableData.Value != null) return TransactionResult.Abort();
            mutableData.Value = userId;
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                // 실패 시 재시도
                GenerateUniqueNickname(userId, onComplete , retryCount - 1);
                return;
            }

            userData.nickName = newNickname;
            SaveUserData(saveSuccess =>
            {
                if (!saveSuccess)
                {
                    // 저장 실패 시 선점했던 닉네임 다시 삭제 
                    nickNameRef.SetValueAsync(null);
                    userData.nickName = "";
                }
                onComplete?.Invoke(saveSuccess);
            });
        });
    }
    #endregion
    #region Save User Data
    private void SaveUserData(Action<bool> onComplete = null)
    {
        UserAccountEntity node = new UserAccountEntity { data = userData, deviceId = DeviceId };
        string jsonData = JsonUtility.ToJson(node);

        var userRef = databaseReference.Child(DatabaseSchema.USERS).Child(userId);

        userRef.SetRawJsonValueAsync(jsonData)
            .ContinueWithOnMainThread(task =>
            {
                bool isSuccess = task.IsCompleted && !task.IsFaulted;
                onComplete?.Invoke(isSuccess);
            });
    }
    public void UpdateSeasonHighScore(int score, int seasonNumber, Action<bool> onComplete = null)
    {
        var leaderboardRef = databaseReference.Child(DatabaseSchema.LEADERBOARD).Child("season_" + seasonNumber).Child(userId);
        leaderboardRef.Child(DatabaseSchema.SCORE)
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted) { onComplete?.Invoke(false); return; }

                int currentScore = 0;
                if (task.Result.Exists)
                {
                    currentScore = Convert.ToInt32(task.Result.Value);
                }

                if (score <= currentScore) { onComplete?.Invoke(true); return; }

                var updates = new Dictionary<string, object> {
                    { DatabaseSchema.NICKNAME, userData.nickName },
                    { DatabaseSchema.SCORE, score }
                };
                leaderboardRef.UpdateChildrenAsync(updates).ContinueWithOnMainThread(task => {
                    onComplete?.Invoke(task.IsCompleted);
                });
            });
    }

    public void RegisterNickname(string newNickName, Action<bool> onComplete = null)
    {
        string oldNickName = userData.nickName;

        if (newNickName == oldNickName)
        {
            onComplete?.Invoke(true);
            return;
        }
        var nickNameRef = databaseReference.Child(DatabaseSchema.NICKNAMES).Child(newNickName);

        nickNameRef.RunTransaction(mutableData =>
        {
            if (mutableData.Value != null) return TransactionResult.Abort();
            mutableData.Value = userId;
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                onComplete?.Invoke(false);
                return;
            }

            userData.nickName = newNickName;
            SaveUserData(saveSuccess =>
            {
                if (!saveSuccess)
                {
                    // 롤백: 선점한 닉네임 삭제
                    nickNameRef.SetValueAsync(null);
                    userData.nickName = oldNickName; // 이전 닉네임이 있다면 복구하는 로직이 필요할 수 있음
                    onComplete?.Invoke(false);
                    return;
                }

                SyncNicknameAcrossServices();

                // 기존 닉네임이 있다면 DB에서 삭제 (중복 방지 위해)
                if (!string.IsNullOrEmpty(oldNickName))
                {
                    databaseReference.Child(DatabaseSchema.NICKNAMES).Child(oldNickName).RemoveValueAsync();
                }
                onComplete?.Invoke(true);
            });
        });
    }
    #endregion
    #region Update Nickname in All Categories (e.g., Leaderboard)
    private void SyncNicknameAcrossServices()
    {
        UpdateLeaderboardNickName(1);
    }
    private void UpdateLeaderboardNickName(int seasonNumber, Action<bool> onComplete = null)
    {
        var leaderboardRef = databaseReference.Child(DatabaseSchema.LEADERBOARD).Child("season_" + seasonNumber).Child(userId);
        leaderboardRef.Child(DatabaseSchema.NICKNAME)
            .SetValueAsync(userData.nickName)
            .ContinueWithOnMainThread(task =>
            {
                bool isSuccess = task.IsCompleted && !task.IsFaulted;
                onComplete?.Invoke(isSuccess);
            });
    }
    #endregion
}