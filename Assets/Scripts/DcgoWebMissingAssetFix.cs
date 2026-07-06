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
    // materiais e ~163 sprites de UI que so existem empacotados no build desktop).
    // Sem eles o WebGL mostra MAGENTA (material de erro) e CAIXAS BRANCAS (Image cujo
    // sprite/textura resolve pra null). Alem disso a UI de menu e' INSTANCIADA de
    // PREFABS em runtime, entao um fix unico no carregamento da cena nao alcanca os
    // botoes/paineis criados depois. Por isso esta camada:
    //
    //   * roda em LOOP (nao so no sceneLoaded), pegando UI instanciada tarde;
    //   * da aos botoes um sprite procedural limpo (cantos arredondados) + cores de
    //     hover/press, sem tocar em onClick nem na logica;
    //   * neutraliza a poluicao branca: fundo de dialogo vira painel escuro
    //     translucido; o resto fica transparente (e para de bloquear cliques);
    //   * NAO mexe na partida (BattleScene) nem no editor de deck (deckbuilder) —
    //     so nos menus / selecao de modo / selecao de deck / config / lobby.
    //
    // Tudo e' puramente visual: so altera Image.sprite / cor / material. A
    // funcionalidade (navegacao, Photon, decks) fica intacta.
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

        // Componentes que marcam o EDITOR de deck (deckbuilder) — excluidos do skin.
        // A LISTA/SELECAO de deck (DeckListPanel/SelectBattleDeck/SelectDeck) NAO
        // entra aqui de proposito: o usuario pediu para skinnar a selecao de deck.
        static readonly string[] DeckEditorMarkers =
        {
            "EditDeck", "DetailCard_DeckEditor", "CardPrefab_CreateDeck"
        };

        Sprite _btn, _panel;
        readonly HashSet<int> _done = new HashSet<int>();
        bool _logged;

        void Start()
        {
            _btn   = MakeRounded(48, 12);   // botao: cantos medios
            _panel = MakeRounded(72, 24);   // painel/dialogo: cantos grandes
            SceneManager.sceneLoaded += OnScene;
            StartCoroutine(Loop());
        }

        void OnScene(Scene s, LoadSceneMode m) { _done.Clear(); _logged = false; }

        IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(0.35f);
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

            // (c) Imagens.
            var images = Object.FindObjectsOfType<Image>(true);
            int broken = 0, skinned = 0, hidden = 0;
            List<string> sample = _logged ? null : new List<string>();

            foreach (var img in images)
            {
                if (!img) continue;

                if (battle) { Conservative(img); continue; }
                if (InDeckEditor(img.transform)) continue;

                var sel = SelectableOf(img);
                bool isBtnGraphic = sel != null && sel.targetGraphic == img;
                bool brokenArt = IsBrokenArt(img);

                if (isBtnGraphic)
                {
                    if (brokenArt && _done.Add(img.GetInstanceID())) { SkinButton(sel, img); skinned++; }
                    continue;
                }

                if (brokenArt)
                {
                    if (_done.Add(img.GetInstanceID()))
                    {
                        if (IsDialogBackground(img)) SkinPanel(img);
                        else Hide(img);
                        hidden++;
                    }
                    broken++;
                    if (sample != null && sample.Count < 30)
                        sample.Add(Path(img.transform) + "  sprite=" + (img.sprite ? img.sprite.name : "NULL"));
                }
                else if (sample != null && sample.Count < 30 && img.color == Color.white && img.sprite != null)
                {
                    // branco opaco mas COM sprite: registrar pra eu entender a fonte
                    // da caixa branca (ex.: textura branca embutida) e refinar depois.
                    sample.Add("[has-sprite] " + Path(img.transform) + " = " + img.sprite.name);
                }
            }

            if (!_logged)
            {
                _logged = true;
                Debug.Log("[DCGOSKIN] scene=" + SceneManager.GetActiveScene().name +
                          " images=" + images.Length + " brokenArt=" + broken +
                          " skinnedBtn=" + skinned + " hidden=" + hidden);
                if (sample != null) foreach (var s in sample) Debug.Log("[DCGOSKIN] " + s);
            }
        }

        // Comportamento conservador dentro da partida: nao redesenha, so evita
        // caixas brancas cobrindo o campo (igual ao fix antigo).
        static void Conservative(Image img)
        {
            if (img.sprite != null || img.color != Color.white) return;
            bool interactive = img.GetComponent<Selectable>()
                || (img.transform.parent && img.transform.parent.GetComponent<Selectable>());
            if (interactive) img.color = new Color(0.22f, 0.34f, 0.52f, 1f);
            else { img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = false; }
        }

        void SkinButton(Selectable sel, Image img)
        {
            img.sprite = _btn;
            img.type = Image.Type.Sliced;
            img.color = Color.white;          // a cor real vem do ColorBlock (multiplica)
            img.raycastTarget = true;

            // Estados via ColorTint (o sprite original de hover/press esta faltando).
            sel.transition = Selectable.Transition.ColorTint;
            sel.targetGraphic = img;
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

        // ------------------------------------------------------------- helpers ----

        static bool BrokenShader(Material m)
        {
            return m != null && (m.shader == null || m.shader.name == "Hidden/InternalErrorShader");
        }

        static bool IsBrokenArt(Image img)
        {
            if (img.sprite == null) return true;
            if (img.sprite.texture == null) return true;
            return false;
        }

        static Selectable SelectableOf(Image img)
        {
            var s = img.GetComponent<Selectable>();
            if (s != null) return s;
            var p = img.transform.parent;
            if (p) { var ps = p.GetComponent<Selectable>(); if (ps) return ps; }
            return null;
        }

        // Fundo grande com filhos interativos -> e' um dialogo/janela: vira painel.
        static bool IsDialogBackground(Image img)
        {
            var size = img.rectTransform.rect.size;
            if (size.x < 260f || size.y < 150f) return false;
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
            var p = t.parent;
            int depth = 0;
            while (p != null && depth < 4) { sb.Insert(0, p.name + "/"); p = p.parent; depth++; }
            return sb.ToString();
        }

        // Sprite branco de cantos arredondados (tingido via cor / ColorBlock), com
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
            float cx = Mathf.Min(fx, size - fx);   // dist ate a borda vertical mais perto
            float cy = Mathf.Min(fy, size - fy);   // dist ate a borda horizontal mais perto
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
