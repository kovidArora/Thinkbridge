// Prod: reads go through the managed-identity proxy Web App; writes/auth go
// straight to the real Week-1 API since they carry the user's own JWT.
export const environment = {
  production: true,
  functionsBaseUrl: 'https://quotes-authors-web-api.azurewebsites.net',
  backendBaseUrl: 'https://quotes-api.happymushroom-1763810c.centralindia.azurecontainerapps.io',
};
