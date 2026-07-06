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
    //   * DECORACOES quebradas (GameObject com script faltando) e caixas brancas
    //     soltas: viram transparentes e param de bloquear cliques;
    //   * fundo de dialogo grande vira painel azul-escuro translucido.
    //
    // Escopo: NAO mexe na partida (BattleScene) nem no editor de deck (deckbuilder).
    // Tudo e' visual (Image.sprite/cor/material); a funcionalidade fica intacta.
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

        // Marcadores do EDITOR de deck (deckbuilder) — excluidos do skin. A
        // LISTA/SELECAO de deck NAO entra aqui de proposito (usuario pediu skin nela).
        static readonly string[] DeckEditorMarkers =
        {
            "EditDeck", "DetailCard_DeckEditor", "CardPrefab_CreateDeck"
        };

        Sprite _btn, _panel;
        readonly HashSet<int> _seenSel = new HashSet<int>();   // Selectables ja tratados
        readonly HashSet<int> _btnImg  = new HashSet<int>();   // Images que sao fundo de botao
        readonly HashSet<int> _doneImg = new HashSet<int>();   // Images de poluicao ja tratadas
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
            _seenSel.Clear(); _btnImg.Clear(); _doneImg.Clear(); _logged = false;
        }

        IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(0.4f);
            while (true) { try { Skin(); } catch { } yield return wait; }
        }

        // ---------------------------------------------------------------- skin ----

        void Skin()
        {
            bool battle = SceneManager.GetActiveScene().name == "BattleScene";

            // (a) Magenta: renderers 3D/particulas com material de erro -> esconder.
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
                if (r && (r.sharedMaterial == null || BrokenShader(r.sharedMaterial)))
                    r.enabled = false;

            // (b) UI com material de erro -> volta ao material padrao.
            foreach (var g in Object.FindObjectsOfType<Graphic>(true))
                if (g && BrokenShader(g.material))
                    g.material = null;

            if (battle)
            {
                foreach (var img in Object.FindObjectsOfType<Image>(true))
                    if (img) Conservative(img);
                return;
            }

            int skinned = 0, hidden = 0, masks = 0;
            List<string> survivors = _logged ? null : new List<string>();

            // (c) BOTOES: um passe por Selectable (pega fundo em qualquer profundidade).
            foreach (var sel in Object.FindObjectsOfType<Selectable>(true))
            {
                if (!sel || InDeckEditor(sel.transform)) continue;
                if (!_seenSel.Add(sel.GetInstanceID())) continue;
                var bg = PickButtonBackground(sel);
                if (bg == null) continue;
                _btnImg.Add(bg.GetInstanceID());
                SkinButton(sel, bg);
                skinned++;
            }

            // (d) RESTO das imagens: mascaras, decoracoes quebradas, caixas brancas.
            foreach (var img in Object.FindObjectsOfType<Image>(true))
            {
                if (!img || InDeckEditor(img.transform)) continue;
                int id = img.GetInstanceID();
                if (_btnImg.Contains(id)) continue;   // e' fundo de botao, ja tratado

                var mask = img.GetComponent<Mask>();
                if (mask != null)
                {
                    if (mask.showMaskGraphic && IsBrokenArt(img)) { mask.showMaskGraphic = false; masks++; }
                    continue;   // nunca mexe no alfa da mascara (quebraria o recorte)
                }

                if (!_doneImg.Add(id)) continue;

                if (HasMissingScript(img.gameObject) || IsBrokenArt(img))
                {
                    if (IsDialogBackground(img)) SkinPanel(img);
                    else Hide(img);
                    hidden++;
                }
                else if (survivors != null && survivors.Count < 25
                         && img.color.a > 0.6f && Big(img))
                {
                    // grande, opaco, COM sprite e sem script faltando: sobreviveu.
                    // Registro pra eu ver se ainda e' poluicao a tratar.
                    survivors.Add(Path(img.transform) + "  sprite=" + (img.sprite ? img.sprite.name : "NULL"));
                }
            }

            if (!_logged)
            {
                _logged = true;
                Debug.Log("[DCGOSKIN] scene=" + SceneManager.GetActiveScene().name +
                          " skinnedBtn=" + skinned + " hidden=" + hidden + " masksOff=" + masks);
                if (survivors != null) foreach (var s in survivors) Debug.Log("[DCGOSKIN survivor] " + s);
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
            img.color = new Color(1f, 1f, 1f, 0f);   // some com a caixa branca
            img.raycastTarget = false;               // e para de bloquear cliques
        }

        // Comportamento conservador dentro da partida: so evita caixas brancas.
        static void Conservative(Image img)
        {
            if (img.sprite != null || img.color != Color.white) return;
            if (img.GetComponent<Mask>() != null) return;
            bool interactive = img.GetComponent<Selectable>()
                || (img.transform.parent && img.transform.parent.GetComponent<Selectable>());
            if (interactive) img.color = new Color(0.22f, 0.34f, 0.52f, 1f);
            else { img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = false; }
        }

        // ------------------------------------------------------------- helpers ----

        static bool BrokenShader(Material m)
        {
            return m != null && (m.shader == null || m.shader.name == "Hidden/InternalErrorShader");
        }

        static bool IsBrokenArt(Image img)
        {
            return img.sprite == null || img.sprite.texture == null;
        }

        static bool HasMissingScript(GameObject go)
        {
            var cs = go.GetComponents<Component>();
            for (int i = 0; i < cs.Length; i++) if (cs[i] == null) return true;
            return false;
        }

        static bool Big(Image img)
        {
            var s = img.rectTransform.rect.size;
            return Mathf.Abs(s.x) > 120f && Mathf.Abs(s.y) > 60f;
        }

        // Fundo grande com filhos interativos -> dialogo/janela: vira painel.
        static bool IsDialogBackground(Image img)
        {
            var size = img.rectTransform.rect.size;
            if (Mathf.Abs(size.x) < 260f || Mathf.Abs(size.y) < 150f) return false;
            return img.GetComponentInChildren<Selectable>(true) != null;
        }

        static bool InDeckEditor(Transform t)
        {
            while (t != null)
            {
                var comps = t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;
                    var n = c.GetType().Name;
                    for (int k = 0; k < DeckEditorMarkers.Length; k++)
                        if (n == DeckEditorMarkers[k]) return true;
                }
                t = t.parent;
            }
            return false;
        }

        static string Path(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            var p = t.parent; int depth = 0;
            while (p != null && depth < 4) { sb.Insert(0, p.name + "/"); p = p.parent; depth++; }
            return sb.ToString();
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, border);
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
