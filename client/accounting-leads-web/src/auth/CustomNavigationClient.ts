import { NavigationClient, type NavigationOptions } from "@azure/msal-browser"
import type { NavigateFunction } from "react-router"

/**
 * Bridges MSAL's internal navigation calls to React Router's history so browser back/forward
 * stays correct across login/logout redirects, per MSAL React's documented integration pattern.
 */
export class CustomNavigationClient extends NavigationClient {
  constructor(private readonly navigate: NavigateFunction) {
    super()
  }

  override async navigateInternal(url: string, options: NavigationOptions): Promise<boolean> {
    const relativePath = url.replace(window.location.origin, "")

    if (options.noHistory) {
      this.navigate(relativePath, { replace: true })
    } else {
      this.navigate(relativePath)
    }

    return false
  }
}
