using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using MyUtility;

// only load a procedure (room prefabs layout)
// doors, enemies and confiners are spawned locally by each prefab
public class LevelGenerator : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Vector2[] playerStartingPositionsArray;
    [SerializeField] PlayerData playerData;
    [SerializeField] private GameObject player;
    [SerializeField] private Vector2[] levelMacroLayoutArray = new Vector2[16];
    [SerializeField, Range(1, 5)] private byte xDistances = 2;
    [SerializeField, Range(1, 5)] private byte yDistances = 2;
    [SerializeField] private GameObject inGameTarget; 

    public static GameObject PlayerInstance { get; set; }
    public static Vector3 s_PlayerPos { get; set; }

    [Header("Level")]
    public CameraBasicController camController;
    private byte startingConfinerIndex;
    private byte[] startingMacroArray = new byte[3] { 0, 10, 5 }; // position of EntryRoom

    private GameObject[] macroLayoutsObjectArray;
    public static byte s_amountToGenerate = 16;
    [Range(1, 5)] public byte maxRewardRoomsAmount = 2; // REMOVE

    private byte selector;
    private readonly byte procedureSelectorMin = 1;
    private readonly byte procedureSelectorMax = 3;

    private Collider2D[] confinerArray; // DEBUG 
    public static List<Collider2D> s_ConfinersList;  

    public static Action<Collider2D> OnGenerationComplete_CameraCollider2D; // => CameraBasicController
    public static Action<Bounds> OnGenerationComplete_PlayerBounds; 
    public static Action OnSettingFinalRun;

    public static int s_TotalAmountOfEnemies { get; set; }

    private bool listenersAreNotified = false;
     
    [Header("-- DEBUG --")]
    public bool allowSystemExceptions = true; 
    public static bool LogException { get; set; }

    public static bool s_FinalRun;

    private GameObject macroLayout_reference;
    public bool forceMacroLayout;
    [Range(0, 2)] public byte forcedIndex;

    private bool showGoldenPath = true; 
    public static Action<bool> OnSettingGoldenPath;
    private bool canUseGoldenPath = true;

    public bool showLevelMacroState = true; 
    public static bool s_ShowLevelMacroState; 

    private void OnEnable()
    {
        EnemiesGeneration.OnEnemiesGeneration += SetAmountOfEnemies;
        CameraBasicController.OnNullReferenceException += NullRefNotificationSafeguard; 
        Room.OnBeingCreated += UpdateConfinerList;  
        Health.OnBossDeath += IncreaseBossKills;
        Shop.OnInteractingWithShop_TargetIsVisible += SetTargetVisibility; 
    }

    void Awake()
    {
        s_FinalRun = playerData.currentRunCounter == NewRun.s_amountOfRunsToWin - 1;  

        // chose a macro layout
        macroLayoutsObjectArray = Resources.LoadAll<GameObject>("Level/Macro"); 
        selector = forceMacroLayout ? forcedIndex : (byte)UnityEngine.Random.Range(0, macroLayoutsObjectArray.Length);
        macroLayout_reference = Instantiate(macroLayoutsObjectArray[selector], Vector3.zero, Quaternion.identity, transform);

        // not on awake to avoid boundless for weapon 
        PlayerInstance = Instantiate(player, playerStartingPositionsArray[selector], Quaternion.identity);
        playerData.currentRunCounter++;
        startingConfinerIndex = startingMacroArray[selector];

        s_ShowLevelMacroState = showLevelMacroState; 
    }   

    private void Start()
    { 
        s_ConfinersList = new List<Collider2D>();
        LogException = allowSystemExceptions;
        Cursor.visible = false; 

        // enable each room
        StartCoroutine(GenerateMacroLayout());
    }

    private IEnumerator GenerateMacroLayout()
    {
        yield return new WaitForSeconds(0.05f); 
        for (int i = 0; i < levelMacroLayoutArray.Length; i++) 
        {
            // super performant. You should get hired by Insomniac Games 
            yield return new WaitForSeconds(0.05f); 
            macroLayout_reference.transform.GetChild(i).gameObject.SetActive(true);
            macroLayout_reference.transform.GetChild(i).GetComponent<Room>().PositionInArray = (byte)i;  // used as parameter for Action (confiner)
        }

        yield return new WaitForSeconds(0.05f); 
        OnGenerationComplete_CameraCollider2D(s_ConfinersList[startingConfinerIndex]); // Notify camera for starting confiner 
        Room startingRoom = macroLayout_reference.transform.GetChild(startingMacroArray[selector]).GetComponent<Room>();
        OnGenerationComplete_PlayerBounds(startingRoom.selfConfiner_interior.bounds); 
        listenersAreNotified = true;

        // DEBUG
        confinerArray = new Collider2D[s_ConfinersList.Count]; 
        s_ConfinersList.CopyTo(confinerArray, 0); 
    } 

    private void Update()
    {
        try
        {
            s_PlayerPos = PlayerInstance.transform.position;
        } catch (NullReferenceException nullRefEx) when (Utility.LogException(nullRefEx, gameObject.name)) { }

        // helper for jury
        if (Input.GetKeyDown(KeyCode.G) && canUseGoldenPath && Time.time > 2f)
        {
            OnSettingGoldenPath(showGoldenPath); 
            showGoldenPath = !showGoldenPath;
            StartCoroutine(GoldenPathCooldown()); 
        } 
    } 

    private void SetTargetVisibility(bool isVisible)
    {
        inGameTarget.SetActive(isVisible); 
    }
    
    IEnumerator GoldenPathCooldown()
    {
        canUseGoldenPath = false;
        yield return new WaitForSeconds(2f);
        canUseGoldenPath = true; 
    }

    IEnumerator CallProcedure()
    {
        yield return null;
        SendTransform();

        selector = (byte)UnityEngine.Random.Range(procedureSelectorMin, procedureSelectorMax);

        Debug.Log($"procedure selector is {selector}");

        if (selector == 1)
        {
            HorizontalProcedure1 procedure = gameObject.AddComponent<HorizontalProcedure1>();
            procedure.StartProcedure();

            Debug.Log($"total amount of rooms generated is {procedure.StartProcedure().Count}");
        }
        else
        {
            VerticalProcedure1 procedure = gameObject.AddComponent<VerticalProcedure1>();
            procedure.StartProcedure();

            Debug.Log($"total amount of rooms generated is {procedure.StartProcedure().Count}");
        }

        if (playerData.currentRunCounter == NewRun.s_amountOfRunsToWin)
            OnSettingFinalRun(); 
    }

    void UpdateConfinerList(Collider2D colliderToAdd)
    {
        s_ConfinersList.Add(colliderToAdd); 
    }

    public Transform SendTransform() => transform; // to set this gameobject as parent

    void NullRefNotificationSafeguard(string notifier)
    {
        if (notifier == "MainCamera") // still to be improved and abstracted
        {
            camController.confiner.m_BoundingShape2D = s_ConfinersList[0];
        }
    }

    // for the scoring mechanic
    void SetAmountOfEnemies(int amountToAdd) 
    {
        s_TotalAmountOfEnemies += amountToAdd;
    }

    public void IncreaseBossKills()
    {
        playerData.bossKills++;
        PlayerData.s_hasKilledFinalBoss = playerData.bossKills == 5; // hardcoded amount of runs before final run (stronger boss but gives all missing codes); 
    }

    private void OnDisable()
    {
        EnemiesGeneration.OnEnemiesGeneration -= SetAmountOfEnemies;
        CameraBasicController.OnNullReferenceException -= NullRefNotificationSafeguard;
        Room.OnBeingCreated -= UpdateConfinerList;
        Health.OnBossDeath -= IncreaseBossKills;
        Shop.OnInteractingWithShop_TargetIsVisible -= SetTargetVisibility;
        // Room.OnPlayerExiting -= SendNewConfiner;
    }
}
