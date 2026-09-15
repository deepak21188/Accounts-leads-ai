import { EventType, PublicClientApplication, type AccountInfo } from "@azure/msal-browser"
import { msalConfig } from "@/auth/msalConfig"

export const msalInstance = new PublicClientApplication(msalConfig)

/**
 * Read by LoginPage on mount. Written here, at module scope, rather than from a component
 * effect, because handleRedirectPromise() (called inside MsalProvider's own effect) processes
 * a failed redirect response, fires LOGIN_FAILURE, and MSAL navigates back to
 * /accountant/login — all before LoginPage has (re)mounted to register its own listener. This
 * callback is registered at import time, before any of that happens, so it never misses the
 * event; sessionStorage survives the intervening full-page reload at the redirect URI.
 */
export const LOGIN_ERROR_STORAGE_KEY = "msalLoginError"

msalInstance.addEventCallback((event) => {
  if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
    const account = (event.payload as { account?: AccountInfo }).account
    if (account) {
      msalInstance.setActiveAccount(account)
    }
  }

  // msal-browser 5.x has no distinct LOGIN_FAILURE event — loginRedirect is implemented as a
  // redirect-based token acquisition, so a failed sign-in (denied consent, not assigned, etc.)
  // surfaces as ACQUIRE_TOKEN_FAILURE instead.
  if (event.eventType === EventType.ACQUIRE_TOKEN_FAILURE) {
    sessionStorage.setItem(LOGIN_ERROR_STORAGE_KEY, event.error?.message || "Sign-in failed.")
  }
})
