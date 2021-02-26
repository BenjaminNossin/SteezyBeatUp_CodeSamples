using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MyUtility; 

public class Shop : MonoBehaviour
{
    [SerializeField] private PlayerData playerdata; 
    [SerializeField] private GameObject buttonPanel;
    [SerializeField] private Animator buttonAnimator;
    [SerializeField] private GameObject shopPanel; 
    [SerializeField] private BoxCollider2D shopCollider2D;
    [SerializeField] private Sprite[] digitsSpriteArray_Pool = new Sprite[10];
    [SerializeField] private Image[] digitsImageArray = new Image[3];

    private byte priceModifier;
    private byte priceOfRelaunch; 

    public static bool s_IsWaitingForInput;
    public static bool s_IsOpen;
    private static bool localIsOpen; 
    public static Action OnRelauchingAssignement;
    public static Action<bool> OnInteractingWithShop_TargetIsVisible;
    public static Action<bool> OnTryBuying; 
    private bool shopIsOpen;
    private bool canReadInput = true;

    private string stringedPrice;
    private char[] transitionCharArray;
    private List<byte> priceByteList = new List<byte>();

    [Header("Hub Arrays")]
    [SerializeField] private Image[] globalRun = new Image[2];
    [SerializeField] private Image[] currentRun = new Image[2];
    [SerializeField] private Image[] highScore = new Image[6];
    [SerializeField] private Image[] winRate = new Image[3];

    private List<byte[]> playerDataToProcess;
    private byte[] runCouter_GlobalArray;
    private byte[] runCouter_CurrentArray;
    private byte[] scoreArray;
    private byte[] winRateArray;
    private List<Image[]> arraysToProcess;

    [Header("Blink Effect")]
    [SerializeField] private Image m_BlinkingImage;
    private readonly float blinkDelays = 0.1f;
    private bool failedToBuyIsSet;
    private float timer;
    private bool doblink;

    Texture2D cursorTexture;
    private Room roomRef;

    private void OnEnable()
    {
        HubUI.OnFailingToBuy += Blink_Decorator; 
    }

    private void Start() 
    {
        if (SceneManager.GetActiveScene().name == SceneManager.GetSceneByBuildIndex((int)SceneIndexes.FirstLevel).name)
        {
            // setting price
            roomRef = GetComponentInParent<Room>();
            priceModifier = (byte)((int)PlayerData.s_Difficulty / 10);
            priceOfRelaunch = (byte)(priceModifier * roomRef.RoomTier);

            // updating price visuals
            transitionCharArray = new char[3];
            ConvertVariableToIndividualImages(stringedPrice, priceOfRelaunch, transitionCharArray, priceByteList, digitsImageArray);
        }
    }

