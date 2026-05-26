using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Auto-configures and starts g.tec ERP training when the scene loads.
/// No Inspector setup required.
/// </summary>
public class BCITrainingAutoBootstrap : MonoBehaviour
{
    [Header("Auto Start")]
    public bool autoStartTraining = true;
    public float startupDelaySeconds = 1.5f;

    [Header("ERP Timing")]
    [Tooltip("frequency = 1000 / (OnTimeMs + OffTimeMs)")]
    public int onTimeMs = 100;
    public int offTimeMs = 100;

    [Header("ERP Class/Trial Setup")]
    public int numberOfClasses = 6;
    public int numberOfTrainingTrials = 30;

    [Header("Debug")]
    public bool verboseLogging = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject go = new GameObject("BCITrainingAutoBootstrap");
        go.AddComponent<BCITrainingAutoBootstrap>();
    }

    private void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    private IEnumerator BootstrapRoutine()
    {
        // Give scene/prefabs a moment to initialize.
        yield return new WaitForSeconds(startupDelaySeconds);

        DisableCustomFlicker();

        Component paradigm = FindComponentByFullTypeName("Gtec.UnityInterface.ERPParadigm");
        if (paradigm == null)
        {
            Debug.LogWarning("[BCI-Auto] ERPParadigm not found. Auto-start skipped.");
            yield break;
        }

        ConfigureParadigm(paradigm);

        if (autoStartTraining)
            StartTraining(paradigm);
    }

    private void DisableCustomFlicker()
    {
        BottleFlashController flicker = FindObjectOfType<BottleFlashController>(true);
        if (flicker == null) return;

        FieldInfo continuousField = typeof(BottleFlashController).GetField("continuousFlicker");
        if (continuousField != null)
            continuousField.SetValue(flicker, false);

        if (verboseLogging)
            Debug.Log("[BCI-Auto] Disabled custom continuous flicker.");
    }

    private void ConfigureParadigm(Component paradigm)
    {
        Type t = paradigm.GetType();

        SetFieldIfExists(t, paradigm, "NumberOfClasses", numberOfClasses);
        SetFieldIfExists(t, paradigm, "NumberOfTrainingTrials", numberOfTrainingTrials);
        SetFieldIfExists(t, paradigm, "OnTimeMs", onTimeMs);
        SetFieldIfExists(t, paradigm, "OffTimeMs", offTimeMs);

        if (verboseLogging)
        {
            float freq = 1000f / Mathf.Max(1f, onTimeMs + offTimeMs);
            Debug.Log($"[BCI-Auto] ERP configured: classes={numberOfClasses}, trials={numberOfTrainingTrials}, on={onTimeMs}ms, off={offTimeMs}ms (~{freq:F2} Hz)");
        }
    }

    private void StartTraining(Component paradigm)
    {
        Type paradigmType = paradigm.GetType();
        MethodInfo startMethod = paradigmType.GetMethod("StartParadigm", BindingFlags.Public | BindingFlags.Instance);
        if (startMethod == null)
        {
            Debug.LogWarning("[BCI-Auto] StartParadigm method not found on ERPParadigm.");
            return;
        }

        Type modeType = paradigmType.Assembly.GetType("Gtec.UnityInterface.ParadigmMode");
        if (modeType == null)
        {
            Debug.LogWarning("[BCI-Auto] ParadigmMode type not found.");
            return;
        }

        object trainingMode = Enum.Parse(modeType, "Training");

        try
        {
            startMethod.Invoke(paradigm, new[] { trainingMode });
            if (verboseLogging)
                Debug.Log("[BCI-Auto] ERP training started automatically.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BCI-Auto] Failed to start ERP training automatically: {ex.Message}");
        }
    }

    private static Component FindComponentByFullTypeName(string fullTypeName)
    {
        MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour mb = all[i];
            if (mb == null) continue;
            if (mb.GetType().FullName == fullTypeName)
                return mb;
        }
        return null;
    }

    private static void SetFieldIfExists(Type type, object instance, string fieldName, object value)
    {
        FieldInfo f = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return;

        object converted = value;
        Type targetType = f.FieldType;

        try
        {
            if (targetType == typeof(uint))
                converted = Convert.ToUInt32(value);
            else if (targetType == typeof(int))
                converted = Convert.ToInt32(value);
            else if (targetType == typeof(float))
                converted = Convert.ToSingle(value);
            else if (targetType == typeof(double))
                converted = Convert.ToDouble(value);
            else if (targetType.IsEnum)
                converted = Enum.ToObject(targetType, value);
            else if (targetType != value.GetType())
                converted = Convert.ChangeType(value, targetType);
        }
        catch
        {
            // Keep original value if conversion fails; reflection set below may no-op/fail silently by caller context.
            converted = value;
        }

        f.SetValue(instance, converted);
    }
}
