using System.Collections.Generic;
using UnityEngine;

public class BottleFlashController : MonoBehaviour
{
    [Header("Assign in order: Bottle 1..6 (auto-found if empty)")]
    public SpriteRenderer[] overlays;

    [Header("Sprites (auto-loaded from Resources/flashobjects)")]
    public Sprite[] darkSprites;
    public Sprite[] flashSprites;

    [Header("SSVEP Frequencies (Hz) per bottle 1..6")]
    [Tooltip("Each bottle flickers continuously at its own frequency.")]
    public float[] frequenciesHz = new float[] { 6f, 7.5f, 8.57f, 10f, 12f, 15f };

    [Header("Mode")]
    [Tooltip("If true, all bottles flicker continuously (SSVEP). If false, only selected bottle flashes.")]
    public bool continuousFlicker = true;
    [Tooltip("If true, all overlays use the same sprite pair (default: bottle 1's). Color does not affect BCI selection.")]
    public bool useUniformSprite = true;
    [Tooltip("Which sprite index (1..6) to use for all overlays when Use Uniform Sprite is on.")]
    [Range(1, 6)] public int uniformSpriteIndex = 1;

    [Header("Selection Highlight (only when continuousFlicker=false)")]
    public float flashOnSeconds = 0.18f;
    public float flashOffSeconds = 0.18f;
    public int flashesPerSelection = 5;

    [Header("Debug")]
    public bool verboseLogging = true;

    private float[] timers;
    private bool[] isBright;
    private int singleFlashIdx = -1;
    private int singleFlashCount = 0;
    private float singleFlashTimer = 0f;

    private void Awake()
    {
        AutoFindOverlays();
        AutoLoadSprites();
        ResetTimers();
        SetIdleAll();
    }

    private void AutoFindOverlays()
    {
        if (overlays != null && overlays.Length > 0) return;

        List<SpriteRenderer> found = new List<SpriteRenderer>();
        SpriteRenderer[] all = FindObjectsOfType<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "BCIFlashOverlay")
                found.Add(all[i]);
        }
        overlays = found.ToArray();

        if (verboseLogging)
            Debug.Log($"[BottleFlash] Auto-found {overlays.Length} overlays.");
    }

    private void AutoLoadSprites()
    {
        if (darkSprites == null || darkSprites.Length < 6)
        {
            darkSprites = new Sprite[6];
            for (int i = 0; i < 6; i++)
                darkSprites[i] = Resources.Load<Sprite>($"flashobjects/darkobject{i + 1}");
        }
        if (flashSprites == null || flashSprites.Length < 6)
        {
            flashSprites = new Sprite[6];
            for (int i = 0; i < 6; i++)
                flashSprites[i] = Resources.Load<Sprite>($"flashobjects/flashobject{i + 1}");
        }
    }

    private void ResetTimers()
    {
        int n = overlays != null ? overlays.Length : 0;
        timers = new float[n];
        isBright = new bool[n];
    }

    public void SetIdle()
    {
        SetIdleAll();
    }

    private void SetIdleAll()
    {
        if (overlays == null) return;
        for (int i = 0; i < overlays.Length; i++)
        {
            SpriteRenderer ov = overlays[i];
            if (ov == null) continue;

            ov.enabled = true;
            ForceVisible(ov);

            Sprite s = GetDarkSprite(i);
            if (s != null) ov.sprite = s;
        }
    }

    private Sprite GetDarkSprite(int idx)
    {
        int useIdx = useUniformSprite ? Mathf.Clamp(uniformSpriteIndex - 1, 0, 5) : idx;
        if (darkSprites != null && useIdx < darkSprites.Length) return darkSprites[useIdx];
        return null;
    }

    private Sprite GetFlashSprite(int idx)
    {
        int useIdx = useUniformSprite ? Mathf.Clamp(uniformSpriteIndex - 1, 0, 5) : idx;
        if (flashSprites != null && useIdx < flashSprites.Length) return flashSprites[useIdx];
        return null;
    }

    private void Update()
    {
        if (overlays == null) return;

        if (continuousFlicker)
        {
            UpdateSSVEPFlicker();
        }
        else if (singleFlashIdx >= 0)
        {
            UpdateSingleFlash();
        }
    }

    private void UpdateSSVEPFlicker()
    {
        for (int i = 0; i < overlays.Length; i++)
        {
            SpriteRenderer ov = overlays[i];
            if (ov == null) continue;

            float freq = (frequenciesHz != null && i < frequenciesHz.Length) ? frequenciesHz[i] : 10f;
            if (freq <= 0f) continue;

            float halfPeriod = 0.5f / freq;
            timers[i] += Time.deltaTime;
            if (timers[i] >= halfPeriod)
            {
                timers[i] -= halfPeriod;
                isBright[i] = !isBright[i];

                Sprite next = isBright[i] ? GetFlashSprite(i) : GetDarkSprite(i);
                if (next != null)
                    ov.sprite = next;
            }
        }
    }

    private void UpdateSingleFlash()
    {
        if (singleFlashIdx < 0 || singleFlashIdx >= overlays.Length) return;
        SpriteRenderer ov = overlays[singleFlashIdx];
        if (ov == null) return;

        singleFlashTimer += Time.deltaTime;
        bool brightPhase = isBright[singleFlashIdx];
        float duration = brightPhase ? flashOnSeconds : flashOffSeconds;

        if (singleFlashTimer >= duration)
        {
            singleFlashTimer = 0f;
            isBright[singleFlashIdx] = !brightPhase;

            Sprite next = isBright[singleFlashIdx] ? GetFlashSprite(singleFlashIdx) : GetDarkSprite(singleFlashIdx);
            if (next != null)
                ov.sprite = next;

            if (!isBright[singleFlashIdx])
            {
                singleFlashCount++;
                if (singleFlashCount >= flashesPerSelection)
                {
                    singleFlashIdx = -1;
                }
            }
        }
    }

    public void FlashOnly(int bottleIndex1to6)
    {
        if (continuousFlicker)
        {
            if (verboseLogging)
                Debug.Log($"[BottleFlash] Selection registered for bottle {bottleIndex1to6} (SSVEP mode keeps flickering).");
            return;
        }

        int idx = bottleIndex1to6 - 1;
        if (overlays == null || idx < 0 || idx >= overlays.Length || overlays[idx] == null)
        {
            Debug.LogWarning($"[BottleFlash] Cannot flash bottle {bottleIndex1to6} (overlay missing).");
            return;
        }

        singleFlashIdx = idx;
        singleFlashCount = 0;
        singleFlashTimer = 0f;
        isBright[idx] = false;
    }

    private static void ForceVisible(SpriteRenderer sr)
    {
        Color c = sr.color;
        c.a = 1f;
        sr.color = c;
    }
}
