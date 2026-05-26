using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Input source driven by BCI selection events from the g.tec ERP pipeline.
/// Wire ERPPipeline.OnClassSelection (Inspector) -> BCIInputSource.OnBCISelection.
/// Falls back to keyboard for shake/serve so you can still test without the headset.
/// </summary>
public class BCIInputSource : MonoBehaviour, IInputSource
{
    [Header("Mapping (BCI Class ID -> Bottle index 1..6)")]
    [Tooltip("If empty, identity mapping is used: class 1 -> bottle 1, class 2 -> bottle 2, etc.")]
    public int[] classIdToBottle = new int[] { 1, 2, 3, 4, 5, 6 };

    [Header("Selection Behavior")]
    [Tooltip("Ignore repeated BCI selections within this many seconds.")]
    public float minSelectionIntervalSeconds = 0.6f;
    [Tooltip("Also accept keyboard 1..6 for testing without headset.")]
    public bool allowKeyboardFallback = true;

    [Header("Debug")]
    public bool verboseLogging = true;

    private readonly Queue<int> pendingBottlePresses = new Queue<int>();
    private float lastSelectionTime = -999f;
    private BottleFlashController flashController;

    private void Awake()
    {
        flashController = FindObjectOfType<BottleFlashController>();
    }

    /// <summary>Wire this to ERPPipeline.OnClassSelection in the Inspector.</summary>
    public void OnBCISelection(int classId)
    {
        if (Time.time - lastSelectionTime < minSelectionIntervalSeconds)
        {
            if (verboseLogging) Debug.Log($"[BCI] Ignored class {classId} (cooldown).");
            return;
        }

        int bottle = MapClassToBottle(classId);
        if (bottle < 1 || bottle > 6)
        {
            if (verboseLogging) Debug.Log($"[BCI] Class {classId} did not map to a bottle.");
            return;
        }

        lastSelectionTime = Time.time;
        pendingBottlePresses.Enqueue(bottle);

        if (flashController != null)
            flashController.FlashOnly(bottle);

        if (verboseLogging) Debug.Log($"[BCI] Class {classId} -> bottle {bottle}");
    }

    /// <summary>Same hook but accepts uint (some BCI events use uint).</summary>
    public void OnBCISelectionUInt(uint classId) => OnBCISelection((int)classId);

    private int MapClassToBottle(int classId)
    {
        if (classIdToBottle != null && classId >= 1 && classId - 1 < classIdToBottle.Length)
            return classIdToBottle[classId - 1];
        return classId;
    }

    public bool GetIngredientPressed(int ingredientIndex)
    {
        if (pendingBottlePresses.Count > 0 && pendingBottlePresses.Peek() == ingredientIndex)
        {
            pendingBottlePresses.Dequeue();
            return true;
        }

        if (allowKeyboardFallback)
        {
            switch (ingredientIndex)
            {
                case 1: return Input.GetKeyDown(KeyCode.Alpha1);
                case 2: return Input.GetKeyDown(KeyCode.Alpha2);
                case 3: return Input.GetKeyDown(KeyCode.Alpha3);
                case 4: return Input.GetKeyDown(KeyCode.Alpha4);
                case 5: return Input.GetKeyDown(KeyCode.Alpha5);
                case 6: return Input.GetKeyDown(KeyCode.Alpha6);
            }
        }
        return false;
    }

    public bool GetShakePressed()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    public bool GetServePressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }
}
