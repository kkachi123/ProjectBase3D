using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using UnityEngine;

// [데이터 구조] 유저 정보 클래스
public class UserData
{
    public string userId;
    public string nickName;

    public UserData(string id, string name)
    {
        userId = id;
        nickName = name;
    }
}

public class UserDataManager
{
    public UserData userData { get; private set; }

    private FirebaseAuth auth;
    private DatabaseReference databaseReference;
    private int nicknameRetryCount = 0; // 무한 재귀 방지용

    public void Initialize(FirebaseAuth firebaseAuth, DatabaseReference dbReference)
    {
        auth = firebaseAuth;
        databaseReference = dbReference;
    }

    // 1. 유저 데이터 로드 시도
    public void LoadUserData()
    {
        if (auth == null || databaseReference == null || auth.CurrentUser == null)
        {
            Debug.LogError("Firebase not initialized or User not logged in.");
            return;
        }

        string userId = auth.CurrentUser.UserId;
        databaseReference.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Failed to load user data: " + task.Exception);
                return;
            }

            DataSnapshot snapshot = task.Result;
            if (snapshot.Exists)
            {
                // 데이터가 있으면 로드
                userData = JsonUtility.FromJson<UserData>(snapshot.GetRawJsonValue());
                Debug.Log($"User Data Loaded: {userData.nickName}");
            }
            else
            {
                // 데이터가 없으면 중복 없는 닉네임 생성 시작
                Debug.LogWarning("No user data found. Generating unique nickname...");
                nicknameRetryCount = 0;
                GenerateUniqueNickname(userId);
            }
        });
    }

    // 2. 중복되지 않는 랜덤 닉네임 생성 (재귀)
    private void GenerateUniqueNickname(string userId)
    {
        if (nicknameRetryCount > 10)
        {
            Debug.LogError("Failed to generate unique nickname after 10 attempts.");
            return;
        }

        int randomNumber = UnityEngine.Random.Range(1000, 9999);
        string newNickname = "User" + randomNumber;
        userData = new UserData(userId, newNickname);

        // SaveUserData를 호출하여 중복 체크 (Transaction 실행)
        SaveUserData((isSuccess) => {
            if (!isSuccess)
            {
                nicknameRetryCount++;
                GenerateUniqueNickname(userId); // 중복 시 재시도
            }
            else
            {
                Debug.Log($"Unique Nickname Registered: {newNickname}");
            }
        });
    }

    // 3. 닉네임 중복 체크 및 데이터 최종 저장
    public void SaveUserData(Action<bool> onComplete = null)
    {
        if (auth == null || databaseReference == null || userData == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        var nickNameRef = databaseReference.Child("nicknames").Child(userData.nickName);

        // RunTransaction의 결과 Task는 Task<DataSnapshot>이 아니라 내부적으로 TransactionResult를 처리합니다.
        nickNameRef.RunTransaction(mutableData =>
        {
            // 데이터가 이미 존재하면 중복이므로 중단(Abort)
            if (mutableData.Value != null)
            {
                return TransactionResult.Abort();
            }

            // 존재하지 않으면 내 ID를 할당하여 닉네임 선점
            mutableData.Value = userData.userId;
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Transaction Error: " + task.Exception);
                onComplete?.Invoke(false);
                return;
            }

            // TransactionResult를 통해 성공(Committed) 여부 확인
            // task.Result가 자동으로 TransactionResult로 캐스팅되지 않을 경우 명시적 확인 필요
            //if (task.Result.Committed)
            //{
            //    // 닉네임 등록 성공 -> 유저 상세 정보(JSON) 저장
            //    SaveUserDetailJson(onComplete);
            //}
            //else
            //{
            //    // 중복 발생 (Committed == false)
            //    onComplete?.Invoke(false);
            //}
        });
    }

    // 4. 실제 유저 상세 데이터(JSON)를 users 노드에 저장
    private void SaveUserDetailJson(Action<bool> onComplete)
    {
        string jsonData = JsonUtility.ToJson(userData);
        databaseReference.Child("users").Child(userData.userId).SetRawJsonValueAsync(jsonData)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    onComplete?.Invoke(true);
                }
                else
                {
                    Debug.LogError("Failed to save User Detail JSON");
                    onComplete?.Invoke(false);
                }
            });
    }

    // [기타] 점수 저장 (시즌별)
    public void SaveScore(int score, int seasonNumber)
    {
        if (userData == null) return;

        databaseReference
            .Child("leaderboard")
            .Child("season_" + seasonNumber)
            .Child(userData.userId)
            .SetValueAsync(score);
    }
}