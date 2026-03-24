using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class UserData
{
    public string nickName;

    public UserData(string nickName)
    {
        this.nickName = nickName;
    }
}
// wrapper 클래스 추가 (UserData와 device ID를 함께 저장)
public class UserNode
{
    public UserData data;
    public string deviceId; // 디바이스 ID 추가 (중복 로그인 방지용)
}

public class UserDataManager
{
    public UserData userData { get; private set; }
    public string devieId => SystemInfo.deviceUniqueIdentifier; // 현재 디바이스 ID 반환

    private FirebaseAuth auth;
    private DatabaseReference databaseReference;
    public void Initialize(FirebaseAuth firebaseAuth, DatabaseReference dbReference)
    {
        auth = firebaseAuth;
        databaseReference = dbReference;
        LoadUserData();
    }

    // 유저 데이터 로드 시도
    public void LoadUserData(Action<bool> onComplete = null)
    {
        if (auth?.CurrentUser == null) return;

        string userId = auth.CurrentUser.UserId;
        var userRef = databaseReference.Child(FirebasePaths.USERS).Child(userId);

        userRef
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted) { onComplete?.Invoke(false); return; }

                DataSnapshot snapshot = task.Result;
                bool needsNickname = false;

                if (snapshot.Exists && snapshot.HasChild(FirebasePaths.DATA))
                {
                    string json = snapshot.Child(FirebasePaths.DATA).GetRawJsonValue();
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
    private void GenerateUniqueNickname(string userId ,Action<bool> onComplete = null)
    {
        string newNickname = "User_" + UnityEngine.Random.Range(100000, 999999);

        var nickNameRef = databaseReference.Child(FirebasePaths.NICKNAMES).Child(newNickname);
        nickNameRef.RunTransaction(mutableData =>
        {
            // 닉네임이 이미 존재하면 중단
            if (mutableData.Value != null) return TransactionResult.Abort();
            mutableData.Value = userId; // 닉네임과 유저 ID 매핑 저장
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                // 닉네임 충돌 시 재생성 시도
                GenerateUniqueNickname(userId, onComplete); 
                return;
            }

            userData.nickName = newNickname;
            // 1. 저장 실패 시 닉네임 선점 롤백 로직 포함
            SaveUserData(success =>
            {
                if (!success)
                {
                    // 저장 실패 시 선점했던 닉네임 다시 삭제 (Rollback)
                    nickNameRef.SetValueAsync(null);
                    userData.nickName = "";
                }
                onComplete?.Invoke(success);
            });
        });
    }

    private void SaveUserData(Action<bool> onComplete = null)
    {
        UserNode node = new UserNode { data = userData, deviceId = devieId };

        string jsonData = JsonUtility.ToJson(node);
        var userRef = databaseReference.Child(FirebasePaths.USERS).Child(auth.CurrentUser.UserId);

        userRef.SetRawJsonValueAsync(jsonData)
            .ContinueWithOnMainThread(task =>
            {
                bool isSuccess = task.IsCompleted && !task.IsFaulted;
                onComplete?.Invoke(isSuccess);
            });
    }

    public void RegisterNickname(string nickName, Action<bool> onComplete = null)
    {
        var nickNameRef = databaseReference.Child(FirebasePaths.NICKNAMES).Child(nickName);

        nickNameRef.RunTransaction(mutableData =>
        {
            if (mutableData.Value != null)
            {
                return TransactionResult.Abort();
            }
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task => 
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                onComplete?.Invoke(false);
                return;
            }

            userData.nickName = nickName;
            SaveUserData(saveSuccess => {
                if (!saveSuccess)
                {
                    // 롤백: 선점한 닉네임 삭제
                    nickNameRef.SetValueAsync(null);
                    userData.nickName = ""; // 이전 닉네임이 있다면 복구하는 로직이 필요할 수 있음
                }
                onComplete?.Invoke(saveSuccess);
            });
        });
    }
    public void RecordScore(int score, int seasonNumber, Action<bool> onComplete = null)
    {
        var recordRef = databaseReference.Child(FirebasePaths.LEADERBOARD).Child("season_" + seasonNumber).Child(auth.CurrentUser.UserId);

        recordRef.RunTransaction(mutableData =>
        {
            int currentScore = 0;
            // 기존 점수가 존재하면 가져오기 (최고 점수 유지)
            if (mutableData.Value != null)
            {
                // 데이터 구조가 바뀌었으므로 사전(Dictionary) 형태로 읽어야 함
                var dict = mutableData.Value as Dictionary<string, object>;
                if (dict != null && dict.ContainsKey(FirebasePaths.SCORE))
                    currentScore = Convert.ToInt32(dict[FirebasePaths.SCORE]);
            }
            // 점수가 낮으면 업데이트 중단(Abort)
            if (score <= currentScore) return TransactionResult.Abort();

            // 새로운 점수로 업데이트
            // 닉네임도 함께 저장하여 리더보드에서 표시 가능
            var newData = new Dictionary<string, object>();
            newData[FirebasePaths.SCORE] = score;
            newData[FirebasePaths.NICKNAME] = userData.nickName;

            mutableData.Value = newData;
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task =>
        {
            bool isSuccess = task.IsCompleted && !task.IsFaulted;
            onComplete?.Invoke(isSuccess);
        });
    }
}