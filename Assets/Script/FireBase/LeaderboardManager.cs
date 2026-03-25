using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RankEntry
{
    public string userId;
    public string nickName;
    public int score;

    public RankEntry(string id, string name, int s)
    {
        userId = id;
        nickName = name;
        score = s;
    }
}
public class LeaderboardManager
{
    private int curSeasonNumber;

    private string userId;
    private DatabaseReference leaderboardRef;

    public LeaderboardManager(string userId, DatabaseReference dbReference)
    {
        this.userId = userId;
        leaderboardRef = dbReference.Child(DatabaseSchema.LEADERBOARD);
        curSeasonNumber = DatabaseSchema.CURRENT_SEASON;
    }

    public void UpdateSeasonHighScore(int newScore ,Action<bool> onComplete = null)
    {
        var scorePath = leaderboardRef.Child(DatabaseSchema.SEASON_PREFIX + curSeasonNumber).Child(userId).Child(DatabaseSchema.SCORE);

        /**/
        scorePath
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled) { onComplete?.Invoke(false); return; }

                int currentScore = task.Result.Exists ? Convert.ToInt32(task.Result.Value) : 0;

                if (newScore <= currentScore) { onComplete?.Invoke(true); return; }

                scorePath
                .SetValueAsync(newScore)
                .ContinueWithOnMainThread(t => onComplete?.Invoke(t.IsCompleted));
            });

        // Get -> 비교 -> Set 방식 (트랜잭션보다 간단하지만, 동시 업데이트 시 낮은 점수가 덮어씌워질 수 있음)
        /*
        scorePath.RunTransaction(mutableData =>
        {
            int currentScore = 0;
            if (mutableData.Value != null) currentScore = Convert.ToInt32(mutableData.Value);
            if (newScore <= currentScore) return TransactionResult.Abort();
            mutableData.Value = newScore;
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled) { onComplete?.Invoke(false); return; }

            // task.Result.Committed가 true면 성공, false면 Abort(점수가 낮아서 안 바뀜)
            onComplete?.Invoke(task.IsCompleted);
        });
        */
    }

    public void UpdateNicknameInLeaderboard(string newNickname)
    {
        var nickNamePath = leaderboardRef.Child(DatabaseSchema.SEASON_PREFIX + curSeasonNumber).Child(userId).Child(DatabaseSchema.NICKNAME);

        // 시즌 번호는 DatabaseSchema.CURRENT_SEASON 등을 활용
        nickNamePath
            .SetValueAsync(newNickname)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted) Debug.Log($"[Leaderboard] 닉네임 동기화 완료: {newNickname}");
            });

        /*
        nickNamePath.RunTransaction(mutableData =>
        {
            mutableData.Value = newNickname;
            return TransactionResult.Success(mutableData);
        })
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted) Debug.Log($"[Leaderboard] 닉네임 동기화 완료: {newNickname}");
        });
        */
    }

    // 시즌 seasonNumber 상위 limit명의 랭킹 데이터를 가져옵니다.
    public void GetTopRankers(int seasonNumber, int limit, Action<List<RankEntry>> onComplete)
    {
        // 1. 점수(score) 기준으로 정렬하고, 마지막(가장 높은 쪽)부터 limit개만큼 가져옴
        Query topRankersQuery = 
            leaderboardRef
            .Child(DatabaseSchema.SEASON_PREFIX + seasonNumber)
            .OrderByChild(DatabaseSchema.SCORE).LimitToLast(limit);

        topRankersQuery.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            List<RankEntry> rankList = new List<RankEntry>();

            if (task.IsFaulted || !task.Result.Exists)
            {
                onComplete?.Invoke(rankList);
                return;
            }

            // 2. 결과 가공 (LimitToLast는 오름차순으로 오므로 리스트를 뒤집어 내림차순 정렬)
            foreach (DataSnapshot snapshot in task.Result.Children)
            {
                string id = snapshot.Key;
                string name = snapshot.Child(DatabaseSchema.NICKNAME).Value.ToString();
                int score = Convert.ToInt32(snapshot.Child(DatabaseSchema.SCORE).Value);

                rankList.Add(new RankEntry(id, name, score));
            }

            // 점수 높은 순으로 정렬 (내림차순)
            rankList.Reverse();
            onComplete?.Invoke(rankList);
        });
    }

}
