using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System; 

public enum DetectorInfos { None, LeftPistol, RightPistol, Cannon, All } // use [Flags] instead of "both"

// Gamefeel => bataille de cowboy (le premier qui dégaine a gagné)
// le boss peut rapidement te tuer, mais tu peux rapidement le tuer si tu maîtrises bien ton perso 
public class BossPlatypusBrain : MonoBehaviour
{
    [SerializeField] private BasicAIBrain brain; 
    [SerializeField, Range(2, 10)] private float combatStateActivationDelay = 4f;
    [SerializeField, Range(3, 10)] private float movementWidht = 6f;
    [SerializeField, Range(0.25f, 5)] private float speed = 0.75f;

    [Header("Generic Combat")]
    [SerializeField, Range(0, 1)] private float pistolShootProbability = 0.9f;
    [SerializeField, Range(0, 1)] private float cannonShootProbability = 0.5f;
    [SerializeField, Range(1, 8)] private float shootDelay = 2f;
    [SerializeField, Range(5, 20)] private float invocationDelay = 10f;

    [Header("Phases")]
    [SerializeField, Range(0, 1)] private float invocationProbability_Phase1 = 0.5f;
    [SerializeField, Range(0, 1)] private float invocationProbability_Phase2 = 0.7f;
    [SerializeField, Space] private List<Transform> invocationSpawnPoints_Phase1;
    [SerializeField] private List<Transform> invocationSpawnPoints_Phase2;
    [SerializeField] private List<GameObject> ads_Phase1;
    [SerializeField, Space] private GameObject ads_Phase2;
    public DetectorInfos weaponDetectorInfo;

    public List<Behaviour> componentsToActivateOnPhase1;
    public List<GameObject> componentsToActivateOnPhase2;
    public List<Behaviour> componentsToDeactivateOnDeath;

    private float m_invocationSelector;
    private int m_platypusToInvokeSelector;

    private Animator m_animator;
    private bool m_canInvoke;

    public static bool s_dead = false;
    private bool m_behaviourDisabled = false;
    public static bool s_isInCombatState = false;

    [Header("Detection")]
    public TargetDetection pistolDetector_Left;
    public TargetDetection pistolDetector_Right;
    public TargetDetection cannonDetector;
    public AIBasicAnimator bossAnimator;
    public GameObject pistolFirePoint_Right;
    public GameObject pistolFirePoint_Left; 
    public GameObject cannonFirePoint;
    private Room roomInfos;

    [Header("UI")]
    public GameObject healthObj;
    public Health healthData;
    public Slider healthUI;
    public Image BossUI_BkgImage; 
    public Sprite bossDeathSprite;
    public GameObject[] adsSpawnVFXWarning = new GameObject[8];
    private float prewarningFeedbackDuration; 


    private bool m_canShoot = true;
    public static bool s_hasEnteredPhase1;
    public static bool s_hasEnteredPhase2;

    public static Action OnChannellingAttack;
    public static Action OnCastingPistolAttack;
    public static Action OnCastingPistolAttack_RightArm;
    public static Action OnCastingCanonAttack; 

    public static Action OnBossAwakening;
    public static Action OnInvokingPlatypus;

    public static Action OnLaughing;
    public static Action<Transform> OnSendingFirepoint; 

    [Header("Code")]
    [SerializeField] GameObject code;
    private float timer; 


    private void OnEnable()
    {
        // during spawn animation, the bar slightly increases to normal size with a badass sound 

        Health.OnBossLoseHP += DecreaseBossUI;
        Health.OnEnteringPhase2 += ActivateState_Phase2;
        Health.OnBossDeath += SetBossDeathState; 
    }

    private void Start()
    {
        // you need an average 250 cash per game 
        // boss should give around 200 
        // droped cash should give 10 
        prewarningFeedbackDuration = PlayerData.s_Difficulty == Difficulty.Easy ? 2f : (PlayerData.s_Difficulty == Difficulty.Normal ? 1.5f : 0.75f); 
        m_animator = GetComponent<Animator>();
        roomInfos = GetComponentInParent<Room>(); 
        healthUI.value = healthUI.maxValue = healthData.MaxHP;
        healthObj.SetActive(false);

        foreach (var item in componentsToActivateOnPhase1)
            item.enabled = false;

        foreach (var item in componentsToActivateOnPhase2)
            item.SetActive(false); 
    }

