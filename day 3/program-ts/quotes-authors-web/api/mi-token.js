// App Service's own Managed Identity endpoint (no npm dependency needed) —
// see https://learn.microsoft.com/azure/app-service/overview-managed-identity
const ENTRA_AUDIENCE = process.env.ENTRA_AUDIENCE;

let cached = null;

async function getManagedIdentityToken() {
  const now = Date.now();
  if (cached && cached.expiresOn - now > 60_000) {
    return cached.token;
  }

  const url = `${process.env.IDENTITY_ENDPOINT}?resource=${encodeURIComponent(ENTRA_AUDIENCE)}&api-version=2019-08-01`;
  const response = await fetch(url, {
    headers: { 'X-IDENTITY-HEADER': process.env.IDENTITY_HEADER },
  });

  const rawText = await response.text();

  if (!response.ok) {
    throw new Error(`IMDS token request failed: status=${response.status} body=${rawText.slice(0, 300)}`);
  }
  if (!rawText) {
    throw new Error(`IMDS token request returned an empty body: status=${response.status}`);
  }

  const body = JSON.parse(rawText);
  cached = { token: body.access_token, expiresOn: Number(body.expires_on) * 1000 };
  return cached.token;
}

module.exports = { getManagedIdentityToken };
