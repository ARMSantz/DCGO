using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Parte cloud do ContinuousController (classe partial — ver PATCH.md, passo 1).
// Reaproveita o parsing e o CreateDeckFromFile privados já existentes para
// reconstruir os decks vindos do backend, mantendo total compatibilidade de
// formato com o jogo de desktop.
public partial class ContinuousController
{
    // Reconstrói DeckDatas a partir dos decks salvos na nuvem.
    // Chame no lugar de LoadDeckLists() em WebGL (ver PATCH.md, passo 2).
    public IEnumerator LoadDecksFromCloud()
    {
        if (!DcgoWebBridge.IsLoggedIn)
        {
            Debug.LogWarning("[DcgoWeb] Sem login; nenhum deck carregado da nuvem.");
            yield break;
        }

        List<DcgoWebClient.CloudDeck> decks = null;
        string error = null;

        yield return DcgoWebClient.Instance.GetDecks(d => decks = d, e => error = e);

        if (error != null)
        {
            Debug.LogError("[DcgoWeb] Falha ao carregar decks: " + error);
            yield break;
        }

        DeckDatas = new List<DeckData>();

        int index = 0;
        foreach (var cloud in decks)
        {
            try
            {
                ParseAndAddDeck(cloud.id.ToString(), cloud.deckCode, index);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DcgoWeb] Deck '{cloud.name}' inválido: {ex.Message}");
            }
            index++;
        }

        DeckDatas = DeckDatas.OrderBy(x => x.DeckName).ToList();
    }

    // Mesmo parsing de LoadDeckLists(), mas a partir de uma string (não arquivo).
    private void ParseAndAddDeck(string id, string deckFile, int index)
    {
        // Formato (DeckCodeUtility.GetDeckBuilderFile):
        //   Name: <nome>
        //   Key Card: <int>
        //   Sort Index: <int>
        //   <linha em branco>
        //   // DeckList ...
        string[] lines = deckFile.Replace("\r\n", "\n").Split('\n');

        string name = lines.Length > 0 ? lines[0].Replace("Name: ", "") : "Deck";
        int keyCard = (lines.Length > 1 && int.TryParse(lines[1].Replace("Key Card: ", ""), out var k)) ? k : -1;
        int sortValue = (lines.Length > 2 && int.TryParse(lines[2].Replace("Sort Index: ", ""), out var s)) ? s : index;
        if (sortValue < 0) sortValue = 0;

        int slashIndex = deckFile.IndexOf("//", StringComparison.Ordinal);
        string deck = slashIndex >= 0 ? deckFile.Substring(slashIndex) : deckFile;

        // CreateDeckFromFile é privado mas acessível aqui (mesma classe partial).
        CreateDeckFromFile(id, name, keyCard, deck, sortValue);
    }

    // Salva um deck na nuvem (cria se novo, atualiza se já existir).
    public IEnumerator SaveDeckToCloud(DeckData data)
    {
        string deckFile = DeckCodeUtility.GetDeckBuilderFile(data);
        string error = null;

        if (int.TryParse(data.DeckID, out int backendId) && backendId > 0)
        {
            yield return DcgoWebClient.Instance.UpdateDeck(
                backendId, data.DeckName, deckFile, _ => { }, e => error = e);
        }
        else
        {
            yield return DcgoWebClient.Instance.CreateDeck(
                data.DeckName, deckFile, created => data.DeckID = created.id.ToString(), e => error = e);
        }

        if (error != null)
            Debug.LogError("[DcgoWeb] Falha ao salvar deck: " + error);
    }

    // Remove um deck da nuvem.
    public IEnumerator DeleteDeckFromCloud(DeckData data)
    {
        if (!int.TryParse(data.DeckID, out int backendId) || backendId <= 0)
            yield break;

        string error = null;
        yield return DcgoWebClient.Instance.DeleteDeck(backendId, () => { }, e => error = e);
        if (error != null)
            Debug.LogError("[DcgoWeb] Falha ao excluir deck: " + error);
    }
}