    void FixedUpdate()
    {
        // second phase = shoot with right arm too
        // third phase = shoot with canon

        // I really need to improve with decision-making algorithms (trees) because too much if/else (bad for performance and too much "robot like") .. 
        healthObj.SetActive(brain.CanAttack);
        if (!s_dead && roomInfos.PlayerIsInsideRoom && !Health.s_PlayerIsDead && !UpdateUI.s_playerHasWon) 
        {          
            if (!s_hasEnteredPhase1)
                StartCoroutine(ActivateState_Phase1(combatStateActivationDelay));
            
            if (s_isInCombatState)
            {
                // set detector infos
                if (!s_hasEnteredPhase2)
                {
                    if (pistolDetector_Left.TargetIsDetected)
                    {
                        weaponDetectorInfo = DetectorInfos.LeftPistol;
                    }
                    else if (pistolDetector_Right.TargetIsDetected)
                    {
                        weaponDetectorInfo = DetectorInfos.RightPistol;
                    }
                    else
                    {
                        weaponDetectorInfo = DetectorInfos.None;
                    }
                }
                else
                {
                    if (cannonDetector.TargetIsDetected)
                    {
                        weaponDetectorInfo = DetectorInfos.Cannon;
                    }

                    if ((pistolDetector_Left.TargetIsDetected || pistolDetector_Right.TargetIsDetected) && cannonDetector.TargetIsDetected)
                    {
                        weaponDetectorInfo = DetectorInfos.All;
                    }

                    if (!pistolDetector_Left.TargetIsDetected && !pistolDetector_Right.TargetIsDetected && !cannonDetector.TargetIsDetected)
                    {
                        weaponDetectorInfo = DetectorInfos.None;
                    }
                }

                if (m_canShoot)
                {
                    float selector = UnityEngine.Random.Range(0f, 1f);
                    if (!s_hasEnteredPhase2 && (weaponDetectorInfo == DetectorInfos.LeftPistol || weaponDetectorInfo == DetectorInfos.RightPistol))
                    {
                        if (selector <= pistolShootProbability)
                        {
                            Debug.Log("calling basic attack");
                            StartCoroutine(PistolAttack(weaponDetectorInfo == DetectorInfos.LeftPistol)); 
                            StartCoroutine(CoolDown(shootDelay));
                        }
                        else
                        {
                            StartCoroutine(CoolDown(shootDelay * 0.5f));
                        }
                    }
                    else if (s_hasEnteredPhase2)
                    {
                        if (selector <= pistolShootProbability && (weaponDetectorInfo == DetectorInfos.LeftPistol || weaponDetectorInfo == DetectorInfos.RightPistol))
                        {
                            Debug.Log("calling basic attack");
                            StartCoroutine(PistolAttack(weaponDetectorInfo == DetectorInfos.LeftPistol));
                            StartCoroutine(CoolDown(shootDelay));
                        }
                        else if (selector <= cannonShootProbability && weaponDetectorInfo == DetectorInfos.Cannon)
                        {
                            Debug.Log("calling cannon attack");
                            StartCoroutine(bossAnimator.BossCanonAttack());
                            StartCoroutine(CoolDown(shootDelay));
                        }
                        else
                        {
                            StartCoroutine(CoolDown(shootDelay * 0.5f));
                        }
                    }
                    else if (weaponDetectorInfo == DetectorInfos.All && s_hasEnteredPhase2)
                    {
                        if (selector <= pistolShootProbability * 0.5f)
                        {
                            StartCoroutine(PistolAttack(weaponDetectorInfo == DetectorInfos.LeftPistol));
                            StartCoroutine(CoolDown(shootDelay));
                        }
                        else if (selector > pistolShootProbability * 0.5f)
                        {
                            StartCoroutine(bossAnimator.BossCanonAttack());
                            StartCoroutine(CoolDown(shootDelay));
                        }
                        else
                        {
                            StartCoroutine(CoolDown(shootDelay * 0.5f));
                        }
                    }                                  
                }

                if (!m_animator.GetCurrentAnimatorStateInfo(0).IsName("Boss atk 1")
                    && !m_animator.GetCurrentAnimatorStateInfo(0).IsName("Boss atk 2")
                    && !m_animator.GetCurrentAnimatorStateInfo(0).IsName("Boss idle")
                    && !m_animator.GetCurrentAnimatorStateInfo(0).IsName("Boss Speatk")
                    && !m_animator.GetCurrentAnimatorStateInfo(0).IsName("Boss dying"))
                {
                    m_animator.Play("Boss idle");
                }

                if (!s_hasEnteredPhase2)
                {
                    if (weaponDetectorInfo == DetectorInfos.LeftPistol)
                        Movement(pistolFirePoint_Left.transform.position.x); 
                    else if (weaponDetectorInfo == DetectorInfos.RightPistol) 
                        Movement(pistolFirePoint_Right.transform.position.x); 
                    else 
                        Movement(pistolFirePoint_Left.transform.position.x);
                }
                else if (weaponDetectorInfo == DetectorInfos.None) 
                {
                    Movement(cannonFirePoint.transform.position.x); // canon must not have always priority over pistol 
                                                                    // => randomise it 
                }

            }
                
            if (m_canInvoke)
                TryInvokePlatypus(invocationProbability_Phase1); 
        }
    }

    IEnumerator ActivateState_Phase1(float delay)
    {
        s_hasEnteredPhase1 = true; 

        yield return new WaitForSeconds(delay);
        OnBossAwakening();
        s_isInCombatState  = true;

        yield return new WaitForSeconds(2f);
        foreach (var item in componentsToActivateOnPhase1)
        {
            item.enabled = true;
        }

        StartCoroutine(InvocationCooldown(invocationDelay));
    }

