using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MyUtility.Checks;
using System.Collections;

public class EnergyBar : MonoBehaviour
{
    [SerializeField] private Sprite nonFilledSprite;
    [SerializeField] private Sprite filledSprite;
    [SerializeField] private List<Image> energyUI; 
    [SerializeField, Range(5, 30)] private byte activePowerupDuration = 20;
    [SerializeField, Range(0, 10)] private byte decreaseDelay = 3;
    [SerializeField, Range(2, 8)] private byte successfullGlitchBoost = 5;
    public static bool powerupIsWaiting; 

    private int timer;
    private int powerupTimer;
    private bool successfullGlitch = false;

    private byte energyIndex;

    [Header("-- DEBUG --")]
    public bool simulateEnergy;
    public bool addEnergy; 
    [SerializeField, Range(1, 5)] private byte energyAmount = 1;

    private const byte FIXED_FRAME = 50;
    private bool setBackToUnfilled; 

    void OnEnable()
    {
        // on looting an item, the bar amount is increased
        InventorySystem.OnCollectingEnergy += UpdateEnergyBar;
        ProcessPlayerInputs.OnUsingActivePower += ResetBarValueAfterBerserkMode;
        Projectile.OnSuccessfulGlitch += ApplySuccessfullGlitchReward;
    }

    private void FixedUpdate()
    {
        // decrease in normal mode
        if (!powerupIsWaiting) 
        {
            if (!setBackToUnfilled)
            {
                for (byte i = 0; i < energyUI.Count; i++)
                    energyUI[i].sprite = filledSprite;

                setBackToUnfilled = true; 
            }

            DecreaseGeneralTimer(); 
        }

        if (addEnergy)
        {
            UpdateEnergyBar(energyAmount);
            addEnergy = false; 
        }
    } 

    // change image to yellow when energy bar is full 
    public void UpdateEnergyBar(int amount)
    {
        // logic and basic values
        byte ceil = energyIndex;
        for (byte i = energyIndex; i < amount + ceil && i < energyUI.Count; i = energyIndex)
        {
            energyUI[energyIndex].gameObject.SetActive(true); 
            energyUI[energyIndex].color = new Vector4(1f, 1f, 1f, 1f);

            if (energyIndex < energyUI.Count)
                energyIndex++;
        }

        // if the amount hits max value
        if (energyIndex == energyUI.Count && !powerupIsWaiting)
        {
            // bool to avoid stacking invokes
            setBackToUnfilled = false;
            powerupIsWaiting = true;

            for (byte i = 0; i < energyUI.Count; i++)
                energyUI[i].sprite = filledSprite;

            InvokeRepeating(nameof(UpdateActivePowerupState), 0f, Time.fixedDeltaTime);
            return;
        }

        successfullGlitch = false;
        timer = 0;
    } // DONE 5.0

    void DecreaseGeneralTimer()
    {
        timer += 1; 
        timer = (int)Mathf.Repeat(timer, decreaseDelay * FIXED_FRAME); 

        if (ValueCheck.IsBetweenMinAndMax(timer, (decreaseDelay * FIXED_FRAME) - 2, (decreaseDelay * FIXED_FRAME)))
        {
            if (energyIndex > 0)
                energyIndex--;

            energyUI[energyIndex].color = new Vector4(1f, 1f, 1f, 0f);
            energyUI[energyIndex].gameObject.SetActive(false); 

            CancelInvoke(nameof(DecreaseGeneralTimer)); 
        }
    } // DONE 3.0

    void UpdateActivePowerupState()
    {
        powerupTimer += 1; 
        powerupTimer = (int)Mathf.Repeat(powerupTimer, activePowerupDuration * FIXED_FRAME);

        if (ValueCheck.IsBetweenMinAndMax(powerupTimer, (activePowerupDuration * FIXED_FRAME) - 2, (activePowerupDuration * FIXED_FRAME))) 
        {
            powerupIsWaiting = false;
            Debug.Log("ACTIVE POWERUP IS DOWN");

            for (int i = energyUI.Count - 1; i >= 0; i--)
            {
                energyUI[i].color = new Vector4(1f, 1f, 1f, 0f);
                energyUI[i].sprite = nonFilledSprite;
            }

            energyIndex = 0;
            powerupTimer = 0; 
            CancelInvoke(nameof(UpdateActivePowerupState));
        }
    } // DONE 3.0

    public void ResetBarValueAfterBerserkMode() 
    {
        powerupTimer = (activePowerupDuration * FIXED_FRAME) -2;
        UpdateActivePowerupState();

    } // DONE 3.0 

    void ApplySuccessfullGlitchReward() 
    {
        successfullGlitch = true;
        UpdateEnergyBar(successfullGlitchBoost);
    } // DONE 2.0

    private void OnDisable()
    {
        InventorySystem.OnCollectingEnergy -= UpdateEnergyBar;
        ProcessPlayerInputs.OnUsingActivePower -= ResetBarValueAfterBerserkMode;
        Projectile.OnSuccessfulGlitch -= ApplySuccessfullGlitchReward;
    }
}
