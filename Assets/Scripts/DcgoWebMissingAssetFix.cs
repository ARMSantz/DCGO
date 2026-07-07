#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

        Sprite _btn, _panel;
        readonly HashSet<int> _seenSel = new HashSet<int>();   // Selectables ja tratados
        readonly HashSet<int> _btnImg  = new HashSet<int>();   // Images que sao fundo de botao
        readonly HashSet<int> _doneImg = new HashSet<int>();   // Images de poluicao ja tratadas
        readonly HashSet<int> _diag    = new HashSet<int>();   // ja logados no diagnostico
        bool _logged;

        void Start()
        {
            _btn   = MakeRounded(48, 12);   // botao: cantos medios
            _panel = MakeRounded(72, 24);   // painel/dialogo: cantos grandes
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
            // NAO basta. Enquanto a BattleScene existir (carregada), pular tudo: assim
            // FindObjectsOfType nunca toca nas cartas/campo. Ao fim da partida ela e'
            // descarregada e o menu volta a ser skinnado.
            if (SceneManager.GetSceneByName("BattleScene").isLoaded) return;

            // (a) Magenta: SO renderers com shader de ERRO (InternalError). NAO mexer em
            //     material nulo — carta/preview carregando a textura tem material nulo por
            //     um instante e nao pode ser desabilitada pra sempre.
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
                if (r && BrokenShader(r.sharedMaterial))
                    r.enabled = false;

            // (b) UI glow com shader de erro (GlowImage/UIEffect stripados). Texto/botao
            //     -> volta ao material padrao (legivel/clicavel). Decoracao solta -> some.
            foreach (var g in Object.FindObjectsOfType<Graphic>(true))
            {
                if (!g || !BrokenShader(g.material)) continue;
                bool keep = g is Text || IsTMP(g) || InSelectable(g.transform);
                if (keep) g.material = null;
                else g.enabled = false;
            }

            int skinned = 0, hidden = 0, masks = 0;

            // (c) BOTOES: skin do fundo em qualquer profundidade (fica visivel/clicavel).
            foreach (var sel in Object.FindObjectsOfType<Selectable>(true))
            {
                if (!sel) continue;
                if (!_seenSel.Add(sel.GetInstanceID())) continue;
                var bg = PickButtonBackground(sel);
                if (bg == null) continue;
                _btnImg.Add(bg.GetInstanceID());
                SkinButton(sel, bg);
                skinned++;
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

                // Parte de um CONTROLE (X de fechar, filtro, checkmark, seta, icone de
                // botao): NUNCA esconder — perder isso e' perder funcao. Deixa visivel.
                if (InSelectable(img.transform)) continue;

                if (!_doneImg.Add(id)) continue;

                bool broken = HasMissingScriptUp(img.transform, 2) || IsBrokenArt(img);
                if (!broken) continue;

                // So decoracao/painel: dialogo -> painel escuro; caixa GRANDE -> some.
                // Caixinha quebrada solta fica como esta (evita apagar algo util).
                if (IsDialogBackground(img) && !IsFullscreen(img)) { SkinPanel(img); hidden++; }
                else if (Big(img) || IsFullscreen(img)) { Hide(img); hidden++; }
            }

            if (skinned > 0 || hidden > 0 || !_logged)
            {
                _logged = true;
                Debug.Log("[DCGOSKIN] scene=" + SceneManager.GetActiveScene().name +
                          " skinnedBtn=" + skinned + " hidden=" + hidden + " masksOff=" + masks);
            }
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
