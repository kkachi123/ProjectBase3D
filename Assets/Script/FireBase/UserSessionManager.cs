using Firebase.Database;
using Firebase.Extensions;
using System;
using UnityEngine;
public class UserSessionManager
{
    private DatabaseReference rootRef;
    private DatabaseReference deviceRef;
    private EventHandler<ValueChangedEventArgs> sessionHandler;
    private string userId;

    public string DeviceId => SystemInfo.deviceUniqueIdentifier;

    public UserSessionManager(DatabaseReference dbRef) => rootRef = dbRef;

    // 기기 검증 및 세션 점유
    public void ValidateDeviceSession(string uid, Action<bool> onComplete)
    {
        // userId , deviceRef 초기화
        userId = uid;
        deviceRef = rootRef.Child(DatabaseSchema.USERS).Child(userId).Child(DatabaseSchema.DEVICE_ID);

        // 현재 deviceId와 DB에 저장된 deviceId 비교 (밀어내기 방지)
        deviceRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted) { onComplete?.Invoke(false); return; }

            string dbDeviceId = task.Result.Value as string;
            if (string.IsNullOrEmpty(dbDeviceId) || dbDeviceId == DeviceId)
            {
                // Start Session : 현재 사용자 ID의 deviceId 필드에 현재 기기 ID 저장 (밀어내기 방지)
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
                onKicked?.Invoke();
            }
        };
        deviceRef.ValueChanged += sessionHandler;
        // 앱 종료 시 세션 정보 자동 제거 설정 (선택 사항)
        deviceRef.OnDisconnect().RemoveValue(); 
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
    public void ClearSession(Action onComplete)
    {
        deviceRef.SetValueAsync(null).ContinueWithOnMainThread(_ => onComplete?.Invoke());
        StopMonitoring();
    }
}
