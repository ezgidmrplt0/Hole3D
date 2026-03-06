using UnityEngine;

public static class AndroidVibration
{
#if UNITY_ANDROID && !UNITY_EDITOR
    public static void Vibrate(long milliseconds)
    {
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (vibrator != null)
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
    }
#else
    public static void Vibrate(long milliseconds)
    {
        Handheld.Vibrate();
    }
#endif
}
