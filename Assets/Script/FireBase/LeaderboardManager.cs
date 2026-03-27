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

    public void UpdateSeasonHighScore(LeaderboardEntry newData, Action<bool> onComplete = null)
    {
        string jsonData = JsonUtility.ToJson(newData);

        var scorePath = leaderboardRef.Child(DatabaseSchema.SEASON_PREFIX + curSeasonNumber).Child(userId);

        scorePath
            .SetRawJsonValueAsync(jsonData)
            .ContinueWithOnMainThread(task =>
            {
                bool isSuccess = task.IsCompleted && !task.IsFaulted;
                onComplete?.Invoke(isSuccess);
            });
    }

    // 시즌 seasonNumber 상위 limit명의 랭킹 데이터를 가져옵니다.
    public void GetTopRankers(int seasonNumber, int limit, Action<List<RankEntry>> onComplete)
    {
        // 1. 점수(score) 기준으로 정렬하고, 마지막(가장 높은 쪽)부터 limit개만큼 가져옴
        Query topRankersQuery = 
            leaderboardRef
            .Child(DatabaseSchema.SEASON_PREFIX + seasonNumber)
            .OrderByChild(DatabaseSchema.LEADERBOARD_SCORE).LimitToLast(limit);

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
                string name = snapshot.Child(DatabaseSchema.LEADERBOARD_NICKNAME).Value.ToString();
                int score = Convert.ToInt32(snapshot.Child(DatabaseSchema.LEADERBOARD_SCORE).Value);

                rankList.Add(new RankEntry(id, name, score));
            }

            // 점수 높은 순으로 정렬 (내림차순)
            rankList.Reverse();
            onComplete?.Invoke(rankList);
        });
    }

}
