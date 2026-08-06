// PONTE DE COMPATIBILIDADE: o projeto original embute copias dos componentes
// padrao de UI como scripts soltos (guids proprios) que NAO estao no repo publico.
// Sem eles, cenas/prefabs perdem Image/Text/Button/etc (cartas de campo ilegiveis).
// Esta subclasse vazia, com o GUID ORIGINAL no .meta, faz o Unity deserializar os
// dados ja salvos nas cenas direto na classe base — a UI volta identica ao desktop.
public class DcgoCompatImage : UnityEngine.UI.Image {}
