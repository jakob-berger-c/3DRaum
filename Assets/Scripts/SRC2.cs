using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
 
/// <summary>
/// Lädt die Lehrer/Lehrerinnen-Seite der HTL Salzburg herunter,
/// extrahiert sichtbare Linktexte (<a>...</a>) und gibt die ersten
/// 'maxCount' Kandidaten in der Unity-Konsole aus.
/// </summary>
public class FetchFirstFiveTeachers : MonoBehaviour
{
    // URL der Seite mit der Lehrerliste (konstant)
    private const string TeachersUrl = "https://www.htl-salzburg.ac.at/lehrerinnen.html";
 
    // Anzahl der auszugebenden Einträge; im Unity Inspector anpassbar
    [SerializeField] private int maxCount = 5;
 
    /// <summary>
    /// Unity-Startmethode. Wird einmal beim Aktivieren des GameObjects aufgerufen.
    /// Hier starten wir die Coroutine, die den HTTP-Request asynchron durchführt.
    /// </summary>
    private void Start()
    {
        // UnityWebRequest muss asynchron ausgeführt werden (Coroutine).
        StartCoroutine(FetchAndPrintTeachers());
    }
 
    /// <summary>
    /// Coroutine: Lädt HTML, analysiert es und gibt die ersten 'maxCount' Lehrer aus.
    /// IEnumerator erlaubt yield return zur asynchronen Wartezeit.
    /// </summary>
    private IEnumerator FetchAndPrintTeachers()
    {
        // Erzeuge einen GET-Request. 'using' stellt sicher, dass www am Ende disposed wird.
        using (UnityWebRequest www = UnityWebRequest.Get(TeachersUrl))
        {
            // Setze ein Timeout (in Sekunden) — schützt gegen endloses Warten.
www.timeout = 15;
 
            // Sende den Request asynchron und warte auf das Ergebnis.
            yield return www.SendWebRequest();
 
            // Fehlerprüfung: Unity hat API-Änderungen in neueren Versionen.
#if UNITY_2020_1_OR_NEWER
            // In neueren Unity-Versionen verwendet man 'www.result' zur Prüfung.
            if (www.result != UnityWebRequest.Result.Success)
#else
            // In älteren Versionen gab es isNetworkError / isHttpError.
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                // Netz-/HTTP-Fehler: Ausgabe als Error und Abbruch der Coroutine.
                Debug.LogError($"Fehler beim Laden der Seite: {www.error}");
                yield break;
            }
 
            // Wenn wir hier sind, wurde die Seite erfolgreich geladen.
            // HTML-Quelltext als String aus dem DownloadHandler entnehmen.
            string html = www.downloadHandler.text;
 
            // 1) Extrahiere alle sichtbaren Texte aus <a>...</a>-Elementen.
            //    Diese Basisliste enthält rohe Texte, noch mit potentiellen HTML-Fragmenten.
            var anchorTexts = ExtractAnchorTexts(html);
 
            // 2) Filtern und säubern:
            //    - Leere/Whitespace-Einträge entfernen
            //    - HTML-Entities decodieren (grundlegende Fälle)
            //    - Heuristisch nur Texte mit Komma behalten (z.B. "Nachname Vorname, Titel")
            //    - Kurze Navigations-/Service-Texte aussortieren
            //    - Duplikate entfernen
            var teacherCandidates = anchorTexts
                .Where(t => !string.IsNullOrWhiteSpace(t))            // keine leeren Strings
                .Select(t => HtmlEntityDecode(t.Trim()))             // trimmen + einfache Entity-Dekodierung
                .Where(t => t.Contains(","))                         // Heuristik: Lehrereinträge enthalten Komma
                .Where(t => t.Length >= 6 && !IsLikelyNavigationText(t)) // sanity checks
                .Distinct()                                          // Duplikate entfernen
                .ToList();
 
            // 3) Nimm die ersten 'maxCount' Einträge (oder weniger, wenn nicht genug vorhanden)
            var firstFive = teacherCandidates.Take(maxCount).ToList();
 
            // Ausgabe/Logging je nach Ergebnis
            if (firstFive.Count == 0)
            {
                // Warnung: keine passenden Einträge gefunden (wahrscheinlich Heuristik-Problem)
                Debug.LogWarning("Keine Lehrereinträge gefunden (Parsing-Regel hat nichts erkannt).");
            }
            else
            {
                // Log-Anfang
                Debug.Log($"Gefundene Lehrer (erste {firstFive.Count}):");
                // Ausgeben jeder einzelnen Zeile in der Unity-Konsole
                for (int i = 0; i < firstFive.Count; i++)
                {
                    Debug.Log($"{i + 1}: {firstFive[i]}");
                }
            }
        } // using schließt und disposed UnityWebRequest automatisch
    }
 
    /// <summary>
    /// Extrahiert sichtbare Texte aller <a>...</a> Tags aus dem HTML.
    /// - Verwendet eine einfache Regex, die den inneren Text jeder <a>-Tag-Instanz liefert.
    /// - Entfernt anschließend eventuelle Unter-HTML-Tags innerhalb des Links.
    /// Hinweis: Diese Funktion ist pragmatisch; bei sehr komplexem HTML kann sie fehlschlagen.
    /// </summary>
    /// <param name="html">Der rohe HTML-Quelltext</param>
    /// <returns>Liste der bereinigten Link-Texte</returns>
    private static List<string> ExtractAnchorTexts(string html)
    {
        var list = new List<string>();
 
        // Regex-Pattern:
        //  - "<a[^>]*?>"  : öffnendes <a> Tag mit beliebigen Attributen (non-greedy)
        //  - "(.*?)"      : Gruppe 1, non-greedy, fängt den inneren Text
        //  - "</a>"       : schließendes Tag
        // Singleline: '.' matcht auch Zeilenumbrüche -> wichtig für mehrzeiliges HTML
        var anchorPattern = new Regex("<a[^>]*?>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
 
        // Führe das Matching aus und iteriere über alle Treffer
        var matches = anchorPattern.Matches(html);
 
        foreach (Match m in matches)
        {
            // Sicherheitsprüfung: Gruppe 1 muss existieren (der innere Text)
            if (m.Groups.Count > 1)
            {
                // Den Inhalt der ersten Capturing Group holen
                string inner = m.Groups[1].Value;
 
                // Falls im Inneren weitere Tags sind (z.B. <span>...</span>), entfernen wir sie.
                // Regex "<.*?>" matcht einfache HTML-Tags (nicht rekursiv/komplex).
                inner = Regex.Replace(inner, "<.*?>", string.Empty);
 
                // Nur sinnvolle (nicht-leere) Texte zur Liste hinzufügen
                if (!string.IsNullOrWhiteSpace(inner))
                    list.Add(inner);
            }
        }
 
        return list;
    }
 
    /// <summary>
    /// Heuristik: erkennt, ob ein gegebener Text wahrscheinlich ein Navigations-/Service-Label ist.
    /// Falls ja, möchten wir diesen Text nicht als Lehrereintrag interpretieren.
    /// </summary>
    private static bool IsLikelyNavigationText(string s)
    {
        // Stichwortliste mit typischen Navigations-Wörtern; erweiterbar
        var navKeywords = new[] { "Anmeldung", "News", "Kontakt", "Service", "Ausbildung", "Reset" };
 
        // Prüfe, ob eines der Keywords (case-insensitive) im Text vorkommt.
        foreach (var kw in navKeywords)
            if (s.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
 
        return false;
    }
 
    /// <summary>
    /// Basale HTML-Entity-Dekodierung für häufige Fälle (Ampersand, Umlaute, Zitat, Apostroph).
    /// Hinweis: Nicht vollständig — für alle möglichen Entities wäre ein vollständiger Decoder nötig.
    /// </summary>
    private static string HtmlEntityDecode(string s)
    {
        // Direkte Ersetzungen: deckt die in vielen deutschen Seiten häufigen Entities ab.
        return s.Replace("&amp;", "&")
                .Replace("&nbsp;", " ")
                .Replace("&ouml;", "ö")
                .Replace("&uuml;", "ü")
                .Replace("&auml;", "ä")
                .Replace("&Ouml;", "Ö")
                .Replace("&Uuml;", "Ü")
                .Replace("&Auml;", "Ä")
                .Replace("&#39;", "'")
                .Replace("&quot;", "\"");
    }
}