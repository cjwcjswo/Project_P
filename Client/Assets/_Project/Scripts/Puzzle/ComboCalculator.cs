using System;

public class ComboCalculator
{
    private const float AMPLIFY_COEFFICIENT = 0.2f;

    public int CurrentCombo { get; private set; }

    public event Action<int> OnComboChanged;

    public void Reset()
    {
        CurrentCombo = 0;
        OnComboChanged?.Invoke(0);
    }

    public void IncrementCombo()
    {
        CurrentCombo++;
        OnComboChanged?.Invoke(CurrentCombo);
    }

    /// <summary>
    /// GDD v2.0 공식: 1 + (콤보 - 1) * 0.2
    /// 캡 없음 — 콤보가 높을수록 계속 증폭.
    /// </summary>
    public float GetMultiplier()
    {
        return 1f + (CurrentCombo - 1) * AMPLIFY_COEFFICIENT;
    }

    /// <summary>
    /// 특정 콤보 시점의 배율 계산 (색상별 개별 발동용).
    /// </summary>
    public float GetMultiplierAt(int comboCount)
    {
        return 1f + (comboCount - 1) * AMPLIFY_COEFFICIENT;
    }
}
