using UnityEngine;

public class AndroidDeviceFeatureDetection : MonoBehaviour
{
    public int maxFramesPerSecond = 60;
    public bool hapticsSupported;
    public bool multiTouchSupported;
    public bool bluetoothAudioDetected;

    private void Start()
    {
        //DetectHapticsSupport();
        DetectMultiTouchSupport();
        DetectBluetoothAudio();
        DetectMaxFramerate();
    }

//    private void DetectHapticsSupport()
//    {
//#if UNITY_ANDROID && !UNITY_EDITOR
//        try
//        {
//            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
//            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
//            var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

//            if (vibrator != null)
//            {
//                // hasVibrator() exists on most Android API levels
//                hapticsSupported = vibrator.Call<bool>("hasVibrator");

//                // Optional: check for amplitude control (API 26+)
//                var version = new AndroidJavaClass("android.os.Build$VERSION");
//                int sdkInt = version.GetStatic<int>("SDK_INT");
//                if (sdkInt >= 26)
//                {
//                    try
//                    {
//                        bool hasAmp = vibrator.Call<bool>("hasAmplitudeControl");
//                        Debug.Log("Has amplitude control: " + hasAmp);
//                    }
//                    catch (Exception) { /* method may not exist on some OEMs */ }
//                }
//            }
//            else
//            {
//                hapticsSupported = false;
//            }
//        }
//        catch (Exception e)
//        {
//            Debug.LogWarning("Error checking vibrator: " + e);
//            hapticsSupported = false;
//        }
//#else
//        // Editor, iOS or other platforms - fallback
//        hapticsSupported = false;
//#endif

//        Debug.Log("Haptics Supported: " + hapticsSupported);
//    }

    private void DetectMultiTouchSupport()
    {
        // Check if the device supports multi-touch
        multiTouchSupported = Input.multiTouchEnabled;
        Debug.Log("Multi-Touch Supported: " + multiTouchSupported);
    }

    private void DetectBluetoothAudio()
    {
        // Placeholder for Bluetooth audio detection logic
        // Actual implementation may require platform-specific plugins
        bluetoothAudioDetected = false; // Default to false
        Debug.Log("Bluetooth Audio Detected: " + bluetoothAudioDetected);
    }

    private void DetectMaxFramerate()
    {
        int detectedFps = 60; // sensible default

        try
        {
            // Preferred: Unity API
            int unityRefresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value); // cast double to float
            if (unityRefresh > 0)
            {
                detectedFps = unityRefresh;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
        else
        {
            // Fallback: query Android Display.getRefreshRate() which returns a float
            try
            {
                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var windowManager = activity.Call<AndroidJavaObject>("getSystemService", "window");
                var display = windowManager?.Call<AndroidJavaObject>("getDefaultDisplay");
                if (display != null)
                {
                    float refresh = display.Call<float>("getRefreshRate");
                    if (refresh > 0f) detectedFps = Mathf.RoundToInt(refresh);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Android refresh rate query failed: " + e);
            }
        }
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Error detecting refresh rate: " + e);
        }

        maxFramesPerSecond = detectedFps;
        Debug.Log("Max Frames Per Second detected: " + maxFramesPerSecond);
    }

    public void SetMaxFramerate(int fps)
    {
        if (fps <= 0)
        {
            // Use platform default
            QualitySettings.vSyncCount = 1; // enable vSync to follow display
            Application.targetFrameRate = -1;
            Debug.Log("Using platform default framerate (vSync enabled).");
            return;
        }

        // Allow Application.targetFrameRate to work
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;
        Debug.Log("TargetFrameRate set to: " + fps);
    }
}
