using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class GoogleLoginHandler : MonoBehaviour
{
    private static string redirectUri = $"http://localhost:{GoogleRedirectListener.ListenPort}";

    private void OnEnable() {
        NetworkEventHandler.OnGoogleAuthCodeReceived += OnReceivedAuthCode;
    }
    private void OnDisable() {
        NetworkEventHandler.OnGoogleAuthCodeReceived -= OnReceivedAuthCode;
    }

    public static void StartGoogleLogin()
    {
        string scope = "openid%20email";
        string state = Guid.NewGuid().ToString("N");
        string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={SecureCredentials.GetClientId()}&redirect_uri={redirectUri}&response_type=code&scope={scope}&state={state}&access_type=offline&prompt=consent";

        Application.OpenURL(authUrl);
        GoogleRedirectListener.StartListening();
    }

    void OnReceivedAuthCode(string code) // Call this with the code captured from browser redirect
    {
        StartCoroutine(ExchangeAuthCodeForToken(code));
    }

    IEnumerator ExchangeAuthCodeForToken(string authCode)
    {
        WWWForm form = new WWWForm();
        form.AddField("code", authCode);
        form.AddField("client_id", SecureCredentials.GetClientId());
        form.AddField("client_secret", SecureCredentials.GetClientSecret());
        form.AddField("redirect_uri", redirectUri);
        form.AddField("grant_type", "authorization_code");

        using (UnityWebRequest www = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                //GameLogger.GetInstance().LogError("Token exchange failed: " + www.error);
            }
            else
            {
                var json = www.downloadHandler.text;
                var tokenResponse = JsonUtility.FromJson<GoogleTokenResponse>(json);
                NetworkEventHandler.GoogleTokenReceived(tokenResponse.id_token);
            }
        }
    }

    [Serializable]
    public class GoogleTokenResponse
    {
        public string access_token;
        public string expires_in;
        public string refresh_token;
        public string scope;
        public string token_type;
        public string id_token;
    }
}

