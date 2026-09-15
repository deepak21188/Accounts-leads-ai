import type { Configuration } from "@azure/msal-browser"

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_ENTRA_CLIENT_ID,
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID}`,
    redirectUri: import.meta.env.VITE_ENTRA_REDIRECT_URI,
    postLogoutRedirectUri: import.meta.env.VITE_ENTRA_REDIRECT_URI,
  },
  cache: {
    // Smaller XSS blast radius than localStorage (cleared when the tab closes), at the cost of
    // re-login when a new tab is opened — an acceptable trade for a 3-20 person internal
    // dashboard. See docs/architecture.md §23.
    cacheLocation: "sessionStorage",
  },
}

export const loginRequest = { scopes: [import.meta.env.VITE_ENTRA_API_SCOPE] }

export const apiTokenRequest = { scopes: [import.meta.env.VITE_ENTRA_API_SCOPE] }
