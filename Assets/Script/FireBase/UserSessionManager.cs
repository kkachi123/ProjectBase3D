using Firebase.Database;
using Firebase.Extensions;
using System;
using UnityEngine;
public class UserSessionManager
{
    private DatabaseReference deviceRef;
    private EventHandler<ValueChangedEventArgs> sessionHandler;
    private string userId;

    public string DeviceId => SystemInfo.deviceUniqueIdentifier;

    public UserSessionManager(DatabaseReference dbRef)
    {
        deviceRef = dbRef.Child(DatabaseSchema.USERS).Child(userId).Child(DatabaseSchema.DEVICE_ID);
    }

    // 기기 검증 및 세션 점유
    public void ValidateDeviceSession(string uid, Action<bool> onComplete)
    {
        userId = uid;
        deviceRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted) { onComplete?.Invoke(false); return; }

            string dbDeviceId = task.Result.Value as string;
            if (string.IsNullOrEmpty(dbDeviceId) || dbDeviceId == DeviceId)
            {
                deviceRef.SetValueAsync(DeviceId);
                onComplete?.Invoke(true);
            }
            else onComplete?.Invoke(false);
        });
    }

    // 실시간 밀어내기 감시 시작
    public void StartMonitoring(Action onKicked)
    {
        sessionHandler = (sender, args) =>
        {
            string serverId = args.Snapshot.Value as string;
            if (!string.IsNullOrEmpty(serverId) && serverId != DeviceId)
            {
                StopMonitoring();
                onKicked?.Invoke();
            }
        };
        deviceRef.ValueChanged += sessionHandler;
    }

    private void StopMonitoring()
    {
        if (deviceRef != null && sessionHandler != null)
        {
            deviceRef.ValueChanged -= sessionHandler;
            deviceRef = null;
            sessionHandler = null;
        }
    }
    public void StartSession(Action onComplete)
    {
        deviceRef.SetValueAsync(DeviceId).ContinueWithOnMainThread(_ => onComplete?.Invoke());
    }
    public void ClearSession(Action onComplete)
    {
        deviceRef.SetValueAsync(null).ContinueWithOnMainThread(_ => onComplete?.Invoke());
        StopMonitoring();
    }
}
