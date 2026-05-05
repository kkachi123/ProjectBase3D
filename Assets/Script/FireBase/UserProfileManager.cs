using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

[Serializable]
public class UserData
{
    public string nickName;
    public int highScore; 

    public UserData(string nickName)
    {
        this.nickName = nickName;
        highScore = 0;
    }
}
#region wrapper Class
public class LeaderboardUserEntry
{
    public string nickName;
    public int score;
    public LeaderboardUserEntry(string nickName, int score)
    {
        this.nickName = nickName;
        this.score = score;
    }
}
#endregion

public class UserProfileManager
{
    public UserData userData { get; private set; }

    private string userId; 
    private DatabaseReference rootRef;

    private Subject<LeaderboardUserEntry> onUpdateHighScore = new Subject<LeaderboardUserEntry>();
    public IObservable<LeaderboardUserEntry> OnNicknameChanged => onUpdateHighScore;

    public UserProfileManager(string userId, DatabaseReference dbReference)
    {
        this.userId = userId;
        rootRef = dbReference;
    }
    #region Load User Data
    public void LoadUserData(Action<bool> onComplete = null)
    {
        var userRef = rootRef.Child(DatabaseSchema.USERS).Child(userId);

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

                if (needsNickname) GenerateUniqueNickname(onComplete);
                else onComplete?.Invoke(true);
            });
    }

    private const int MAX_NICKNAME_RETRIES = 5;
    private void GenerateUniqueNickname(Action<bool> onComplete = null, int retryCount = MAX_NICKNAME_RETRIES)
    {
        if (retryCount <= 0)
        {
            Debug.LogError("닉네임 생성 실패: 최대 재시도 횟수를 초과했습니다.");
            onComplete?.Invoke(false);
            return;
        }

        string newNickname = "Player" + UnityEngine.Random.Range(100, 9999);

        TryRegisterNickname(newNickname, success =>
        {
            if (success) onComplete?.Invoke(true);
            else
            {
                // 아주 짧은 딜레이(50ms) 후 재시도하여 스택 오버플로 방지 및 DB 부하 분산
                Observable.Timer(TimeSpan.FromMilliseconds(50))
                    .Subscribe(_ => GenerateUniqueNickname(onComplete, retryCount - 1));
            }
        });
    }
    #endregion
    #region Save User Data
    private void SaveUserData(Action<bool> onComplete = null)
    {
        string jsonData = JsonUtility.ToJson(userData);

        var userDataRef = rootRef.Child(DatabaseSchema.USERS).Child(userId).Child(DatabaseSchema.DATA);

        userDataRef.SetRawJsonValueAsync(jsonData)
            .ContinueWithOnMainThread(task =>
            {
                bool isSuccess = task.IsCompleted && !task.IsFaulted;
                onComplete?.Invoke(isSuccess);
            });
    }

    public void TryRegisterNickname(string newNickName, Action<bool> onComplete = null)
    {
        string oldNickName = userData.nickName;

        if (newNickName == oldNickName)
        {
            onComplete?.Invoke(true);
            return;
        }
        var newNickNameRef = rootRef.Child(DatabaseSchema.NICKNAMES).Child(newNickName);

        newNickNameRef.RunTransaction(mutableData =>
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
                    newNickNameRef.SetValueAsync(null);
                    userData.nickName = oldNickName; // 이전 닉네임이 있다면 복구하는 로직이 필요할 수 있음
                    onComplete?.Invoke(false);
                    return;
                }

                // 기존 닉네임이 DB에서 삭제 (중복 방지)
                if (!string.IsNullOrEmpty(oldNickName))
                    rootRef.Child(DatabaseSchema.NICKNAMES).Child(oldNickName).RemoveValueAsync();

                // Leaderboard 업데이트 (닉네임 변경이 반영되도록)
                LeaderboardUserEntry entry = new LeaderboardUserEntry(userData.nickName, userData.highScore);
                onUpdateHighScore?.OnNext(entry);

                onComplete?.Invoke(true);
            });
        });
    }

    public void TryUpdateHighScore(int newScore, Action<bool> onComplete = null)
    {
        if (newScore <= userData.highScore)
        {
            onComplete?.Invoke(true); // 점수가 낮거나 같으면 업데이트 필요 없음
            return;
        }
        userData.highScore = newScore;
        SaveUserData(onComplete : success =>
        {
            if (success)
            {
                LeaderboardUserEntry entry = new LeaderboardUserEntry(userData.nickName, userData.highScore);
                onUpdateHighScore?.OnNext(entry);
            }
            onComplete?.Invoke(success);
        });
    }
    #endregion
}