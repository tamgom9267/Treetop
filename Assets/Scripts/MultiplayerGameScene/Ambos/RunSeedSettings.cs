using UnityEngine;

/// <summary>Seleccion temporal: no escribe archivos ni depende de Photon.</summary>
public static class RunSeedSettings
{
    public const int MinSeed = 0;
    public const int MaxSeed = 99999;
    private static int soloSeed;
    private static bool hasSoloSeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        hasSoloSeed = false;
    }

    public static bool IsValid(int seed) => seed >= MinSeed && seed <= MaxSeed;

    public static bool TryParse(string text, out int seed)
    {
        seed = 0;
        if (string.IsNullOrEmpty(text) || text.Length > 5) return false;
        foreach (char digit in text)
        {
            if (digit < '0' || digit > '9') return false;
            seed = seed * 10 + digit - '0';
        }
        return IsValid(seed);
    }

    public static void SelectSoloSeed(int seed)
    {
        if (!IsValid(seed)) throw new System.ArgumentOutOfRangeException(nameof(seed));
        soloSeed = seed;
        hasSoloSeed = true;
    }

    public static bool TryConsumeSoloSeed(out int seed)
    {
        seed = soloSeed;
        bool selected = hasSoloSeed;
        hasSoloSeed = false;
        return selected;
    }
}
