using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using System;
using System.Linq;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class SelectBattleMode : MonoBehaviour
{
    [Header("バトルモード選択")]
    public YesNoObject selectBattleModeWindow;

    [Header("ルームマッチ選択")]
    [SerializeField] YesNoObject selectRoomMatchWindow;

    [Header("ルームID入力")]
    [SerializeField] EnterRoom enterRoom;

    [Header("ルームマッチマネージャ")]
    [SerializeField] RoomManager roomManager;

    [Header("LoadingObject")]
    public LoadingObject loadingObject;

    public void OffSelectBattleMode()
    {
        Off();
    }

    public void Off()
    {
        this.gameObject.SetActive(false);
    }

    bool connecting = false;

    public void SetUpSelectBattleMode()
    {
        if (connecting)
        {
            return;
        }

        ContinuousController.instance.StartCoroutine(SetUpSelectBattleModeCoroutine());
    }

    public IEnumerator SetUpSelectBattleModeCoroutine()
    {
        selectBattleModeWindow.CloseOnButtonClicked = false;
        selectRoomMatchWindow.CloseOnButtonClicked = false;

        selectBattleModeWindow.Off();
        selectRoomMatchWindow.Off();
        enterRoom.Off();

        Opening.instance.battle.selectBattleDeck.Off();

        if (PhotonNetwork.IsConnected)
        {
            yield return ContinuousController.instance.StartCoroutine(PhotonUtility.DisconnectCoroutine());
        }

        this.gameObject.SetActive(true);

        StartSelectBattleMode();
    }

    public void StartSelectBattleMode()
    {
        List<UnityAction> Commands = new List<UnityAction>()
            {
                () =>
                {
                    //ランダムマッチ
                    StartSelectBattleDeck(false);
                },

                () =>
                {
                    //ルームマッチ
                    StartSelectRoomMatch();
                },

                () =>
                {
                    //AI戦
                    StartSelectBattleDeck(true);
                },
            };

        List<string> CommandTexts = new List<string>()
            {
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Random Match",
                    JpnMessage:"ランダムマッチ"
                ),
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Room Match",
                    JpnMessage:"ルームマッチ"
                ),
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Bot Match",
                    JpnMessage:"Bot戦"
                ),
            };

        selectBattleModeWindow.SetUpYesNoObject(
            Commands,
            CommandTexts,
            LocalizeUtility.GetLocalizedString(
                    EngMessage: "Please select the mode to play.",
                    JpnMessage: "対戦モードを選択してください"
                ),
            true);
    }

    void StartSelectBattleDeck(bool isAI)
    {
        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        enterRoom.Close_(false);
        selectRoomMatchWindow.Close_(false);

        ContinuousController.instance.isAI = isAI;
        ContinuousController.instance.isRandomMatch = true;

        Opening.instance.battle.selectBattleDeck.Off();

        if (!ContinuousController.instance.isAI)
        {
            Opening.instance.battle.selectBattleDeck.SetUpSelectBattleDeck(Opening.instance.battle.selectBattleDeck.OnClickSelectButton_RandomMatch, 0);
        }

        else
        {
            ContinuousController.instance.AIBattleDeckData = null;

            // Etapa 1 – deck do jogador (fluxo original)
            Opening.instance.battle.selectBattleDeck.SetUpSelectBattleDeck(() =>
            {
                SelectBattleDeck sbd = Opening.instance.battle.selectBattleDeck;

                sbd.OnClickSelectButton_BotMatch();

                DeckData playerDeck = ContinuousController.instance.BattleDeckData;
                string playerDeckName = playerDeck != null ? playerDeck.DeckName : "-";

                bool battleStarted = false;
                GameObject bannerGO = null;

                Text selLabel = sbd.SelectDeckButton.GetComponentInChildren<Text>(true);
                string selLabelOriginal = selLabel != null ? selLabel.text : null;

                // Sprite proprio e NOMEADO para a UI da etapa 2 — a camada de skin
                // (DcgoWebMenuSkin) ignora sprites com nome, entao nada aqui e'
                // escondido ou re-skinnado por engano.
                Texture2D solidTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                solidTex.SetPixels32(new Color32[] {
                    new Color32(255,255,255,255), new Color32(255,255,255,255),
                    new Color32(255,255,255,255), new Color32(255,255,255,255) });
                solidTex.Apply();
                Sprite solid = Sprite.Create(solidTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                solid.name = "DcgoBotStepUI";

                void Cleanup()
                {
                    sbd.OnCloseSelectBattleDeckAction = null;
                    if (bannerGO != null) { UnityEngine.Object.Destroy(bannerGO); bannerGO = null; }
                    if (selLabel != null && selLabelOriginal != null) selLabel.text = selLabelOriginal;
                }

                void StartBattle()
                {
                    // Guarda contra clique duplo: carregar a BattleScene 2x (aditiva)
                    // duplica a UI da partida e trava os botoes do mulligan.
                    if (battleStarted) return;
                    battleStarted = true;
                    Cleanup();
                    sbd.SelectDeckObject.SetActive(false);
                    sbd.Off();
                    ContinuousController.instance.StartCoroutine(StartBattleCoroutine());
                }

                void RestartSelection()
                {
                    if (battleStarted) return;
                    Cleanup();
                    sbd.SelectDeckObject.SetActive(false);
                    StartSelectBattleDeck(true);
                }

                // ---------- Etapa 2 – deck do BOT ----------
                sbd.TitleText.text = LocalizeUtility.GetLocalizedString(
                    EngMessage: "STEP 2/2 — CHOOSE THE BOT'S DECK",
                    JpnMessage: "ステップ 2/2 — Botのデッキを選択");

                if (selLabel != null) selLabel.text = LocalizeUtility.GetLocalizedString(
                    EngMessage: "Use as\nBOT deck",
                    JpnMessage: "Botのデッキ\nに使う");

                bannerGO = new GameObject("BotStepBanner");
                bannerGO.transform.SetParent(sbd.SelectDeckObject.transform, false);
                Image bannerImg = bannerGO.AddComponent<Image>();
                bannerImg.sprite = solid;
                bannerImg.color = new Color(0.03f, 0.08f, 0.15f, 0.97f);
                RectTransform bRT = bannerGO.GetComponent<RectTransform>();
                bRT.anchorMin = new Vector2(0.5f, 0f);
                bRT.anchorMax = new Vector2(0.5f, 0f);
                bRT.pivot = new Vector2(0.5f, 0f);
                bRT.sizeDelta = new Vector2(1560f, 110f);
                bRT.anchoredPosition = new Vector2(0f, 18f);

                GameObject infoGO = new GameObject("Info");
                infoGO.transform.SetParent(bannerGO.transform, false);
                Text info = infoGO.AddComponent<Text>();
                info.font = sbd.TitleText.font;
                info.fontSize = 26;
                info.color = Color.white;
                info.alignment = TextAnchor.MiddleLeft;
                info.text = LocalizeUtility.GetLocalizedString(
                    EngMessage: "Your deck: " + playerDeckName + "  ✔\nPick the BOT's deck and press \"Use as BOT deck\" — or start with a random one.",
                    JpnMessage: "自分のデッキ: " + playerDeckName + " ✔\nBotのデッキを選ぶか、ランダムで開始してください。");
                RectTransform iRT = infoGO.GetComponent<RectTransform>();
                iRT.anchorMin = new Vector2(0f, 0f);
                iRT.anchorMax = new Vector2(0.52f, 1f);
                iRT.offsetMin = new Vector2(28f, 6f);
                iRT.offsetMax = new Vector2(-6f, -6f);

                Button MakeBannerButton(string label, float xMin, float xMax, UnityEngine.Events.UnityAction act)
                {
                    GameObject go = new GameObject("Btn_" + xMin);
                    go.transform.SetParent(bannerGO.transform, false);
                    Image bg = go.AddComponent<Image>();
                    bg.sprite = solid;
                    bg.color = new Color(0.15f, 0.28f, 0.48f, 1f);
                    Button b = go.AddComponent<Button>();
                    b.targetGraphic = bg;
                    RectTransform rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(xMin, 0f);
                    rt.anchorMax = new Vector2(xMax, 1f);
                    rt.offsetMin = new Vector2(8f, 14f);
                    rt.offsetMax = new Vector2(-8f, -14f);
                    GameObject lgo = new GameObject("Label");
                    lgo.transform.SetParent(go.transform, false);
                    Text lt = lgo.AddComponent<Text>();
                    lt.font = sbd.TitleText.font;
                    lt.fontSize = 24;
                    lt.color = Color.white;
                    lt.alignment = TextAnchor.MiddleCenter;
                    lt.text = label;
                    RectTransform lrt = lgo.GetComponent<RectTransform>();
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = Vector2.zero;
                    lrt.offsetMax = Vector2.zero;
                    b.onClick.AddListener(act);
                    return b;
                }

                MakeBannerButton(LocalizeUtility.GetLocalizedString(
                    EngMessage: "START — RANDOM BOT DECK",
                    JpnMessage: "ランダムBotで開始"),
                    0.53f, 0.78f, () =>
                {
                    ContinuousController.instance.AIBattleDeckData = null;
                    StartBattle();
                });

                MakeBannerButton(LocalizeUtility.GetLocalizedString(
                    EngMessage: "← CHANGE MY DECK",
                    JpnMessage: "← 自分のデッキを変更"),
                    0.79f, 0.99f, RestartSelection);

                // Confirmar: o botao "Select" do painel agora define o deck do BOT.
                // Copia o deck (mesmo padrao do RandomDeck original) para nunca
                // compartilhar a mesma instancia com o deck do jogador.
                sbd.deckInfoPanel.OnClickSelectDeckAction = () =>
                {
                    DeckData botDeck = sbd.deckInfoPanel.ShowingDeckData;
                    if (botDeck == null || !botDeck.IsValidDeckData()) return;
                    ContinuousController.instance.AIBattleDeckData =
                        new DeckData(botDeck.GetThisDeckCode(), botDeck.DeckID);
                    StartBattle();
                };

                // Fechar o painel na etapa 2 = cancelar e voltar a etapa 1
                sbd.OnCloseSelectBattleDeckAction = RestartSelection;

                ContinuousController.instance.StartCoroutine(sbd.SetDeckList(false));

            }, 0);
        }

        IEnumerator StartBattleCoroutine()
        {
            selectRoomMatchWindow.Close_(false);
            enterRoom.Close_(false);

            ContinuousController.instance.StartCoroutine(Opening.instance.OpeningBGM.FadeOut(0.1f));
            yield return ContinuousController.instance.StartCoroutine(Opening.instance.LoadingObject.StartLoading("Now Loading"));

            foreach (Camera camera in Opening.instance.openingCameras)
            {
                camera.gameObject.SetActive(false);
            }

            Opening.instance.OffYesNoObjects();

            Opening.instance.deck.trialDraw.Close();

            Opening.instance.deck.deckListPanel.Close();

            yield return new WaitForSeconds(0.1f);
            SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Additive);
        }
    }

    public void StartSelectRoomMatch()
    {
        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        Opening.instance.battle.selectBattleDeck.Off();
        ContinuousController.instance.isAI = false;

        List<UnityAction> Commands = new List<UnityAction>()
            {
                () =>
                {
                    //部屋を作る
                    StartCreateRoom();
                },

                () =>
                {
                    //部屋に入る
                    StartEnterRoomID();
                },
            };

        List<string> CommandTexts = new List<string>()
            {
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Create Room",
                    JpnMessage:"ルーム作成"
                ),
                LocalizeUtility.GetLocalizedString(
                    EngMessage:"Join Room",
                    JpnMessage:"ルームに入る"
                ),
            };

        selectRoomMatchWindow.SetUpYesNoObject(
            Commands,
            CommandTexts,
            LocalizeUtility.GetLocalizedString(
                    EngMessage: "Please choose between creating a room or joining an existing one.",
                    JpnMessage: "ルームを作成するかルームに入るか\n選択してください"
                ),
            true);
    }

    void StartCreateRoom()
    {
        roomManager.SetUpRoom();
    }

    void StartEnterRoomID()
    {
        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        enterRoom.SetUpEnterRoom();
    }

    public void OnClickCloseEnterRoomWindow()
    {
        enterRoom.Close_(false);
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();
    }

    public void OnClickCloseSelectRoomMatchWindow()
    {
        enterRoom.Close_(false);
        selectRoomMatchWindow.Close_(false);
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();
    }

    public void OnClickSelectBattleModeWindow()
    {
        enterRoom.Close_(true);
        selectRoomMatchWindow.Close_(false);
        selectBattleModeWindow.Close_(false);
        ContinuousController.instance.PlaySE(Opening.instance.CancelSE);

        Opening.instance.OffYesNoObjects();

        Opening.instance.deck.trialDraw.Close();

        Opening.instance.deck.deckListPanel.Close();

        ContinuousController.instance.StartCoroutine(OnClickSelectBattleModeWindowIEnumerator());
    }

    IEnumerator OnClickSelectBattleModeWindowIEnumerator()
    {
        yield return new WaitForSeconds(0.3f);
        Opening.instance.battle.OffBattle();
        Opening.instance.home.SetUpHomeMode_Disconnect();
        this.gameObject.SetActive(false);
    }
}
