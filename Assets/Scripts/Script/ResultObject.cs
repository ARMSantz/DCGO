using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System;

public class ResultObject : MonoBehaviour
{
    [SerializeField] Image WinImage;
    [SerializeField] Image LoseImage;
    [SerializeField] Text ResultText;

    bool _rematchShown = false;

    public void Init()
    {
        this.gameObject.SetActive(false);
    }

    public void ShowResult(Player Winner, bool Surrendered, string effectName = "")
    {
        this.gameObject.SetActive(true);

        bool isDisconnected = false;

        string log = "";

        log += "\nEnd Game";

        if (Winner != null)
        {
            log += $"\nWinner:{Winner.PlayerName}";
        }

        ResultText.text = "";

        if (String.IsNullOrEmpty(effectName))
        {
            log += $"\nEffect:{effectName}";
            ResultText.text = effectName;
        }            

        if (Winner == GManager.instance.You)
        {
            ContinuousController.instance.PlaySE(GManager.instance.WinSE);

            WinImage.gameObject.SetActive(true);
            LoseImage.gameObject.SetActive(false);

            if (!GManager.instance.IsAI)
            {
                ContinuousController.instance.WinCount++;
                ContinuousController.instance.SaveWinCount();

                if (Surrendered)
                    ResultText.text = "The opponent has surrendered.";

                if (String.IsNullOrEmpty(effectName))
                    ResultText.text = effectName;
            }
        }

        else if (Winner != null)
        {
            ContinuousController.instance.PlaySE(GManager.instance.LoseSE);

            WinImage.gameObject.SetActive(false);
            LoseImage.gameObject.SetActive(true);

            if (Winner != null)
            {
                if (Surrendered)
                    ResultText.text = "You have surrendered.";
            }
        }
        else
        {
            WinImage.gameObject.SetActive(false);
            LoseImage.gameObject.SetActive(true);

            isDisconnected = true;

            if (PhotonNetwork.IsConnected)
            {
                if (PhotonNetwork.PlayerList.Length == 2)
                {
                    isDisconnected = false;
                }

                if (GManager.instance.IsAI)
                {
                    isDisconnected = false;
                }

                WinImage.gameObject.SetActive(true);
                LoseImage.gameObject.SetActive(false);
            }

            if (isDisconnected)
            {
                log += $"\nDisconnected";

                ResultText.text = "Disconnected.";
            }

            else
            {
                log += $"\nDraw";

                ResultText.text = "Draw.";
            }
        }

        PlayLog.OnAddLog?.Invoke(log);

        // Fim de partida COM resultado (vitoria/derrota/empate) e SEM desconexao:
        // oferece rematch. Em desconexao nao ha rematch.
        if (!isDisconnected)
        {
            ShowRematchDialog();
        }
    }

    GameObject _rematchRoot;
    Text _rematchStatus;
    Button _btnRematch, _btnExit;
    bool _isPhoton;
    ContinuousController _cc;

    // Diálogo de rematch (procedural) sob a tela de resultado. REMATCH reinicia com a
    // mesma config (mesmos decks / mesma regra de quem comeca); SAIR/TROCAR DECK volta
    // ao menu (bot/random) ou à sala (room).
    // ROOM/RANDOM: so ha rematch se OS DOIS marcarem rematch; se um marcar sair, aviso
    // "sem rematch" + OK segue o fluxo. Cada um ve em tempo real o que o outro marcou.
    void ShowRematchDialog()
    {
        if (_rematchShown) return;
        _rematchShown = true;

        _cc = ContinuousController.instance;
        _cc.ResetRematchSession();
        _isPhoton = !GManager.instance.IsAI;
        bool room = _isPhoton && !_cc.isRandomMatch;

        var root = new GameObject("RematchDialog", typeof(RectTransform));
        _rematchRoot = root;
        root.transform.SetParent(this.transform, false);
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(0.5f, 0f);
        rrt.pivot = new Vector2(0.5f, 0f);
        rrt.sizeDelta = new Vector2(780f, _isPhoton ? 168f : 118f);
        rrt.anchoredPosition = new Vector2(0f, 64f);
        rrt.localScale = Vector3.one;
        root.transform.SetAsLastSibling();

        // Status (so em Photon): mostra as escolhas dos dois em tempo real.
        if (_isPhoton)
        {
            _rematchStatus = MakeLabelClone(root.transform, "");
            var srt = (RectTransform)_rematchStatus.transform;
            srt.anchorMin = new Vector2(0f, 0.62f); srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(12f, 0f); srt.offsetMax = new Vector2(-12f, 0f);
            _rematchStatus.resizeTextMaxSize = 24;
        }

        float by = _isPhoton ? 0.56f : 1f;
        _btnRematch = MakeResultButton(root.transform, 0.02f, 0.49f, by, "REMATCH",
            new Color(0.15f, 0.42f, 0.22f, 1f), () => _cc.ChooseRematch());
        _btnExit = MakeResultButton(root.transform, 0.51f, 0.98f, by, room ? "TROCAR DECK" : "SAIR",
            new Color(0.45f, 0.18f, 0.18f, 1f), () => _cc.ChooseExit());

        if (_isPhoton)
        {
            _cc.OnRematchUpdate += RefreshRematchUI;
            _cc.OnRematchCancelled += ShowNoRematchNotice;
            RefreshRematchUI();
        }
    }

