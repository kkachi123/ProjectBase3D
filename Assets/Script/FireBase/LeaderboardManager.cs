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
    private int seasonNumber = 1;

    private string userId;
    private DatabaseReference leaderboardRef;

    public LeaderboardManager(string userId, DatabaseReference dbReference)
    {
        this.userId = userId;
        leaderboardRef = dbReference.Child(DatabaseSchema.LEADERBOARD).Child("season_" + seasonNumber).Child(userId);
    }

    public void UpdateSeasonHighScore(int newScore , string nickName, Action<bool> onComplete = null)
    {
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

                if (newScore <= currentScore) { onComplete?.Invoke(true); return; }

                var updates = new Dictionary<string, object> {
                    { DatabaseSchema.NICKNAME, nickName },
                    { DatabaseSchema.SCORE, newScore }
                };
                leaderboardRef.UpdateChildrenAsync(updates).ContinueWithOnMainThread(task =>
                {
                    onComplete?.Invoke(task.IsCompleted);
                });
            });
    }

    /// 상위 10명의 랭킹 데이터를 가져옵니다.
    /*
    public void GetTopRankers(int seasonNumber, int limit, Action<List<RankEntry>> onComplete)
    {
        // 1. 점수(score) 기준으로 정렬하고, 마지막(가장 높은 쪽)부터 limit개만큼 가져옴
        Query topRankersQuery = databaseReference.Child(DatabaseSchema.LEADERBOARD)
                                                 .Child("season_" + seasonNumber)
                                                 .OrderByChild(DatabaseSchema.SCORE)
                                                 .LimitToLast(limit);

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
    */
}
