#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace DcgoWebBuild
{
    // RE-SKIN PROCEDURAL DOS MENUS.
    //
    // O repositorio publico do DCGO nao traz toda a "skin" dos menus (faltam ~15
    // materiais e ~163 sprites de UI empacotados so no build desktop). Sem eles o
    // WebGL mostra MAGENTA (material de erro) e CAIXAS BRANCAS (Image cujo sprite
    // resolve pra null, mascaras sem sprite e decoracoes com SCRIPT faltando). A UI
    // de menu vive toda na cena "Opening" e nasce de prefabs em runtime, entao um
    // fix unico no load nao alcanca. Por isso esta camada roda em LOOP e:
    //
    //   * BOTOES: acha o fundo do botao em qualquer profundidade (a hierarquia real
    //     e' Button > Mask > BackGroundImage) e da um sprite arredondado limpo com
    //     cores de hover/press — sem tocar em onClick/logica;
    //   * MASCARAS sem sprite: desliga so o desenho (showMaskGraphic=false) pra sumir
    //     o branco SEM quebrar o recorte;
    //   * DECORACOES quebradas grandes (script faltando / glow com shader stripado):
    //     somem;
    //   * fundo de dialogo grande vira painel azul-escuro translucido.
    //
    // NAO-DESTRUTIVO: nada dentro de um Selectable e' escondido (X de fechar, filtros,
    // checkmarks, icones de botao ficam visiveis). Escopo: NUNCA roda na partida
    // (BattleScene) — cartas/campo/gameplay 100% intactos. Tudo e' visual
    // (Image.sprite/cor/material); onClick e logica nunca sao tocados.
    public class DcgoWebMenuSkin : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("DcgoWebMenuSkin");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<DcgoWebMenuSkin>();
        }

        Sprite _btn, _panel, _bg;
        Font _uiFont;                                          // fonte de UI reaproveitada
        readonly HashSet<int> _seenSel = new HashSet<int>();   // Selectables ja tratados
        readonly HashSet<int> _btnImg  = new HashSet<int>();   // Images que sao fundo de botao
        readonly HashSet<int> _doneImg = new HashSet<int>();   // Images de poluicao ja tratadas
        readonly HashSet<int> _diag    = new HashSet<int>();   // ja logados no diagnostico
        readonly HashSet<int> _rawHidden = new HashSet<int>(); // RawImages escondidas (sem textura)
        readonly Dictionary<int, Color> _battleColor = new Dictionary<int, Color>(); // cor original recolorida na partida
        TMP_Text _tmpTemplate;                                 // TMP que renderiza (p/ clonar rotulos)
        bool _logged;

        void Start()
        {
            _btn   = MakeRounded(48, 12);   // botao: cantos medios
            _panel = MakeRounded(72, 24);   // painel/dialogo: cantos grandes
            _bg    = MakeVerticalGradient(96,
                         new Color32(24, 40, 68, 255),    // topo: azul profundo
                         new Color32(8, 14, 26, 255));    // base: quase preto
            SceneManager.sceneLoaded += OnScene;
            StartCoroutine(Loop());
        }

        void OnScene(Scene s, LoadSceneMode m)
        {
            _seenSel.Clear(); _btnImg.Clear(); _doneImg.Clear(); _diag.Clear(); _logged = false;
        }

        IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(0.4f);
            while (true) { try { Skin(); } catch { } yield return wait; }
        }

        // ---------------------------------------------------------------- skin ----

        void Skin()
        {
            // ESCOPO ESTRITO: NUNCA rodar durante a partida. ATENCAO: a BattleScene e'
            // carregada ADITIVA e a cena ATIVA continua "Opening" — checar a cena ativa
            // NAO basta. Usa IsValid() (nao isLoaded): a cena entra na lista JA no comeco
            // do carregamento assincrono, entao pulamos tambem a JANELA de load — assim
            // FindObjectsOfType nunca toca no campo/cartas nem por um instante. Ao fim da
            // partida ela e' descarregada e o menu volta a ser skinnado.
            var battleScene = SceneManager.GetSceneByName("BattleScene");
            if (battleScene.IsValid() || battleScene.isLoaded) { SkinBattle(); return; }

            // Saiu da partida: limpa o rastro de recoloracao (ids ja destruidos).
            if (_battleColor.Count > 0) _battleColor.Clear();

            // Botao de LOGOUT no canto do menu (cria uma vez).
            EnsureLogoutButton();

            // (a) Magenta: SO renderers com shader de ERRO (InternalError). NAO mexer em
            //     material nulo — carta/preview carregando a textura tem material nulo por
            //     um instante e nao pode ser desabilitada pra sempre.
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
                if (r && BrokenShader(r.sharedMaterial))
                    r.enabled = false;

            // (a2) PARTICULAS DE FUNDO do menu (PixelRise/PixelStorm/Circuits) rendem
            //      CAIXAS BRANCAS mesmo em GPU real (o blend/arte original nao veio no
            //      repo publico) — validado pelo usuario. Ficam desligadas por padrao,
            //      junto com qualquer particula sem textura ou com shader quebrado.
            foreach (var ps in Object.FindObjectsOfType<ParticleSystem>(true))
            {
                var pr = ps ? ps.GetComponent<ParticleSystemRenderer>() : null;
                if (pr == null) continue;
                var pm = pr.sharedMaterial;
                bool kill = IsBgDeco(ps.transform) || pm == null || pm.mainTexture == null || BrokenShader(pm);
                if (kill) pr.enabled = false;
            }

            // (a3) RawImage sem textura renderiza BRANCO (covers de deck carregam a
            //      textura async). Fica transparente e VOLTA quando a textura chega.
            foreach (var ri in Object.FindObjectsOfType<RawImage>(true))
            {
                if (!ri) continue;
                int rid = ri.GetInstanceID();
                if (ri.texture == null)
                {
                    if (ri.color.a > 0f)
                    {
                        _rawHidden.Add(rid);
                        ri.color = new Color(ri.color.r, ri.color.g, ri.color.b, 0f);
                    }
                }
                else if (_rawHidden.Remove(rid))
                {
                    ri.color = new Color(ri.color.r, ri.color.g, ri.color.b, 1f);
                }
            }

            // (b) UI glow com shader de erro (GlowImage/UIEffect stripados) OU UIParticle
            //     (ParticleEffectForUGUI) sem textura. Texto/botao -> volta ao material
            //     padrao (legivel/clicavel). Decoracao/particula solta -> some.
            foreach (var g in Object.FindObjectsOfType<Graphic>(true))
            {
                if (!g) continue;
                var mat = g.material;
                bool isParticle = g.GetType().Name.IndexOf("Particle") >= 0;
                bool whiteParticle = isParticle && (mat == null || mat.mainTexture == null);
                if (!BrokenShader(mat) && !whiteParticle) continue;
                bool keep = g is Text || IsTMP(g) || (!isParticle && InSelectable(g.transform));
                if (keep) g.material = null;
                else g.enabled = false;
            }

            int skinned = 0, hidden = 0, masks = 0;

            // (c) BOTOES: skin do fundo em qualquer profundidade (fica visivel/clicavel).
            foreach (var sel in Object.FindObjectsOfType<Selectable>(true))
            {
                if (!sel) continue;
                if (!_seenSel.Add(sel.GetInstanceID())) continue;

                bool isClose = sel.name.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || sel.name.IndexOf("Return", System.StringComparison.OrdinalIgnoreCase) >= 0;

                var bg = PickButtonBackground(sel);
                if (bg != null)
                {
                    _btnImg.Add(bg.GetInstanceID());
                    SkinButton(sel, bg);
                    skinned++;
                }
                else if (sel.transition == Selectable.Transition.ColorTint)
                {
                    // Sem fundo quebrado pra trocar: mas se o botao usa ColorTint sobre
                    // uma arte transparente/branca, o HOVER pinta ela de branco e cobre
                    // o texto (ex.: BATTLE/DECK do menu). Neutraliza os estados.
                    var cb = sel.colors;
                    cb.highlightedColor = cb.normalColor;
                    cb.pressedColor     = cb.normalColor;
                    cb.selectedColor    = cb.normalColor;
                    sel.colors = cb;
                }

                // Botao de fechar/voltar sempre com alvo visivel (o "X" some se veio
                // como sprite quebrado no passo (d)).
                if (isClose) EnsureCloseButton(sel);
            }

            // (d) RESTO: mascaras, paineis de dialogo e caixas brancas GRANDES.
            foreach (var img in Object.FindObjectsOfType<Image>(true))
            {
                if (!img) continue;
                int id = img.GetInstanceID();
                if (_btnImg.Contains(id)) continue;   // e' fundo de botao, ja tratado

                // DIAG: TODA imagem grande/opaca/ativa (inclusive dentro de Selectable e
                // mascara) — pra achar as caixas de glow que escapam dos filtros.
                if (img.gameObject.activeInHierarchy && img.color.a > 0.5f && Big(img)
                    && _diag.Add(id) && _diag.Count <= 60)
                {
                    var mat = img.material; var sh = mat && mat.shader ? mat.shader.name : "?";
                    Debug.Log("[DCGOSKIN d] " + img.transform.name
                        + " parent=" + (img.transform.parent ? img.transform.parent.name : "-")
                        + " sprite=" + (img.sprite ? img.sprite.name : "NULL")
                        + " sel=" + InSelectable(img.transform)
                        + " mask=" + (img.GetComponent<Mask>() != null)
                        + " sh=" + sh + " size=" + img.rectTransform.rect.size);
                }

                var mask = img.GetComponent<Mask>();
                if (mask != null)
                {
                    if (mask.showMaskGraphic && IsBrokenArt(img)) { mask.showMaskGraphic = false; masks++; }
                    continue;   // nunca mexe no alfa da mascara (quebraria o recorte)
                }

                // Dentro de um Selectable: arte VALIDA fica intacta (filtro, checkmark,
                // seta, X de fechar). Arte QUEBRADA (sprite vazio) e que nao e' o
                // targetGraphic e' transparentizada — icon de botao sem sprite some
                // sem quebrar o raycast.
                if (InSelectable(img.transform))
                {
                    if (IsBrokenArt(img))
                    {
                        if (_doneImg.Contains(id)) continue;
                        _doneImg.Add(id);
                        img.color = new Color(0f, 0f, 0f, 0f);
                        hidden++;
                        continue;
                    }
                    // Placa BRANCA de campo (dropdown de filtro etc.): escurece pra ver
                    // a selecao. NAO tocar em InputField (campo de texto tem letra escura).
                    if (IsWhitePlate(img) && img.GetComponentInParent<InputField>(true) == null)
                    {
                        if (_doneImg.Contains(id)) continue;
                        _doneImg.Add(id);
                        img.color = new Color(0.10f, 0.14f, 0.22f, 0.92f);
                        hidden++;
                    }
                    continue;
                }

                // _doneImg so' marca quando realmente escondemos — assim imagens que
                // mudam de sprite valido para vazio (ex.: Background_home1 → null) sao
                // re-avaliadas no proximo tick e escondidas.
                if (_doneImg.Contains(id)) continue;

                // Placa BRANCA solta (ex.: tarja do NOME do deck na listagem): escurece
                // pra o nome (texto claro) aparecer, em vez de sumir.
                if (IsWhitePlate(img))
                {
                    _doneImg.Add(id);
                    img.color = new Color(0.10f, 0.14f, 0.22f, 0.92f);
                    hidden++;
                    continue;
                }

                bool broken = HasMissingScriptUp(img.transform, 2) || IsBrokenArt(img);
                if (!broken) continue;
                _doneImg.Add(id);

                // Fundo de tela quebrado (BackGroundObject / LoadingObject) ganha um
                // degrade azul-escuro em vez de simplesmente sumir — home/loading com
                // visual limpo e profissional.
                var par = img.transform.parent;
                bool bgHost = par != null && (par.name == "BackGroundObject" || par.name == "LoadingObject");
                if (bgHost && img.GetComponent<UnityEngine.EventSystems.EventTrigger>() == null)
                {
                    img.sprite = _bg; img.type = Image.Type.Simple; img.color = Color.white;
                    hidden++;
                }
                // So decoracao/painel: dialogo -> painel escuro; caixa GRANDE -> some.
                // Caixinha quebrada solta fica como esta (evita apagar algo util).
                else if (IsDialogBackground(img) && !IsFullscreen(img)) { SkinPanel(img); hidden++; }
                else if (Big(img) || IsFullscreen(img)) { Hide(img); hidden++; }
            }

            if (skinned > 0 || hidden > 0 || !_logged)
            {
                _logged = true;
                Debug.Log("[DCGOSKIN] scene=" + SceneManager.GetActiveScene().name +
                          " skinnedBtn=" + skinned + " hidden=" + hidden + " masksOff=" + masks);
            }
        }

        // ------------------------------------------------------------- partida ----
        //
        // Skin CONSERVADOR da BattleScene: SO recolore caixas brancas NAO-interativas
        // (versos de carta, zonas/pilhas, paineis de dialogo) — assets que nao vieram
        // no fork publico e renderizam branco. Regras de ouro:
        //   * so mexe em objetos da CENA "BattleScene";
        //   * NUNCA toca em Image dentro de Selectable (cartas clicaveis, botoes);
        //   * NUNCA troca sprite, esconde, desliga Renderer/ParticleSystem nem mexe em
        //     Text — so a COR (tint), e guarda a original;
        //   * REVERTE a cor quando a arte carrega (async) — nada fica escuro por engano.
        // Assim a partida fica legivel sem tocar em cartas, posicoes, memoria ou logica.
        void SkinBattle()
        {
            foreach (var img in Object.FindObjectsOfType<Image>(true))
            {
                if (!img) continue;
                if (img.gameObject.scene.name != "BattleScene") continue;
                if (img.GetComponent<Mask>() != null) continue;
                if (InSelectable(img.transform)) continue;   // protege cartas/botoes

                int id = img.GetInstanceID();
                var col = img.color;
                bool whiteish = IsBrokenArt(img)
                    || (col.a > 0.55f && col.r > 0.78f && col.g > 0.78f && col.b > 0.78f);
                var sz = img.rectTransform.rect.size;
                bool bigEnough = Mathf.Abs(sz.x) >= 24f && Mathf.Abs(sz.y) >= 24f;

                if (whiteish && bigEnough)
                {
                    if (!_battleColor.ContainsKey(id)) _battleColor[id] = col;
                    bool panel = Mathf.Abs(sz.x) > 220f && Mathf.Abs(sz.y) > 140f;
                    img.color = panel
                        ? new Color(0.06f, 0.10f, 0.18f, 0.92f)    // painel/zona: azul escuro
                        : new Color(0.12f, 0.18f, 0.30f, 1f);      // verso/placa: azul medio
                }
                else if (_battleColor.TryGetValue(id, out var orig))
                {
                    img.color = orig;               // arte carregou: devolve a cor original
                    _battleColor.Remove(id);
                }
            }
        }

        // ------------------------------------------------------------- logout ----

        // Canvas de tela do menu (cena "Opening"), para pendurar o botao e o dialogo.
        Canvas MenuCanvas()
        {
            Canvas fallback = null;
            foreach (var c in Object.FindObjectsOfType<Canvas>())
            {
                if (!c || !c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
                if (fallback == null) fallback = c;
                if (c.gameObject.scene.name == "Opening") return c;
            }
            return fallback;
        }

        // Botao "LOGOUT" fixo no canto superior direito do menu. Cores NAO-brancas +
        // sprite NOMEADO + transicao None: a propria camada de skin ignora (nao
        // re-skinna nem escurece). Criado uma unica vez.
        void EnsureLogoutButton()
        {
            var canvas = MenuCanvas();
            if (canvas == null) return;
            if (canvas.transform.Find("DcgoLogoutBtn") != null) return;

            var b = MakeSkinButton(canvas.transform, "DcgoLogoutBtn", "LOGOUT",
                                   new Color(0.45f, 0.16f, 0.18f, 0.96f));
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(150f, 50f);
            rt.anchoredPosition = new Vector2(-18f, -16f);
            b.transform.SetAsLastSibling();
            b.onClick.AddListener(ShowLogoutConfirm);
        }

        // Dialogo estilizado de confirmacao: overlay escuro (bloqueia clique) + painel
        // com texto e botoes CANCELAR / SAIR. OK chama DcgoWebBridge.Logout().
        void ShowLogoutConfirm()
        {
            var canvas = MenuCanvas();
            if (canvas == null) return;
            if (canvas.transform.Find("DcgoLogoutDialog") != null) return;

            // Overlay de tela cheia (escurece o fundo e captura o clique).
            var overlay = new GameObject("DcgoLogoutDialog", typeof(RectTransform));
            overlay.transform.SetParent(canvas.transform, false);
            var oimg = overlay.AddComponent<Image>();
            oimg.sprite = _panel; oimg.type = Image.Type.Sliced;
            oimg.color = new Color(0.01f, 0.02f, 0.05f, 0.82f);
            oimg.raycastTarget = true;
            var ort = (RectTransform)overlay.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
            overlay.transform.SetAsLastSibling();

            // Painel central.
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(overlay.transform, false);
            var pimg = panel.AddComponent<Image>();
            pimg.sprite = _panel; pimg.type = Image.Type.Sliced;
            pimg.color = new Color(0.08f, 0.13f, 0.22f, 1f);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(620f, 280f);
            prt.anchoredPosition = Vector2.zero;

            // Texto.
            var msg = MakeLabel(panel.transform, "Sair da conta?\nVoce voltara para a tela de login.", 12, 28);
            var trt = (RectTransform)msg.transform;
            trt.anchorMin = new Vector2(0f, 0.42f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(26f, 0f); trt.offsetMax = new Vector2(-26f, -18f);

            // Botoes.
            var cancel = MakeSkinButton(panel.transform, "Cancel", "CANCELAR",
                                        new Color(0.20f, 0.28f, 0.40f, 1f));
            var crt = (RectTransform)cancel.transform;
            crt.anchorMin = new Vector2(0.06f, 0.10f); crt.anchorMax = new Vector2(0.47f, 0.34f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            cancel.onClick.AddListener(() => Object.Destroy(overlay));

            var ok = MakeSkinButton(panel.transform, "Ok", "SAIR",
                                    new Color(0.55f, 0.16f, 0.18f, 1f));
            var okrt = (RectTransform)ok.transform;
            okrt.anchorMin = new Vector2(0.53f, 0.10f); okrt.anchorMax = new Vector2(0.94f, 0.34f);
            okrt.offsetMin = Vector2.zero; okrt.offsetMax = Vector2.zero;
            ok.onClick.AddListener(() => { DcgoWebBridge.Logout(); });
        }

        // Botao com fundo arredondado NOMEADO + cor fixa (nao-branca) + rotulo. A cor
        // nao-branca e o sprite nomeado fazem a camada de skin ignora-lo.
        Button MakeSkinButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.sprite = _btn; bg.type = Image.Type.Sliced; bg.color = color;
            var b = go.AddComponent<Button>();
            b.targetGraphic = bg;
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.colorMultiplier = 1f; cb.fadeDuration = 0.06f;
            b.colors = cb;

            var t = MakeLabel(go.transform, label, 8, 32);
            var lrt = (RectTransform)t.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6f, 4f); lrt.offsetMax = new Vector2(-6f, -4f);
            return b;
        }

        // TMP que COMPROVADAMENTE renderiza no WebGL. O jogo usa TextMeshPro nos menus;
        // criar Text/TMP do zero nao aparece — precisa CLONAR um TMP existente (herda o
        // asset de fonte SDF ja empacotado).
        TMP_Text TmpTemplate()
        {
            if (_tmpTemplate != null) return _tmpTemplate;
            foreach (var t in Object.FindObjectsOfType<TextMeshProUGUI>())
            {
                if (t == null || t.font == null) continue;
                if (t.name.IndexOf("Dcgo", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                _tmpTemplate = t; break;
            }
            return _tmpTemplate;
        }

        // Rotulo clonado de um TMP (renderiza) + strip de localizador/layout.
        Graphic MakeLabel(Transform parent, string txt, float minS, float maxS)
        {
            var tmpl = TmpTemplate();
            if (tmpl != null)
            {
                var go = Object.Instantiate(tmpl.gameObject, parent);
                go.name = "DcgoLbl";
                foreach (var comp in go.GetComponents<Component>())
                    if (!(comp is TMP_Text) && !(comp is RectTransform) && !(comp is CanvasRenderer))
                        Object.Destroy(comp);
                foreach (Transform ch in go.transform) Object.Destroy(ch.gameObject);
                var t = go.GetComponent<TMP_Text>();
                t.text = txt; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
                t.enableWordWrapping = true; t.richText = false;
                t.enableAutoSizing = true; t.fontSizeMin = minS; t.fontSizeMax = maxS;
                t.raycastTarget = false;
                var rt = (RectTransform)t.transform;
                rt.localScale = Vector3.one;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                return t;
            }

            // Fallback (legado) caso nao ache TMP — melhor que nada.
            var lgo = new GameObject("DcgoLbl", typeof(RectTransform));
            lgo.transform.SetParent(parent, false);
            var lt = lgo.AddComponent<Text>();
            lt.font = UiFont();
            lt.text = txt; lt.color = Color.white; lt.alignment = TextAnchor.MiddleCenter;
            lt.resizeTextForBestFit = true; lt.resizeTextMinSize = (int)minS; lt.resizeTextMaxSize = (int)maxS;
            lt.raycastTarget = false;
            var lrt = (RectTransform)lt.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return lt;
        }

        // Escolhe a imagem de fundo de um botao: o targetGraphic se estiver quebrado,
        // senao a maior Image quebrada dentro dele (preferindo nome "BackGround").
        Image PickButtonBackground(Selectable sel)
        {
            var tg = sel.targetGraphic as Image;
            if (tg != null && IsBrokenArt(tg) && tg.GetComponent<Mask>() == null) return tg;

            Image best = null; float bestScore = -1f;
            foreach (var im in sel.GetComponentsInChildren<Image>(true))
            {
                if (im == null || im.GetComponent<Mask>() != null) continue;
                if (!IsBrokenArt(im)) continue;
                var s = im.rectTransform.rect.size;
                float score = Mathf.Abs(s.x * s.y);
                if (im.name.IndexOf("ackGround") >= 0 || im.name.IndexOf("ackground") >= 0) score += 1e9f;
                if (score > bestScore) { bestScore = score; best = im; }
            }
            return best;
        }

        void SkinButton(Selectable sel, Image bg)
        {
            // Catcher de clique de tela cheia (ex.: "CLICK TO START", fundo atras de
            // dialogo): mantem clicavel mas INVISIVEL — nunca vira caixa azul gigante.
            if (IsFullscreen(bg))
            {
                bg.color = new Color(1f, 1f, 1f, 0f);
                bg.raycastTarget = true;
                sel.transition = Selectable.Transition.None;  // nao piscar branco no hover
                return;
            }

            bg.sprite = _btn;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;          // a cor real vem do ColorBlock (multiplica)
            bg.raycastTarget = true;

            sel.transition = Selectable.Transition.ColorTint;
            sel.targetGraphic = bg;
            var cb = sel.colors;
            cb.normalColor      = new Color(0.13f, 0.20f, 0.34f, 0.95f);
            cb.highlightedColor = new Color(0.20f, 0.32f, 0.52f, 1f);
            cb.pressedColor     = new Color(0.09f, 0.14f, 0.24f, 1f);
            cb.selectedColor    = new Color(0.20f, 0.32f, 0.52f, 1f);
            cb.disabledColor    = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.08f;
            sel.colors = cb;
        }

        void SkinPanel(Image img)
        {
            img.sprite = _panel;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.06f, 0.10f, 0.18f, 0.92f);   // azul escuro translucido
        }

        // Garante um botao de fechar VISIVEL e clicavel: fundo arredondado vermelho-
        // escuro + um "X" branco, criados uma vez por botao (guardado por nome de filho).
        void EnsureCloseButton(Selectable sel)
        {
            var t = sel.transform;
            if (t.Find("DcgoCloseGlyph") != null) return;

            // Fundo: usa o targetGraphic se for Image, senao cria um.
            Image bg = sel.targetGraphic as Image;
            if (bg == null) bg = sel.GetComponentInChildren<Image>(true);
            if (bg == null)
            {
                var bgGo = new GameObject("DcgoCloseBg");
                bgGo.transform.SetParent(t, false);
                bg = bgGo.AddComponent<Image>();
                var rt = bg.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                bgGo.transform.SetAsFirstSibling();
            }
            bg.sprite = _btn;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.55f, 0.16f, 0.16f, 1f);       // vermelho escuro
            bg.raycastTarget = true;
            _btnImg.Add(bg.GetInstanceID());
            sel.targetGraphic = bg;
            sel.transition = Selectable.Transition.ColorTint;
            var cb = sel.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.5f, 0.5f, 1f);
            cb.pressedColor = new Color(0.7f, 0.2f, 0.2f, 1f);
            cb.colorMultiplier = 1f;
            sel.colors = cb;

            var font = UiFont();
            if (font != null)
            {
                var g = new GameObject("DcgoCloseGlyph");
                g.transform.SetParent(t, false);
                var txt = g.AddComponent<Text>();
                txt.font = font;
                txt.text = "✕";                             // ✕
                txt.color = Color.white;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.resizeTextForBestFit = true;
                txt.resizeTextMinSize = 8;
                txt.resizeTextMaxSize = 60;
                var rt = txt.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                g.transform.SetAsLastSibling();
            }
        }

        Font UiFont()
        {
            if (_uiFont == null)
            {
                var anyText = Object.FindObjectOfType<Text>();
                if (anyText != null) _uiFont = anyText.font;
            }
            return _uiFont;
        }

        static void Hide(Image img)
        {
            // So transparencia. NAO mexer em raycastTarget: telas como o "CLICK TO
            // START" usam um Image transparente + EventTrigger de tela cheia pra pegar
            // o clique; desligar o raycast quebraria a navegacao.
            img.color = new Color(1f, 1f, 1f, 0f);
        }

        // ------------------------------------------------------------- helpers ----

        // A imagem faz parte de um controle (Button/Toggle/Slider/...) — self ou
        // ancestral. Icones/fundos de controle NUNCA sao escondidos (perderia funcao).
        static bool InSelectable(Transform t)
        {
            return t.GetComponentInParent<Selectable>(true) != null;
        }

        static bool BrokenShader(Material m)
        {
            return m != null && (m.shader == null || m.shader.name == "Hidden/InternalErrorShader");
        }

        // Particulas decorativas do fundo do menu: mesmo com textura valida elas
        // renderizam caixas brancas no WebGL (validado na GPU real do usuario).
        static bool IsBgDeco(Transform t)
        {
            for (int i = 0; t != null && i < 4; i++, t = t.parent)
            {
                var n = t.name;
                if (n == "PixelRise" || n == "PixelStorm" || n == "Circuits") return true;
            }
            return false;
        }

        // Placa BRANCA de campo/nome (sprite valido mas cor clara e opaca) que cobre o
        // conteudo. Tamanho de "faixa" — nao um icone minusculo nem um painel gigante.
        static bool IsWhitePlate(Image img)
        {
            if (img.GetComponent<Mask>() != null) return false;
            var c = img.color;
            if (c.a < 0.55f) return false;
            if (c.r < 0.78f || c.g < 0.78f || c.b < 0.78f) return false;   // precisa ser claro
            // ARTE (capa de deck, carta, thumbnail): a Image tem tint BRANCO mas o SPRITE
            // e' colorido. Escurecer o tint apagaria a arte — entao ignora qualquer Image
            // com sprite de textura razoavel. So placas de "fill" (sprite nulo/minusculo).
            var sp = img.sprite;
            if (sp != null && sp.texture != null && sp.rect.width >= 32f && sp.rect.height >= 32f)
                return false;
            var s = img.rectTransform.rect.size;
            float w = Mathf.Abs(s.x), h = Mathf.Abs(s.y);
            if (w < 50f || h < 16f) return false;         // icone/checkmark: deixa
            // So BARRAS FINAS (nome de deck, campo de filtro). Miniatura/capa de deck e'
            // ALTA (~100px+) e carrega arte async — nunca escurecer, senao a arte some
            // ate a proxima reconstrucao da lista.
            if (w > 900f || h > 70f) return false;
            return true;
        }

        static bool IsBrokenArt(Image img)
        {
            var s = img.sprite;
            if (s == null || s.texture == null) return true;
            // Placeholder de arte faltando: sprite existe mas SEM NOME (textura branca).
            // As artes reais do menu tem nome (Japan, background_opening1, titleLogo5,
            // circle...); as caixas brancas de botao/painel vem com nome vazio.
            if (string.IsNullOrEmpty(s.name)) return true;
            return false;
        }

        static bool IsTMP(Graphic g)
        {
            var n = g.GetType().Name;
            return n.IndexOf("TextMeshPro") >= 0 || n.IndexOf("TMP_") >= 0;
        }

        static bool HasMissingScript(GameObject go)
        {
            var cs = go.GetComponents<Component>();
            for (int i = 0; i < cs.Length; i++) if (cs[i] == null) return true;
            return false;
        }

        // Decoracoes quebradas (ex.: SciFi_*) tem o script faltando na RAIZ e a Image
        // num filho — por isso subimos alguns niveis procurando o script ausente.
        static bool HasMissingScriptUp(Transform t, int levels)
        {
            for (int i = 0; t != null && i <= levels; i++, t = t.parent)
                if (HasMissingScript(t.gameObject)) return true;
            return false;
        }

        static bool Big(Image img)
        {
            var s = img.rectTransform.rect.size;
            return Mathf.Abs(s.x) > 120f && Mathf.Abs(s.y) > 60f;
        }

        // Ocupa a maior parte do Canvas -> e' um catcher de clique, nao um botao real.
        static bool IsFullscreen(Image img)
        {
            var canvas = img.canvas;
            if (canvas == null) return false;
            var crt = canvas.transform as RectTransform;
            if (crt == null) return false;
            var c = crt.rect.size; var b = img.rectTransform.rect.size;
            if (c.x <= 1f || c.y <= 1f) return false;
            // >85% em ambas as dimensoes = catcher de clique / fundo de tela cheia.
            // Paineis de janela (config/deck, ~75%) ficam abaixo disso e viram painel.
            return Mathf.Abs(b.x) > 0.85f * Mathf.Abs(c.x) && Mathf.Abs(b.y) > 0.85f * Mathf.Abs(c.y);
        }

        // Fundo grande com filhos interativos -> dialogo/janela: vira painel.
        static bool IsDialogBackground(Image img)
        {
            var size = img.rectTransform.rect.size;
            if (Mathf.Abs(size.x) < 260f || Mathf.Abs(size.y) < 150f) return false;
            return img.GetComponentInChildren<Selectable>(true) != null;
        }

        // Sprite branco de cantos arredondados (tingido via cor/ColorBlock), com
        // borda de 9-slice = raio para os cantos nao esticarem ao escalar.
        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(RoundedAlpha(x, y, size, radius) * 255f));
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                   100f, 0, SpriteMeshType.FullRect, border);
            sp.name = "DcgoSkinRounded";
            return sp;
        }

        // Degrade vertical (topo -> base) para fundo de tela profissional.
        static Sprite MakeVerticalGradient(int size, Color32 top, Color32 bottom)
        {
            var tex = new Texture2D(4, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[4 * size];
            for (int y = 0; y < size; y++)
            {
                float f = y / (float)(size - 1);
                Color32 c = Color32.Lerp(bottom, top, f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, 4, size), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "DcgoSkinGradient";
            return sp;
        }

        static float RoundedAlpha(int x, int y, int size, int r)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float cx = Mathf.Min(fx, size - fx);
            float cy = Mathf.Min(fy, size - fy);
            if (cx < r && cy < r)
            {
                float dx = r - cx, dy = r - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01(r - dist + 0.5f);   // AA ~1px
            }
            return 1f;
        }
    }
}
#endif
