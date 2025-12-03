using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Text.RegularExpressions;
 
public class LehrerDetailsLoader : MonoBehaviour
{
    [Header("TMP Text Fields")]
    public TextMeshProUGUI tmpText1; // erstes textfeld für schweiberer
    public TextMeshProUGUI tmpText2; // zweites für meerwald
 
    [Header("URLs der Lehrer-Seiten")]
    public string url1 = "https://www.htl-salzburg.ac.at/lehrerinnen-details/meerwald-stadler-susanne-prof-dipl-ing-g-009.html%22";
    public string url2 = "https://www.htl-salzburg.ac.at/lehrerinnen-details/schweiberer-franz-prof-dipl-ing-c-205.html%22";
 
    void Start()
    {
        if (tmpText1 != null)
            StartCoroutine(LoadLehrerDetails(url1, tmpText1));
 
        if (tmpText2 != null)
            StartCoroutine(LoadLehrerDetails(url2, tmpText2));
    }
 
    IEnumerator LoadLehrerDetails(string url, TextMeshProUGUI tmpText)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();
 
        if (www.result != UnityWebRequest.Result.Success)
        {
            tmpText.text = "Fehler beim Laden der Seite: " + www.error;
        }
        else
        {
            string html = www.downloadHandler.text;
 
            
            string name = Regex.Match(html, @"<div class=""field Lehrername"">.*?<span class=""text"">(.*?)</span>", RegexOptions.Singleline).Groups[1].Value;
            string raum = Regex.Match(html, @"<div class=""field Raum"">.*?<span class=""text"">(.*?)</span>", RegexOptions.Singleline).Groups[1].Value;
            string sprechstunde = Regex.Match(html, @"<div class=""field SprStunde"">.*?<span class=""text"">(.*?)</span>", RegexOptions.Singleline).Groups[1].Value;
 
            tmpText.text = $"Name: {name}\nRaum: {raum}\nSprechstunde: {sprechstunde}";
        }
    }
}