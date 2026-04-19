using UnityEngine;

/// <summary>
/// Centralized Firebase Realtime Database endpoint config.
/// Used by both FirebaseSender (clients) and FirebaseReceiver (big screen).
/// 集中管理 Firebase RTDB 的 URL，发送端和接收端都引用这里。
/// </summary>
[CreateAssetMenu(fileName = "FirebaseConfig", menuName = "Museum Kitchen/Firebase Config")]
public class FirebaseConfig : ScriptableObject
{
    [Tooltip("Realtime Database URL, no trailing slash. Example: https://silk-road-potluck-default-rtdb.firebaseio.com\nRealtime Database 的根 URL，末尾不要加 /。")]
    public string databaseUrl = "https://silk-road-potluck-default-rtdb.firebaseio.com";

    [Tooltip("Path under the database root where dish submissions live.\n菜品提交存放的子路径。")]
    public string potluckPath = "potluck";

    public string PotluckEndpoint => $"{databaseUrl.TrimEnd('/')}/{potluckPath}.json";

    /// <summary>Endpoint for fetching the most recent N entries, ordered by key (Firebase auto-id is time-sortable).</summary>
    public string PotluckRecentEndpoint(int limit) =>
        $"{PotluckEndpoint}?orderBy=\"$key\"&limitToLast={limit}";
}
