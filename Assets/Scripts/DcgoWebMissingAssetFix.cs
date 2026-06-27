#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DcgoWebBuild
{
    // O repositorio publico nao traz toda a "skin" dos menus (faltam ~15 materiais
    // e ~163 sprites de UI que so existem empacotados no build desktop). Sem eles,
    // o WebGL mostra MAGENTA (material faltando -> shader de erro) e BRANCO (Image
    // sem sprite). Este hook, rodando a cada cena, neutraliza esses casos para os
    // menus ficarem usaveis: botoes visiveis/clicaveis e paineis legiveis.
    public static class DcgoWebMissingAssetFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (scene, mode) => FixAll();
            FixAll();
        }

        static bool BrokenShader(Material m)
        {
            return m != null && (m.shader == null || m.shader.name == "Hidden/InternalErrorShader");
        }

        static void FixAll()
        {
            // 1) Renderers 3D / particulas com material faltando (magenta) -> esconder.
            //    Sao decoracoes de fundo; some o magenta sem afetar a jogabilidade.
            foreach (var r in Object.FindObjectsOfType<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || BrokenShader(m))
                    r.enabled = false;
            }

            // 2) UI com material de erro -> volta ao material padrao da UI.
            foreach (var g in Object.FindObjectsOfType<Graphic>(true))
            {
                if (BrokenShader(g.material))
                    g.material = null; // null => defaultGraphicMaterial
            }

            // 3) Image sem sprite renderiza branco. Em vez de esconder (botoes sumiam),
            //    deixa VISIVEL: botoes ganham cor distinta clicavel; paineis ficam
            //    cinza-claro (texto escuro continua legivel). So mexe quando a cor
            //    esta no branco padrao (nao toca em elementos ja coloridos).
            foreach (var img in Object.FindObjectsOfType<Image>(true))
            {
                if (img.sprite != null) continue;
                if (img.color != Color.white) continue;

                bool interactive = img.GetComponent<Selectable>() != null
                                   || (img.transform.parent != null
                                       && img.transform.parent.GetComponent<Selectable>() != null);

                if (interactive)
                    img.color = new Color(0.22f, 0.34f, 0.52f, 1f);   // botao: azul visivel opaco
                else
                    img.color = new Color(0.80f, 0.82f, 0.86f, 0.92f); // painel: cinza claro (texto escuro legivel)
            }
        }
    }
}
#endif