    private void FixedUpdate()
    {
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetButtonDown("Interact")) && canReadInput && s_IsWaitingForInput)
        {
            shopIsOpen = !shopIsOpen;
            buttonPanel.SetActive(false);
            StartCoroutine(Cooldown());

            if (SceneManager.GetActiveScene().name == SceneManager.GetSceneByBuildIndex((int)SceneIndexes.Hub).name)
            {
                shopPanel.SetActive(shopIsOpen);
                Cursor.visible = true;
            }
            else if (SceneManager.GetActiveScene().name == SceneManager.GetSceneByBuildIndex((int)SceneIndexes.FirstLevel).name)
            {
                if (!roomRef)
                    roomRef = GetComponentInParent<Room>();

                if (!cursorTexture)
                    cursorTexture = Resources.Load<Texture2D>("target_cursor");
                Cursor.visible = shopIsOpen;
                Cursor.SetCursor(cursorTexture, new Vector2(-50f, 50f), CursorMode.Auto);

                OnInteractingWithShop_TargetIsVisible(!shopIsOpen);
                shopPanel.SetActive(roomRef.PlayerIsInsideRoom && shopIsOpen);
            }
        }

        if (SceneManager.GetActiveScene().name == SceneManager.GetSceneByBuildIndex((int)SceneIndexes.FirstLevel).name)
            Blink_Decorator(failedToBuyIsSet, m_BlinkingImage);
        else if (SceneManager.GetActiveScene().name == SceneManager.GetSceneByBuildIndex((int)SceneIndexes.Hub).name)
        {
            if (HubUI.s_BlinkingFromHub)
            {
                timer += Time.fixedDeltaTime;

                if (timer > 0.5f)
                {
                    timer = 0f;
                    doblink = false;
                    HubUI.s_BlinkingFromHub = false;
                    CancelInvoke(nameof(FailToBuyVisualWarning));
                }
            }
        }
    } 

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            s_IsWaitingForInput = true;
            buttonPanel.SetActive(s_IsWaitingForInput);
            buttonAnimator.enabled = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            s_IsWaitingForInput = false;
            buttonPanel.SetActive(s_IsWaitingForInput);
            buttonAnimator.enabled = false;
            Cursor.visible = false;
            shopPanel.SetActive(s_IsWaitingForInput);

            if (SceneManager.GetActiveScene().name == SceneManager.GetSceneByBuildIndex((int)SceneIndexes.FirstLevel).name)
                OnInteractingWithShop_TargetIsVisible(true);
        }
    }

    private void OnDisable()
    {
        HubUI.OnFailingToBuy -= Blink_Decorator;
    }

    // only because we did not used fontassets for the UI.. 
    public void ConvertVariableToIndividualImages(string stringedEmpty, byte variableToStore, char[] charArrayTransition, List<byte> targetByteList, Image[] imageTargetArray) 
    {
        stringedEmpty = $"{variableToStore}"; 
        charArrayTransition = stringedEmpty.ToCharArray();

        // could use queue instead
        for (int i = 0; i < imageTargetArray.Length; i++)
        {
            try
            {
                targetByteList.Add((byte)(Convert.ToByte(charArrayTransition[i]) - 48)); // -48 because otherwise I get Unicode value
                imageTargetArray[i].sprite = digitsSpriteArray_Pool[targetByteList[i]];

                if (targetByteList[i] == 0)
                    imageTargetArray[i].GetComponent<RectTransform>().localScale = new Vector3(0.9f, 1f, 1f);

            }
            catch (Exception e) when (Utility.LogException(e, gameObject.name))
            {
                imageTargetArray[i].enabled = false; 
            }
        }
    }

    public void ConvertVariableToIndividualSprites(string stringedEmpty, byte variableToStore, char[] charArrayTransition, List<byte> targetByteList, SpriteRenderer[] srTargetArray)
    {
        stringedEmpty = $"{variableToStore}";
        charArrayTransition = stringedEmpty.ToCharArray();

        // use queue instr
        for (int i = 0; i < srTargetArray.Length; i++)
        {
            try
            {
                targetByteList.Add((byte)(Convert.ToByte(charArrayTransition[i]) - 48));
                srTargetArray[i].sprite = digitsSpriteArray_Pool[targetByteList[i]];

                if (targetByteList[i] == 0) 
                    srTargetArray[i].GetComponent<Transform>().localScale = new Vector3(0.9f, 1f, 1f);

            }
            catch (Exception)
            {
                srTargetArray[i].enabled = false;
            }
        }
    }

    private void Blink_Decorator(bool failedToBuy, Image BlinkingImage)
    {
        m_BlinkingImage = BlinkingImage; // this is partially useless when not called by event.. 
        if (failedToBuy)
        {
            failedToBuyIsSet = false; 
            InvokeRepeating(nameof(FailToBuyVisualWarning), 0f, blinkDelays * 2f + Mathf.Epsilon);
        }
        else if (doblink ^ HubUI.s_BlinkingFromHub)
        {
            timer += Time.fixedDeltaTime;

            if (timer > 0.5f)
            {
                timer = 0f;
                doblink = false;
                HubUI.s_BlinkingFromHub = false; 
                CancelInvoke(nameof(FailToBuyVisualWarning));
            }
        }
    }

    public void HideCursor()
    {
        Cursor.visible = false;
        shopIsOpen = false;
        s_IsWaitingForInput = true; 
    }

    public void ResetInGameCursor(bool isVisible)
    {
        OnInteractingWithShop_TargetIsVisible(isVisible);
        shopIsOpen = false;
        s_IsWaitingForInput = true;
    }

    public void RelaunchAssignement()
    {
        if (playerdata.cash >= priceOfRelaunch)
        {
            playerdata.cash -= priceOfRelaunch; 
            s_IsOpen = s_IsWaitingForInput = false;
            OnRelauchingAssignement();

            shopPanel.SetActive(false); 
            HideCursor();
            ResetInGameCursor(true);
            shopCollider2D.enabled = false;
            OnTryBuying(true); // not enough cash sound
        }
        else 
        {
            OnTryBuying(false); // not enough cash sound
            timer = 0f;
            failedToBuyIsSet = true;
            doblink = true; 
        }

    }

    private void FailToBuyVisualWarning()
    {
        StartCoroutine(DoBlink(blinkDelays));
    }

    private IEnumerator DoBlink(float delay)
    {
        yield return new WaitForSeconds(delay);
        m_BlinkingImage.color = Color.red;

        yield return new WaitForSeconds(delay);
        m_BlinkingImage.color = Color.white; 
    } 

    private IEnumerator Cooldown()
    {
        canReadInput = false;
        yield return new WaitForSeconds(0.25f);
        canReadInput = true; 
    }
}
