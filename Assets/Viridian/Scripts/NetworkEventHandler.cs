using System;
#if EOS_SDK
using Epic.OnlineServices;
#endif
using static NetUtils.InternetConnectivityChecker;

public static class NetworkEventHandler
{
    public static event Action<InternetStatus> OnInternetStatusChanged;
    public static event Action<string> OnGoogleTokenReceived;
    public static event Action<string> OnGoogleAuthCodeReceived;
    
    // Network Events
    public static event Action OnAttemptManualLogin;
    public static event Action OnAttemptAutoLogin;
#if EOS_SDK
    public static event Action<ProductUserId> OnLoggedIn;
    public static event Action OnLoggedOut;
    public static event Action<ProductUserId> OnConnectedToServices;
#else
    public static event Action<object> OnLoggedIn;
    public static event Action OnLoggedOut;
    public static event Action<object> OnConnectedToServices;
#endif
    public static event Action OnConnectedToEOSServicesFailed;

    public static void AttemptManualLogin() => OnAttemptManualLogin?.Invoke();
    public static void AttemptAutoLogin() => OnAttemptAutoLogin?.Invoke();
#if EOS_SDK
    public static void Login(ProductUserId user) => OnLoggedIn?.Invoke(user);
    public static void Logout() => OnLoggedOut?.Invoke();
    public static void ConnectedToServices(ProductUserId user) => OnConnectedToServices?.Invoke(user);
#else
    public static void Login(object user) => OnLoggedIn?.Invoke(user);
    public static void Logout() => OnLoggedOut?.Invoke();
    public static void ConnectedToServices(object user) => OnConnectedToServices?.Invoke(user);
#endif
    public static void FailedConnectingToServices() => OnConnectedToEOSServicesFailed?.Invoke();
    public static void ChangeInternetStatus(InternetStatus status) => OnInternetStatusChanged?.Invoke(status);
    public static void GoogleTokenReceived(string token) => OnGoogleTokenReceived?.Invoke(token);
    public static void GoogleAuthCodeReceived(string code) => OnGoogleAuthCodeReceived?.Invoke(code);
}

