/**
 * Only same-origin paths are honoured as a return URL.
 *
 * `returnUrl` arrives in the query string, which anyone can write. Without this check a link such
 * as `/login?returnUrl=https://example.com` would turn the sign-in page into an open redirect —
 * the usual way a phishing link is made to look like it belongs to the site it impersonates.
 */
export function safeReturnUrl(value: string | null): string | null {
  if (!value || !value.startsWith('/') || value.startsWith('//')) {
    return null
  }

  return value
}
