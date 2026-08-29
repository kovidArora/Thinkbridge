const http = require('http');
const { getManagedIdentityToken } = require('./mi-token');

const BACKEND_URL = process.env.BACKEND_URL;

function sendJson(res, status, body) {
  res.writeHead(status, { 'content-type': 'application/json' });
  res.end(JSON.stringify(body));
}

// Reads for the SPA — authenticated to the Week-1 API with a managed-identity
// token, since this service is calling on its own behalf, not the user's.
async function proxyRead(res, path) {
  const token = await getManagedIdentityToken();
  const backendResponse = await fetch(`${BACKEND_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  const rawText = await backendResponse.text();
  if (!rawText) {
    throw new Error(`Backend returned an empty body: status=${backendResponse.status} path=${path}`);
  }
  sendJson(res, backendResponse.status, JSON.parse(rawText));
}

// Writes/auth carry the browser's own JWT, not a managed-identity token,
// since the action is performed as the signed-in user, not as this service.
async function proxyWrite(req, res, path) {
  const chunks = [];
  for await (const chunk of req) chunks.push(chunk);
  const body = chunks.length ? Buffer.concat(chunks) : undefined;

  const headers = {};
  if (req.headers.authorization) headers.authorization = req.headers.authorization;
  if (req.headers['content-type']) headers['content-type'] = req.headers['content-type'];

  const backendResponse = await fetch(`${BACKEND_URL}${path}`, {
    method: req.method,
    headers,
    body,
  });

  const text = await backendResponse.text();
  res.writeHead(backendResponse.status, {
    'content-type': backendResponse.headers.get('content-type') ?? 'application/json',
  });
  res.end(text);
}

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://localhost');

    if (req.method === 'GET' && url.pathname === '/api/debug-env') {
      return sendJson(res, 200, {
        hasIdentityEndpoint: Boolean(process.env.IDENTITY_ENDPOINT),
        hasIdentityHeader: Boolean(process.env.IDENTITY_HEADER),
        hasBackendUrl: Boolean(process.env.BACKEND_URL),
        hasEntraAudience: Boolean(process.env.ENTRA_AUDIENCE),
      });
    }

    if (req.method === 'GET' && url.pathname === '/api/quotes') {
      const page = url.searchParams.get('page') ?? '1';
      const size = url.searchParams.get('size') ?? '20';
      return await proxyRead(res, `/api/quotes?page=${encodeURIComponent(page)}&size=${encodeURIComponent(size)}`);
    }

    const quoteByIdMatch = url.pathname.match(/^\/api\/quotes\/([^/]+)$/);
    if (req.method === 'GET' && quoteByIdMatch) {
      return await proxyRead(res, `/api/quotes/${encodeURIComponent(quoteByIdMatch[1])}`);
    }

    if (req.method === 'GET' && url.pathname === '/api/authors/stats') {
      return await proxyRead(res, '/api/authors/stats');
    }

    if (url.pathname.startsWith('/api/')) {
      return await proxyWrite(req, res, url.pathname);
    }

    res.writeHead(404).end();
  } catch (err) {
    console.error(err);
    sendJson(res, 502, { error: 'Upstream request failed', detail: err.message });
  }
});

const port = process.env.PORT || 3000;
server.listen(port, () => console.log(`quotes-authors-web-api listening on ${port}`));
