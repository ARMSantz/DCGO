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
            // Sem escolha explicita o bot joga de deck ALEATORIO (regra original).
            ContinuousController.instance.BotDeckData = null;

            // Etapa 1 – deck do jogador
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

                // Sprite proprio e NOMEADO: a camada de skin (DcgoWebMenuSkin) ignora
                // sprites com nome, entao nada do banner e' escondido/re-skinnado.
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
                    // Trava de clique duplo: iniciar a partida 2x carrega a BattleScene
                    // (aditiva) duplicada e mata os botoes do mulligan.
                    if (battleStarted) return;
                    battleStarted = true;
                    Cleanup();
                    // Fecha o painel ANTES da partida — aberto, ele continua raycast
                    // por cima da BattleScene e bloqueia o mulligan.
                    sbd.SelectDeckObject.SetActive(false);
                    sbd.Off();
                    ContinuousController.instance.StartCoroutine(StartBattleCoroutine());
                }

                void RestartSelection()
                {
                    if (battleStarted) return;
                    Cleanup();
                    ContinuousController.instance.BotDeckData = null;
                    sbd.SelectDeckObject.SetActive(false);
                    StartSelectBattleDeck(true);
                }

                // ---------- Etapa 2 – deck do BOT ----------
                // Reusa a mecanica do fork (reseta _once/botao) e sobrepoe o titulo.
                sbd.SetUpBotDeckSelection(() =>
                {
                    DeckData botDeck = sbd.deckInfoPanel.ShowingDeckData;
                    if (botDeck == null || !botDeck.IsValidDeckData()) return;
                    ContinuousController.instance.BotDeckData = botDeck;
                    StartBattle();
                });

                sbd.TitleText.text = LocalizeUtility.GetLocalizedString(
                    EngMessage: "STEP 2/2 — CHOOSE THE BOT'S DECK",
                    JpnMessage: "ステップ 2/2 — Botのデッキを選択");

                if (selLabel != null)
                {
                    // Neutraliza o localizador do rotulo, senao ele reescreve de volta
                    // "Select Your Deck" logo apos eu trocar o texto.
                    foreach (var comp in selLabel.GetComponents<Component>())
                    {
                        var cn = comp.GetType().Name;
                        if (!(comp is Text) &&
                            (cn.IndexOf("Local", System.StringComparison.OrdinalIgnoreCase) >= 0
                          || cn.IndexOf("Translat", System.StringComparison.OrdinalIgnoreCase) >= 0))
                            UnityEngine.Object.Destroy(comp);
                    }
                    selLabel.text = LocalizeUtility.GetLocalizedString(
                        EngMessage: "Use as\nBOT deck",
                        JpnMessage: "Botのデッキ\nに使う");
                }

                // Template de texto que COMPROVADAMENTE renderiza: o rotulo do botao
                // Select (ex.: "Use as BOT deck" aparece). Clonar a TitleText nao
                // renderizava (fonte/material dela nao aparece neste painel no WebGL).
                Text txtTemplate = selLabel != null ? selLabel : sbd.TitleText;

                // Texto CLONADO do template: garante fonte/material que renderizam.
                Text MakeText(Transform parent, string txt, int size, TextAnchor anchor)
                {
                    GameObject go = UnityEngine.Object.Instantiate(txtTemplate.gameObject, parent);
                    go.name = "DcgoTxt";
                    // Remove TUDO que nao seja o proprio Text/RectTransform/CanvasRenderer:
                    // o template tem um componente de LOCALIZACAO que reescreve o texto de
                    // volta pra chave original (por isso aparecia "Select Your Deck").
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp is Text || comp is RectTransform || comp is CanvasRenderer) continue;
                        UnityEngine.Object.Destroy(comp);
                    }
                    foreach (Transform child in go.transform) UnityEngine.Object.Destroy(child.gameObject);
                    Text t = go.GetComponent<Text>();
                    t.text = txt; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                    t.raycastTarget = false;
                    RectTransform rt = (RectTransform)go.transform;
                    rt.localScale = Vector3.one;
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    return t;
                }

                // Faixa fixada no CANVAS RAIZ (coordenadas de TELA), na base ao centro —
                // garante que aparece SEMPRE (ancorar ao SelectDeckObject, de rect enorme,
                // jogava a faixa pra fora da tela). Fica abaixo da listagem de decks.
                Canvas rootCanvas = sbd.SelectDeckObject.GetComponentInParent<Canvas>();
                if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
                Transform bannerParent = rootCanvas != null ? rootCanvas.transform
                                                            : sbd.SelectDeckObject.transform;

                bannerGO = new GameObject("BotStepBanner", typeof(RectTransform));
                bannerGO.transform.SetParent(bannerParent, false);
                Image bannerImg = bannerGO.AddComponent<Image>();
                bannerImg.sprite = solid;
                bannerImg.color = new Color(0.05f, 0.10f, 0.18f, 0.98f);
                RectTransform bRT = (RectTransform)bannerGO.transform;
                bRT.anchorMin = new Vector2(0.5f, 0f);
                bRT.anchorMax = new Vector2(0.5f, 0f);
                bRT.pivot = new Vector2(0.5f, 0f);
                bRT.sizeDelta = new Vector2(1180f, 150f);
                bRT.anchoredPosition = new Vector2(0f, 40f);
                bannerGO.transform.SetAsLastSibling();

                // Info (esquerda): qual e' o SEU deck ja escolhido.
                GameObject infoHost = new GameObject("Info", typeof(RectTransform));
                infoHost.transform.SetParent(bannerGO.transform, false);
                RectTransform iRT = (RectTransform)infoHost.transform;
                iRT.anchorMin = new Vector2(0f, 0f);
                iRT.anchorMax = new Vector2(0.55f, 1f);
                iRT.offsetMin = new Vector2(22f, 4f); iRT.offsetMax = new Vector2(-6f, -4f);
                MakeText(infoHost.transform, LocalizeUtility.GetLocalizedString(
                    EngMessage: "NOW PICK THE BOT'S DECK\nYour deck: " + playerDeckName + "   (close = random bot)",
                    JpnMessage: "Botのデッキを選択\n自分のデッキ: " + playerDeckName + "（閉じる=ランダム）"),
                    22, TextAnchor.MiddleLeft);

                Button MakeBannerButton(float xMin, float xMax, string label, UnityAction act)
                {
                    GameObject go = new GameObject("BotBtn", typeof(RectTransform));
                    go.transform.SetParent(bannerGO.transform, false);
                    Image bg = go.AddComponent<Image>();
                    bg.sprite = solid;
                    bg.color = new Color(0.16f, 0.30f, 0.52f, 1f);
                    Button b = go.AddComponent<Button>();
                    b.targetGraphic = bg;
                    var cb = b.colors;
                    cb.highlightedColor = new Color(0.26f, 0.44f, 0.68f, 1f);
                    cb.pressedColor = new Color(0.10f, 0.20f, 0.36f, 1f);
                    cb.fadeDuration = 0.06f;
                    b.colors = cb;
                    RectTransform rt = (RectTransform)go.transform;
                    rt.anchorMin = new Vector2(xMin, 0.14f);
                    rt.anchorMax = new Vector2(xMax, 0.86f);
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    MakeText(go.transform, label, 20, TextAnchor.MiddleCenter);
                    b.onClick.AddListener(act);
                    return b;
                }

                MakeBannerButton(0.57f, 0.77f, LocalizeUtility.GetLocalizedString(
                    EngMessage: "RANDOM BOT", JpnMessage: "ランダムBot"),
                    () => { ContinuousController.instance.BotDeckData = null; StartBattle(); });

                MakeBannerButton(0.78f, 0.99f, LocalizeUtility.GetLocalizedString(
                    EngMessage: "CHANGE MY DECK", JpnMessage: "自分のデッキを変更"),
                    RestartSelection);

                // Fechar o painel na etapa 2 = cancelar e voltar a etapa 1
                sbd.OnCloseSelectBattleDeckAction = RestartSelection;

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
