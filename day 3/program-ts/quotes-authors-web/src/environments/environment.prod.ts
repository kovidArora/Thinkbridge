// Prod: everything routes through the proxy Web App. Reads use its
// managed-identity token; writes/auth pass through with the user's own JWT
// untouched (see api/index.js). Calling the real backend directly from the
// browser would need CORS configured there too — routing both through one
// origin (which already has CORS set up) avoids that entirely.
export const environment = {
  production: true,
  functionsBaseUrl: 'https://quotes-authors-web-api.azurewebsites.net',
  backendBaseUrl: 'https://quotes-authors-web-api.azurewebsites.net',
};