    static string ChoiceText(ContinuousController.RematchChoice c)
    {
        if (c == ContinuousController.RematchChoice.Rematch) return "Rematch";
        if (c == ContinuousController.RematchChoice.Exit) return "Sair";
        return "aguardando...";
    }

    void RefreshRematchUI()
    {
        if (_cc == null) return;
        if (_rematchStatus != null)
            _rematchStatus.text = "Voce: " + ChoiceText(_cc.RematchMine)
                                + "     Oponente: " + ChoiceText(_cc.RematchTheirs);
        // Trava os botoes depois de escolher.
        bool chosen = _cc.RematchMine != ContinuousController.RematchChoice.None;
        if (_btnRematch != null) _btnRematch.interactable = !chosen;
        if (_btnExit != null) _btnExit.interactable = !chosen;
    }

    // Aviso "sem rematch" + OK: substitui o conteudo do dialogo.
    void ShowNoRematchNotice()
    {
        if (_rematchRoot == null) return;
        foreach (Transform ch in _rematchRoot.transform) Destroy(ch.gameObject);
        _btnRematch = _btnExit = null; _rematchStatus = null;

        var msg = MakeLabelClone(_rematchRoot.transform, "Sem rematch. O oponente nao aceitou.");
        var mrt = (RectTransform)msg.transform;
        mrt.anchorMin = new Vector2(0f, 0.55f); mrt.anchorMax = new Vector2(1f, 1f);
        mrt.offsetMin = new Vector2(12f, 0f); mrt.offsetMax = new Vector2(-12f, 0f);

        MakeResultButton(_rematchRoot.transform, 0.30f, 0.70f, 0.5f, "OK",
            new Color(0.20f, 0.30f, 0.48f, 1f), () => _cc.ConfirmNoRematch());
    }

    void OnDestroy()
    {
        if (_cc != null)
        {
            _cc.OnRematchUpdate -= RefreshRematchUI;
            _cc.OnRematchCancelled -= ShowNoRematchNotice;
        }
    }

    Button MakeResultButton(Transform parent, float xMin, float xMax, float yMax, string label,
                            Color color, UnityEngine.Events.UnityAction act)
    {
        var go = new GameObject("Btn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg = go.AddComponent<Image>();
        bg.color = color;
        var b = go.AddComponent<Button>();
        b.targetGraphic = bg;
        var cb = b.colors;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        b.colors = cb;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(xMin, 0f);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = new Vector2(0f, 6f); rt.offsetMax = new Vector2(0f, -6f);

        var lbl = MakeLabelClone(go.transform, label);
        var lrt = (RectTransform)lbl.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        b.onClick.AddListener(act);
        return b;
    }

    // Rótulo clonado do ResultText (Text que RENDERIZA na BattleScene) + strip de
    // qualquer componente de localizacao que reescreveria o texto.
    Text MakeLabelClone(Transform parent, string label)
    {
        var lgo = Instantiate(ResultText.gameObject, parent);
        lgo.name = "Lbl";
        foreach (var comp in lgo.GetComponents<Component>())
            if (!(comp is Text) && !(comp is RectTransform) && !(comp is CanvasRenderer))
                Destroy(comp);
        foreach (Transform ch in lgo.transform) Destroy(ch.gameObject);
        var t = lgo.GetComponent<Text>();
        t.text = label; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = true; t.resizeTextMinSize = 8; t.resizeTextMaxSize = 30;
        t.raycastTarget = false;
        var lrt = (RectTransform)lgo.transform;
        lrt.localScale = Vector3.one;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        return t;
    }
}