    private void ActivateState_Phase2() 
    { 
        // keep reference of them so that they are destroyed when boss dies
        // the player can thus choose to ignore them and focus the boss 
        foreach (var item in componentsToActivateOnPhase2)
            item.SetActive(true);

        for (int i = 0; i < invocationSpawnPoints_Phase2.Count; i++)
        {
            float selector = UnityEngine.Random.Range(0f, 1f); 

            if (selector <= invocationProbability_Phase2)
            {
                GameObject spawnedRerence = Instantiate(ads_Phase2, invocationSpawnPoints_Phase2[i].position, Quaternion.identity);
                BasicAIBrain brainReference = spawnedRerence.GetComponent<BasicAIBrain>();
                brainReference.enemyReactionTime = 1f;
                brainReference.StartCoroutine(brainReference.SetEntityState(true, true)); 
            }

        }

        s_hasEnteredPhase2 = true;
        speed *= 1.75f;  
    } 

    // version 1.0
    private void Movement(float referencePoint)
    {
        transform.Translate(new Vector2((PlayerMovement.s_selfPosition.x - referencePoint), 0f).normalized * Time.fixedDeltaTime * speed);
    } 

    public void SendLaughEvent()
    {
        OnLaughing(); 
    }

    // version 1.0
    private void TryInvokePlatypus(float invocationProbability)
    {
        // try invoking for each spawn point
        for (int i = 0; i < invocationSpawnPoints_Phase1.Count; i++)
        {
            // try invoking
            m_invocationSelector = UnityEngine.Random.Range(0f, 1f);

            if (m_invocationSelector <= invocationProbability)
            {
                // if invocation, select entity
                m_platypusToInvokeSelector = UnityEngine.Random.Range(0, ads_Phase1.Count);
                StartCoroutine(SwitchVFXOnAndOff((byte)i)); 
            }
        }

        // why not increase invocation probability if it failed 
        StartCoroutine(InvocationCooldown(invocationDelay)); 
    }

    private IEnumerator SwitchVFXOnAndOff(byte index)
    {
        adsSpawnVFXWarning[index].SetActive(true);
        adsSpawnVFXWarning[index].GetComponent<Animator>().enabled = true;

        yield return new WaitForSeconds(prewarningFeedbackDuration);
        adsSpawnVFXWarning[index].SetActive(false);
        adsSpawnVFXWarning[index].GetComponent<Animator>().enabled = false; 

        // spawn
        GameObject spawnedRerence = Instantiate(ads_Phase1[m_platypusToInvokeSelector], invocationSpawnPoints_Phase1[index].position, Quaternion.identity);
        BasicAIBrain brainReference = spawnedRerence.GetComponent<BasicAIBrain>();
        brainReference.enemyReactionTime = 0.25f;
        brainReference.StartCoroutine(brainReference.SetEntityState(true, true));
        OnInvokingPlatypus();
    }

    public void SpawnBonusCode()
    {
        code.SetActive(true);
        InvokeRepeating(nameof(TranslateToPoint), 0.5f, Time.fixedDeltaTime); 
    }

    private void TranslateToPoint()
    {
        timer += Time.fixedDeltaTime;
        code.transform.Translate(Vector2.down.normalized * Time.fixedDeltaTime, Space.Self);

        if (timer > 5f)
            CancelInvoke(nameof(TranslateToPoint)); 
    }

    private void DecreaseBossUI(float amount)
    {
        healthUI.value -= amount; 
    }

    private void SetBossDeathState()
    {
        healthUI.value = 0f;
        BossUI_BkgImage.sprite = bossDeathSprite; 

        foreach (var item in componentsToDeactivateOnDeath)
        {
            item.enabled = false;
            m_behaviourDisabled = true;
        }
    }

    IEnumerator InvocationCooldown(float delay)
    {
        m_canInvoke = false; 

        yield return new WaitForSeconds(delay);
        m_canInvoke = true; 
    }

    IEnumerator PistolAttack(bool fromLeft) 
    {
        yield return new WaitForFixedUpdate();
        Transform transfToSend = fromLeft ? pistolFirePoint_Left.transform : pistolFirePoint_Right.transform;
        OnSendingFirepoint(transfToSend);
        OnChannellingAttack();
        bossAnimator.BossAttackPistol(fromLeft);
    }

    public void SendPistolCastEvent()
    {
        OnCastingPistolAttack();
    } 

    public void SendCanonCastEvent()
    {
        OnCastingCanonAttack(); 
    }

    IEnumerator CoolDown(float delay)
    {
        m_canShoot = false;
        yield return new WaitForSeconds(delay);
        m_canShoot = true;
    }

    private void OnDisable()
    {
        Health.OnBossLoseHP -= DecreaseBossUI;
        Health.OnEnteringPhase2 -= ActivateState_Phase2;
        Health.OnBossDeath -= SetBossDeathState;
    }
}
